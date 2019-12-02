using HlidacStatu.Service.OCRApi;
using Microsoft.Extensions.Logging;
using RunProcessAsTask;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HlidacStatu.OcrMinion
{
    public class OcrFlow
    {
        private readonly ILogger _logger;
        private readonly IClient _hlidacRest;
        private OCRTask _currentTask;

        public OcrFlow(ILogger<OcrFlow> logger, IClient client)
        {
            _logger = logger;
            _hlidacRest = client;
        }

        public async Task RunFlow(CancellationToken cancellationToken)
        {
            try
            {
                _currentTask = await _hlidacRest.GetTaskAsync(cancellationToken);

                await GetImageAsync(cancellationToken);

                await RunTesseractAsync(cancellationToken);
            }
            catch (DelayedException ex)
            {
                if (ex is ServerHasNoTasksException)
                {
                    _logger.LogDebug($"{ex.Message} Next try in {ex.DelayInSec.ToString()} seconds.");
                }
                else
                {
                    _logger.LogWarning($"{ex.Message} Next try in {ex.DelayInSec.ToString()} seconds.");
                }
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(ex.DelayInSec), cancellationToken);
                }
                catch
                {
                    // cancellation token called
                }
                
                return;
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Application was canceled.");
                if (_currentTask != null && !string.IsNullOrWhiteSpace(_currentTask.TaskId))
                {
                    await _hlidacRest.CancelTaskAsync(_currentTask.TaskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{ex.Message}.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * 60), cancellationToken); // lets wait for a while
                }
                catch
                {
                    // cancellation token was called
                }
            }
            finally
            {
             
                Cleanup();
            }
        }

        /// <summary>
        /// Downloads file and stores it on disk.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private async Task GetImageAsync(CancellationToken cancellationToken)
        {
            if (_currentTask != null && !string.IsNullOrWhiteSpace(_currentTask.TaskId))
            {
                var downloadStream = await _hlidacRest.GetFileToAnalyzeAsync(_currentTask.TaskId, cancellationToken);
                using (var fileStream = new FileStream(_currentTask.InternalFileName, FileMode.Create, FileAccess.Write))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }
                _logger.LogInformation($"Image successfully downloaded.");
            }
            else
                throw new Exception("Impossible happened - task is null.");
        }

        private void Cleanup()
        {
            if (_currentTask != null)
            {
                try
                {
                    File.Delete(_currentTask.InternalFileName);
                    File.Delete(_currentTask.InternalFileName + ".txt");
                }
                catch (FileNotFoundException ex)
                {
                    _logger.LogDebug(ex, $"Error when deleting file [{_currentTask.InternalFileName}]. File doesn't exist.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Error when deleting file [{_currentTask.InternalFileName}].");
                }
            }
        }

        private async Task RunTesseractAsync(CancellationToken cancellationToken)
        {
            // to run OCR asynchronously I am using this library: https://github.com/jamesmanning/RunProcessAsTask
            DateTime taskStart = DateTime.Now;
            _logger.LogInformation($"Tesseract started");

            var processInfo = new ProcessStartInfo(
                "tesseract",
                $"{_currentTask.InternalFileName} {_currentTask.InternalFileName} -l ces --psm 1 --dpi 300");
            Task<ProcessResults> tesseractTask = ProcessEx.RunAsync(processInfo, cancellationToken);

            var tesseractResult = await tesseractTask;

            DateTime taskEnd = DateTime.Now;
            string taskRunTime = tesseractResult.RunTime.TotalSeconds.ToString();
            if (tesseractResult.ExitCode == 0)
            {
                _logger.LogInformation($"Tesseract successfully finished. It took {taskRunTime} s.");
                string text = await File.ReadAllTextAsync($"{_currentTask.InternalFileName}.txt", Encoding.UTF8, cancellationToken);

                Document document = new Document(_currentTask.TaskId,
                    taskStart, taskEnd, _currentTask.OrigFileName, text,
                    taskRunTime);

                //send text
                await _hlidacRest.SendResultAsync(_currentTask.TaskId, document, cancellationToken);
            }
            else
            {
                _logger.LogWarning($"Tesseract failed. Sending report to the server...");
                string tesseractError = string.Join('\n', tesseractResult.StandardError);
                _logger.LogWarning(tesseractError);
                Document document = new Document(_currentTask.TaskId,
                    taskStart, taskEnd, _currentTask.OrigFileName, null,
                    taskRunTime, tesseractError);

                //send error
                await _hlidacRest.SendResultAsync(_currentTask.TaskId, document, cancellationToken);
            }
        }

    }
}