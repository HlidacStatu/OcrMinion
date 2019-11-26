using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HlidacStatu.OcrMinion
{
    internal class Worker : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IServiceProvider _services;

        public Worker(
            ILogger<Worker> logger,
            IHostApplicationLifetime appLifetime,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _appLifetime = appLifetime;
            _services = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            await DoWork(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            int taskCounter = 0;
            _logger.LogInformation("OCR minion initialized. Hold on to your hat...");
            while (!cancellationToken.IsCancellationRequested)
            {
                // for every task we create a clean scope
                using (var serviceScope = _services.CreateScope())
                {
                    var services = serviceScope.ServiceProvider;
                    try
                    {
                        _logger.LogInformation($"Starting OCR process of #{++taskCounter}. task.");
                        OcrFlow flow = services.GetRequiredService<OcrFlow>();
                        await flow.RunFlow(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Guess what? Something went wrong and we don't know what.");
                        // todo: send this error message to a server
                        return;
                    }
                }
            }
        }

        private void OnStopping()
        {
            _logger.LogInformation("Starting graceful termination.");

            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            _logger.LogInformation("Gracefully terminated.");

            // Perform post-stopped activities here
        }
    }
}