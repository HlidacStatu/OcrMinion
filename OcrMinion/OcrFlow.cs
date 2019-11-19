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
            this._logger = logger;
            this._hlidacRest = client;
        }

        public async Task RunFlow()
        {

            try
            {
                OCRTask currentTask = await _hlidacRest.GetTaskAsync();
            }
            catch (ServerHasNoTasksException ex)
            {
                _logger.LogDebug($"{ex.Message} Waiting for {ex.DelayInSec.ToString()} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(ex.DelayInSec));
                return;
            }
            catch (BlockedByServerException ex)
            {
                _logger.LogWarning($"{ex.Message} Waiting for {ex.DelayInSec.ToString()} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(ex.DelayInSec));
                return;
            }
            catch (Exception ex)
            {

            }

            if(string.IsNullOrWhiteSpace(currentTask.TaskId))
            {
                string returnedTask = Newtonsoft.Json.JsonConvert.SerializeObject(currentTask);
                _logger.LogWarning($"Returned task is invalid. \n{returnedTask}");
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
                

            // run OCR and wait for its end
            // to run OCR asynchronously I am using this library: https://github.com/jamesmanning/RunProcessAsTask
            DateTime taskStart = DateTime.Now;
            _logger.LogInformation($"Tesseract started");
            Task <ProcessResults> tesseractTask;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // this part is here only for debugging purposes
                string tesseractArgs = $"{currentTask.InternalFileName} {currentTask.InternalFileName} -l ces --psm 1 --dpi 300";
                tesseractTask = ProcessEx.RunAsync("tesseract.exe", tesseractArgs);
            }
            else
            {
                string tesseractArgs = $"{currentTask.InternalFileName} {currentTask.InternalFileName} -l ces --psm 1 --dpi 300".Replace("\"", "\\\"");
                tesseractTask = ProcessEx.RunAsync("tesseract", tesseractArgs);
            }

            var tesseractResult = await tesseractTask;

            DateTime taskEnd = DateTime.Now;
            string taskRunTime = tesseractResult.RunTime.TotalSeconds.ToString();
            if (tesseractResult.ExitCode == 0)
            {
                _logger.LogInformation($"Tesseract successfully finished. It took {taskRunTime}");
                string text = await File.ReadAllTextAsync($"{currentTask.InternalFileName}.txt", Encoding.UTF8);

                Document document = new Document(currentTask.TaskId,
                    taskStart, taskEnd, currentTask.OrigFileName, text,
                    taskRunTime);

                //send text
                await _hlidacRest.SendResultAsync(currentTask.TaskId, document);

            }
            else
            {
                _logger.LogWarning($"Tesseract failed. Sending report to the server...");
                string tesseractError = string.Join('\n', tesseractResult.StandardError);
                _logger.LogWarning(tesseractError);
                Document document = new Document(currentTask.TaskId,
                    taskStart, taskEnd, currentTask.OrigFileName, null,
                    taskRunTime, tesseractError);
                
                //send error
                await _hlidacRest.SendResultAsync(currentTask.TaskId, document); 
            }

            try
            {
                File.Delete(currentTask.InternalFileName);
                File.Delete(currentTask.InternalFileName + ".txt");
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogDebug(ex, $"Error when deleting file [{currentTask.InternalFileName}]. File doesn't exist.");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Error when deleting file [{currentTask.InternalFileName}].");
            }

        }

        private async Task<OCRTask> GetNewImage()
        {
            _logger.LogDebug("Getting new image.");
            // try to get task until we get some :)
            while (true)
            {
                OCRTask task = await _hlidacRest.GetTaskAsync();

                if (task != null && !string.IsNullOrWhiteSpace(task.TaskId))
                {
                    var downloadStream = await _hlidacRest.GetFileToAnalyzeAsync(task.TaskId);
                    using (var fileStream = new FileStream(task.InternalFileName, FileMode.Create, FileAccess.Write))
                    {
                        await downloadStream.CopyToAsync(fileStream);
                    }
                    _logger.LogDebug($"Image for task[{task.TaskId}] successfully downloaded.");
                    return task;
                }
                else
                {
                    string returnedTask = Newtonsoft.Json.JsonConvert.SerializeObject(task);
                    _logger.LogWarning($"Returned task is invalid. \n{returnedTask}");

                    // invalid task is probably because there were no tasks to process on server side, we need to wait some time
                    // todo - this can be done in polly probably
                    await Task.Delay(TimeSpan.FromSeconds(20));
                }
            }
        }
    }
}
