using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RunProcessAsTask;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OcrMinion
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // todo: graceful shutdown https://stackoverflow.com/questions/40742192/how-to-do-gracefully-shutdown-on-dotnet-with-docker
            #region configuration

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(configure =>
                {
                    configure.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddEnvironmentVariables();
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
                        config.ApiKey = hostContext.Configuration.GetValue<string>("OCRM_APIKEY");
                        config.Server = hostContext.Configuration.GetValue<string>("OCRM_SERVER"); //todo: default value should be guid
                        config.Demo = hostContext.Configuration.GetValue<bool>("OCRM_DEMO", false);
                    });
                    services.AddHttpClient<IHlidacRest, HlidacRest>(config =>
                    {
                        config.BaseAddress = new Uri(hostContext.Configuration.GetValue<string>("base_address"));
                        config.DefaultRequestHeaders.Add("User-Agent", hostContext.Configuration.GetValue<string>("user_agent"));
                    });
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
                        // todo: try catch, polly???
                        HlidacTask currentTask = await taskQueue.Dequeue();

                        logger.LogInformation($"Starting OCR process of {++taskCounter}. task.");
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
                            string tesseractArgs = $"tesseract {currentTask.InternalFileName} {currentTask.InternalFileName} -l CES --psm 1 --dpi 300".Replace("\"", "\\\"");
                            tesseractTask = ProcessEx.RunAsync("/bin/sh", tesseractArgs);
                        }

                        // we can preload new image here, so we doesnt have to wait for it later
                        taskQueue.Enqueue(GetNewImage(hlidacRest, logger));

                        var tesseractResult = await tesseractTask;

                        DateTime taskEnd = DateTime.Now;
                        if (tesseractResult.ExitCode == 0)
                        {
                            logger.LogInformation($"OCR process of {taskCounter}. task successfully finished.");
                            string text = await File.ReadAllTextAsync($"{currentTask.InternalFileName}.txt", Encoding.UTF8);

                            HlidacDocument document = new HlidacDocument(currentTask.TaskId, 
                                taskStart, taskEnd, currentTask.OrigFileName, text, 
                                tesseractResult.RunTime.TotalSeconds.ToString());

                            await hlidacRest.SendResultAsync(currentTask.TaskId, document);

                            File.Delete(currentTask.InternalFileName);
                            File.Delete(currentTask.InternalFileName +".txt");
                        }
                        else
                        {
                            logger.LogWarning($"OCR process of {taskCounter}. task unsuccessfully finished.");
                            logger.LogWarning(string.Join('\n', tesseractResult.StandardError));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Guess what? Something went wrong and we don't know what.");
                }
            }

            return 0;
        }

        private static async Task<HlidacTask> GetNewImage(IHlidacRest hlidacRest, ILogger logger)
        {
            logger.LogInformation("Getting new image.");
            HlidacTask task = await hlidacRest.GetTaskAsync();

            if (task != null && !string.IsNullOrWhiteSpace(task.TaskId))
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
                // retry?? or polly? :)
            }
            return task;
        }
    }
}

/*
API:
1) Získání tasku
env:
apikey
server (pokud není, tak vygenerovat vlastní)

GET https://ocr.hlidacstatu.cz/task.ashx?apikey=APIKEY&server=DockerXYZ&minPriority=0&maxPriority=99&type=image

pokud pridate &demo=1, pak to vrati testovaci task(ten ma nulove GUID). pri demo = 1 muze byt APIKEY cokoliv

to vrati
{"TaskId":"00000000-0000-0000-0000-000000000000","Priority":5,"Intensity":0,"OrigFilename":"testfile.jpg","localTempFile":null}

anebo to nevrati nic.Pak zadny task ve fronte neni.

server= DockerXYZ je jmeno serveru, ktery o task zada (rekneme jmeno Docker stroje nebo neco takoveho)

2) Pokud je nejaky task, je nutne ziskat soubor k analyze
GET https://ocr.hlidacstatu.cz/task.ashx?taskid=00000000-0000-0000-0000-000000000000

to vrati binarku souboru(v tomto pripade vzdy JPEG). U nuloveho GUID vzdy stejny testovaci soubor.

tesseract volame s parametry  tesseract {filename} {filename} -l CES --psm 1 --dpi 300

3) Kdyz je zpracovani hotove, je nutne zavolat

POST https://ocr.hlidacstatu.cz/donetask.ashx?taskid=00000000-0000-0000-0000-000000000000&method=done

{
   "Id”:"00000000-0000-0000-0000-000000000000",
   "Documents":[
      {
         "ContentType":"image/jpeg",
         "Filename":"_!_img3.jpg",
         "Text":" \n\nKB\n\n \n\nČíslo účtu | 107-5493970277 /0100|\n\n \n\nKomerční banka, a.s., ",
         "Confidence":0.0,
         "UsedOCR":true,
         "Pages":0,
         "RemainsInSec":0.0,
         "UsedTool”:”Tesseract",
         "Server”:”DockerXYZ"
      }
   ],
   "Server”:”DockerXYZ",
   "Started":"2019-09-24T03:07:00.4195551+02:00",
   "Ends":"2019-09-24T03:07:30.1851238+02:00",
   "IsValid":1,
   "Error":null
}

k JSON:
- Documents.Text - ziskany text z Tesseract
- Documents.Confidence - nekdy vraci Tesseract
- Documents.UsedOCR - vzdyt true
- Documents.Pages - vzdy 0
- Documents.RemainsInSec: doba v sekundach, jak dlouho task bezel
- Documents.UsedTool: ’Tesseract'
- IsValid: pokud vse ok, pak 1. Jinak 0
- Error: pokud nastala chyba, pak sem chybova hlaska
*/