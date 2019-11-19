using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HlidacStatu.Service.OCRApi;

namespace HlidacStatu.OcrMinion
{
    internal class Program
    {
        private static async Task<int> Main()
        {
            #region preconfiguration

            const string env_apiKey = "OCRM_APIKEY";
            const string env_email = "OCRM_EMAIL";
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
            Console.WriteLine($"  {env_email}={appConfiguration.GetValue<string>(env_email)}");
            Console.WriteLine($"  {env_demo}={appConfiguration.GetValue<bool>(env_demo)}");
            Console.WriteLine($"  {base_address}={appConfiguration.GetValue<string>(base_address)}");
            Console.WriteLine($"  {user_agent}={appConfiguration.GetValue<string>(user_agent)}");

            #endregion preconfiguration

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
                    logging.AddConsole(); // time doesn't need timestamp to it, because it is appended by docker
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<ClientOptions>(config =>
                    {
                        config.ApiKey = hostContext.Configuration.GetValue<string>(env_apiKey);
                        config.Email = hostContext.Configuration.GetValue<string>(env_email);
                        if (string.IsNullOrWhiteSpace(config.Email))
                        {
                            config.Email = Guid.NewGuid().ToString();
                        }
                        config.Demo = hostContext.Configuration.GetValue<bool>(env_demo, false);
                    });

                    services.AddHttpClient<IClient, RestClient>(config =>
                    {
                        config.BaseAddress = new Uri(hostContext.Configuration.GetValue<string>(base_address));
                        config.DefaultRequestHeaders.Add("User-Agent", hostContext.Configuration.GetValue<string>(user_agent));
                    })
                    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                        TimeSpan.FromMinutes(5), // polly wait max 5 minutes for response
                        Polly.Timeout.TimeoutStrategy.Optimistic))
                    .AddTransientHttpErrorPolicy(pb => 
                        pb.WaitAndRetryAsync(400, 
                            retryAttempt => TimeSpan.FromSeconds(retryAttempt/20) )
                        ); // total waiting time in case of repeating transient error 
                           // should be about 67 minutes, then app restarts

                    services.AddScoped<OcrFlow>();
                }).UseConsoleLifetime();

            var host = builder.Build();

            #endregion configuration

            int taskCounter = 0;
            Console.WriteLine("OCR minion initialized. Hold on to your hat...");
            while (true)
            {
                // for every task we create a clean scope
                using (var serviceScope = host.Services.CreateScope())
                {
                    var services = serviceScope.ServiceProvider;
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    try
                    {
                        logger.LogInformation($"Starting OCR process of #{++taskCounter}. task.");
                        OcrFlow flow = services.GetRequiredService<OcrFlow>();
                        await flow.RunFlow();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Guess what? Something went wrong and we don't know what.");
                        // todo: send this error message to a server
                        return 1;
                    }
                } 
            }
        }

        
    }
}