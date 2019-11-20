using HlidacStatu.Service.OCRApi;
using Microsoft.Extensions.Logging;
using RunProcessAsTask;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.OcrMinion
{
    class OcrFlow
    {
        ILogger _logger;
        IClient _hlidacRest;
        public OcrFlow(ILogger<OcrFlow> logger, IClient client)
        {
            _logger = logger;
            _hlidacRest = client;
        }

        public async Task RunFlow()
        {

            try
            {
                OCRTask currentTask = await _hlidacRest.GetTaskAsync();

                await GetImageAsync(currentTask);

                await RunTesseractAsync(currentTask);

                Cleanup(currentTask);

            }
            catch (ServerHasNoTasksException ex)
            {
                _logger.LogDebug($"{ex.Message} Next try in {ex.DelayInSec.ToString()} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(ex.DelayInSec));
                return;
            }
            catch (BlockedByServerException ex)
            {
                _logger.LogWarning($"{ex.Message} Next try in {ex.DelayInSec.ToString()} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(ex.DelayInSec));
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{ex.Message}.");
                await Task.Delay(TimeSpan.FromSeconds(5 * 60)); // lets wait for a while
                Console.WriteLine(ex.Message);
            }
            
        }

        /// <summary>
        /// Downloads file and stores it on disk.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private async Task GetImageAsync(OCRTask task)
        {
            if (task != null && !string.IsNullOrWhiteSpace(task.TaskId))
            {
                var downloadStream = await _hlidacRest.GetFileToAnalyzeAsync(task.TaskId);
                using (var fileStream = new FileStream(task.InternalFileName, FileMode.Create, FileAccess.Write))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }
                _logger.LogInformation($"Image successfully downloaded.");
            }
            else
                throw new Exception("Impossible happened - task is null.");
        }

        private void Cleanup(OCRTask task)
        {
            try
            {
                File.Delete(task.InternalFileName);
                File.Delete(task.InternalFileName + ".txt");
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogDebug(ex, $"Error when deleting file [{task.InternalFileName}]. File doesn't exist.");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Error when deleting file [{task.InternalFileName}].");
            }
        }

        private async Task RunTesseractAsync(OCRTask task)
        {
            // to run OCR asynchronously I am using this library: https://github.com/jamesmanning/RunProcessAsTask
            DateTime taskStart = DateTime.Now;
            _logger.LogInformation($"Tesseract started");
            Task<ProcessResults> tesseractTask;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // this part is here only for debugging purposes
                string tesseractArgs = $"{task.InternalFileName} {task.InternalFileName} -l ces --psm 1 --dpi 300";
                tesseractTask = ProcessEx.RunAsync("tesseract.exe", tesseractArgs);
            }
            else
            {
                string tesseractArgs = $"{task.InternalFileName} {task.InternalFileName} -l ces --psm 1 --dpi 300".Replace("\"", "\\\"");
                tesseractTask = ProcessEx.RunAsync("tesseract", tesseractArgs);
            }

            var tesseractResult = await tesseractTask;

            DateTime taskEnd = DateTime.Now;
            string taskRunTime = tesseractResult.RunTime.TotalSeconds.ToString();
            if (tesseractResult.ExitCode == 0)
            {
                _logger.LogInformation($"Tesseract successfully finished. It took {taskRunTime}");
                string text = await File.ReadAllTextAsync($"{task.InternalFileName}.txt", Encoding.UTF8);

                Document document = new Document(task.TaskId,
                    taskStart, taskEnd, task.OrigFileName, text,
                    taskRunTime);

                //send text
                await _hlidacRest.SendResultAsync(task.TaskId, document);

            }
            else
            {
                _logger.LogWarning($"Tesseract failed. Sending report to the server...");
                string tesseractError = string.Join('\n', tesseractResult.StandardError);
                _logger.LogWarning(tesseractError);
                Document document = new Document(task.TaskId,
                    taskStart, taskEnd, task.OrigFileName, null,
                    taskRunTime, tesseractError);

                //send error
                await _hlidacRest.SendResultAsync(task.TaskId, document);
            }
        }
        
    }
}
