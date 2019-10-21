using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RunProcessAsTask;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OcrMinion
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            #region preconfiguration
            const string env_apiKey = "OCRM_APIKEY";
            const string env_server = "OCRM_SERVER";
            const string env_demo = "OCRM_DEMO";
            const string base_address = "base_address";
            const string user_agent = "user_agent";

            var confBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            var appConfiguration = confBuilder.Build();

            // check if api key is set, otherwise, close app
            if (string.IsNullOrWhiteSpace(appConfiguration.GetValue<string>(env_apiKey)))
            {
                Console.WriteLine($"Environment variable{env_apiKey} is not set. Exiting app.");
                return 1;
            }
            // write basic configuration to stdout
            Console.WriteLine("Loaded configuration:");
            Console.WriteLine($"  {env_apiKey}={appConfiguration.GetValue<string>(env_apiKey)}");
            Console.WriteLine($"  {env_server}={appConfiguration.GetValue<string>(env_server)}");
            Console.WriteLine($"  {env_demo}={appConfiguration.GetValue<bool>(env_demo)}");
            Console.WriteLine($"  {base_address}={appConfiguration.GetValue<string>(base_address)}");
            Console.WriteLine($"  {user_agent}={appConfiguration.GetValue<string>(user_agent)}");


            #endregion
            // todo: graceful shutdown https://stackoverflow.com/questions/40742192/how-to-do-gracefully-shutdown-on-dotnet-with-docker
            #region configuration

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(configure =>
                {
                    configure.AddConfiguration(appConfiguration);
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    logging.AddConsole(config =>
                    {
                        config.TimestampFormat = "yyyy-MM-dd HH:mm:ss; ";
                    });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<HlidacOption>(config =>
                    {
                        config.ApiKey = hostContext.Configuration.GetValue<string>(env_apiKey);
                        config.Server = hostContext.Configuration.GetValue<string>(env_server,
                            Guid.NewGuid().ToString());
                        config.Demo = hostContext.Configuration.GetValue<bool>(env_demo, false);
                    });

                    services.AddHttpClient<IHlidacRest, HlidacRest>(config =>
                    {
                        config.BaseAddress = new Uri(hostContext.Configuration.GetValue<string>(base_address));
                        config.DefaultRequestHeaders.Add("User-Agent", hostContext.Configuration.GetValue<string>(user_agent));
                    })
                    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                        TimeSpan.FromMinutes(5), // polly wait max 5 minutes for response
                        Polly.Timeout.TimeoutStrategy.Optimistic))
                    .AddTransientHttpErrorPolicy(pb => pb.RetryAsync(1000)) // polly retry sending request if it fails up to 1000 times
                    .AddTransientHttpErrorPolicy(pb => pb.CircuitBreakerAsync(5, TimeSpan.FromMinutes(1))); // if request fails 5 times consecutively, then wait 1 minute before sending another request
                }).UseConsoleLifetime();

            var host = builder.Build();

            #endregion configuration

            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                int taskCounter = 0;
                try
                {
                    var hlidacRest = services.GetRequiredService<IHlidacRest>();
                    var taskQueue = new Queue<Task<HlidacTask>>(3);

                    logger.LogInformation("OCR minion initialized.");
                    taskQueue.Enqueue(GetNewImage(hlidacRest, logger));

                    while (true)
                    {
                        HlidacTask currentTask = await taskQueue.Dequeue();

                        logger.LogInformation($"Starting OCR process of #{++taskCounter}. task.");

                        if (currentTask.IsValid)
                        {
                            // run OCR and wait for its end
                            // to run OCR asynchronously I am using this library: https://github.com/jamesmanning/RunProcessAsTask
                            DateTime taskStart = DateTime.Now;
                            Task<ProcessResults> tesseractTask;
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                // this part is here only for debugging purposes
                                string tesseractArgs = $"{currentTask.InternalFileName} {currentTask.InternalFileName}";
                                tesseractTask = ProcessEx.RunAsync("tesseract.exe", tesseractArgs);
                            }
                            else
                            {
                                string tesseractArgs = $"{currentTask.InternalFileName} {currentTask.InternalFileName} -l ces --psm 1 --dpi 300".Replace("\"", "\\\"");
                                tesseractTask = ProcessEx.RunAsync("tesseract", tesseractArgs);
                            }

                            // we can preload new image here, so we doesnt have to wait for it later
                            taskQueue.Enqueue(GetNewImage(hlidacRest, logger));

                            var tesseractResult = await tesseractTask;

                            DateTime taskEnd = DateTime.Now;
                            if (tesseractResult.ExitCode == 0)
                            {
                                logger.LogInformation($"OCR process of #{taskCounter}. task successfully finished.");
                                string text = await File.ReadAllTextAsync($"{currentTask.InternalFileName}.txt", Encoding.UTF8);

                                HlidacDocument document = new HlidacDocument(currentTask.TaskId,
                                    taskStart, taskEnd, currentTask.OrigFileName, text,
                                    tesseractResult.RunTime.TotalSeconds.ToString());

                                await hlidacRest.SendResultAsync(currentTask.TaskId, document);

                                File.Delete(currentTask.InternalFileName);
                                File.Delete(currentTask.InternalFileName + ".txt");
                            }
                            else
                            {
                                logger.LogWarning($"OCR process of #{taskCounter}. task unsuccessfully finished.");
                                logger.LogWarning(string.Join('\n', tesseractResult.StandardError));
                            }
                        }
                        else //task was invalid, load new task
                        {
                            logger.LogInformation($"Task #{taskCounter} was invalid.");
                            taskQueue.Enqueue(GetNewImage(hlidacRest, logger));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Guess what? Something went wrong and we don't know what.");
                    // todo: send this error message to a server
                    return 1;
                }
            }
        }

        private static async Task<HlidacTask> GetNewImage(IHlidacRest hlidacRest, ILogger logger)
        {
            logger.LogInformation("Getting new image.");
            HlidacTask task = await hlidacRest.GetTaskAsync();

            if (!string.IsNullOrWhiteSpace(task.TaskId))
            {
                var downloadStream = await hlidacRest.GetFileToAnalyzeAsync(task.TaskId);
                using (var fileStream = new FileStream(task.InternalFileName, FileMode.Create, FileAccess.Write))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }
                logger.LogInformation($"Image for task[{task.TaskId}] successfully downloaded.");
            }
            else
            {
                string invalidTask = Newtonsoft.Json.JsonConvert.SerializeObject(task);
                logger.LogWarning($"Returned task is invalid. \n{invalidTask}");
                task.IsValid = false;
                
                // invalid task is probably because there were no tasks to process on server side, we need to wait some time
                // todo - this can be done in polly probably
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
            return task;
        }
    }
}
