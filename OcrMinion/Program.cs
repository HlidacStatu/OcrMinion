using HlidacStatu.Service.OCRApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace HlidacStatu.OcrMinion
{
    internal class Program
    {
        private static void Main()
        {
            #region preconfiguration

            const string env_apiKey = "OCRM_APIKEY";
            const string env_email = "OCRM_EMAIL";
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
                return;
            }
            // write basic configuration to stdout
            Console.WriteLine("Loaded configuration:");
            Console.WriteLine($"  {env_apiKey}={appConfiguration.GetValue<string>(env_apiKey)}");
            Console.WriteLine($"  {env_email}={appConfiguration.GetValue<string>(env_email)}");
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
                    });

                    services.AddHttpClient<IClient, RestClient>(config =>
                    {
                        config.BaseAddress = new Uri(hostContext.Configuration.GetValue<string>(base_address));
                        config.DefaultRequestHeaders.Add("User-Agent", hostContext.Configuration.GetValue<string>(user_agent));
                        config.DefaultRequestHeaders.Add("Accept", "*/*");
                    })
                    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                        TimeSpan.FromMinutes(5), // polly wait max 5 minutes for response
                        Polly.Timeout.TimeoutStrategy.Optimistic))
                    .AddTransientHttpErrorPolicy(pb =>

                        pb.RetryAsync(60, async (response, attempt) =>
                        {
                            int delay = 0;
                            if (response.Result is null) // network errors
                            {
                                delay = 10;
                            }
                            else // server errors (500)
                            {
                                try
                                {
                                    var content = await response.Result.Content.ReadAsStringAsync();
                                    delay = JsonConvert.DeserializeAnonymousType(content, new { NextRequestInSec = 0 })
                                        .NextRequestInSec;
                                }
                                catch
                                {
                                    delay = 60;
                                }
                            }
                            await Task.Delay(TimeSpan.FromSeconds(delay));
                        })
                    );

                    services.AddScoped<OcrFlow>();
                    services.AddHostedService<Worker>();
                }).UseConsoleLifetime();

            #endregion configuration

            var host = builder.Build();
            host.Run();
        }
    }
}