using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.Service.OCRApi
{
    public class RestClient : IClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _email;
        private readonly bool _isDemo;
        private readonly ILogger _logger;

        public RestClient(HttpClient client, IOptionsMonitor<ClientOptions> options, ILogger<RestClient> logger)
        {
            _httpClient = client;
            _apiKey = options.CurrentValue.ApiKey;
            _email = options.CurrentValue.Email;
            _isDemo = options.CurrentValue.Demo;
            _logger = logger;
        }

        /*
            1) Získání tasku
            GET https://ocr.hlidacstatu.cz/gettask.ashx?apikey=APIKEY&server=DockerXYZ&minPriority=0&maxPriority=99&type=image
            pokud pridate &demo=1, pak to vrati testovaci task(ten ma nulove GUID). pri demo = 1 muze byt APIKEY cokoliv to vrati
            {"TaskId":"00000000-0000-0000-0000-000000000000","Priority":5,"Intensity":0,"OrigFilename":"testfile.jpg","localTempFile":null}
            anebo to nevrati nic.Pak zadny task ve fronte neni.
            server= DockerXYZ je jmeno serveru, ktery o task zada (rekneme jmeno Docker stroje nebo neco takoveho)
        */

        public async Task<OCRTask> GetTaskAsync()
        {
            string demoParam = (_isDemo) ? "&demo=1" : "";
            var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/gettask.ashx?apikey={_apiKey}&server={_email}&minPriority=0&maxPriority=99&type=image{demoParam}");
            
            _logger.LogDebug($"sending request: {new Uri(_httpClient.BaseAddress, request.RequestUri)}");
            var response = await _httpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug($"Response status code: {response.StatusCode.ToString("d")}, content: {responseContent}");

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<OCRTask>(responseContent);
            }
            else
            { 
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    var result = JsonConvert.DeserializeObject<ErrorResult>(responseContent);
                    throw new ServerHasNoTasksException(result.NextRequestInSec);
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var result = JsonConvert.DeserializeObject<ErrorResult>(responseContent);
                    throw new BlockedByServerException(result.Error, result.NextRequestInSec);
                }
            }
            // todo: other errors - how to treat them???
            throw new HttpRequestException($"GetTaskAsync failed with {response.StatusCode}");
            
        }

        /* throw new HttpRequestException($"GetFileToAnalyzeAsync failed with {response.StatusCode}");
            2) Pokud je nejaky task, je nutne ziskat soubor k analyze
            GET https://ocr.hlidacstatu.cz/gettask.ashx?taskid=00000000-0000-0000-0000-000000000000
            to vrati binarku souboru(v tomto pripade vzdy JPEG). U nuloveho GUID vzdy stejny testovaci soubor.
        */

        public async Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/gettask.ashx?apikey={_apiKey}&server={_email}&taskId={taskId}");
            _logger.LogDebug($"sending request: {new Uri(_httpClient.BaseAddress, request.RequestUri)}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug($"response received successfully");
                return await response.Content.ReadAsStreamAsync();
            }
            else
            {
                _logger.LogDebug($"response error: {response.StatusCode.ToString()}");
                throw new HttpRequestException($"GetFileToAnalyzeAsync failed with {response.StatusCode}");
            }
        }

        /*
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

        public async Task SendResultAsync(string taskId, Document document)
        {
            document.Server = _email;
            if (document.Documents.Length > 0)
            {
                document.Documents[0].Server = _email;
            }
            else
            {
                _logger.LogError("Invalid document. This should never happen.");
                throw new MissingMemberException(nameof(Document), nameof(Document.Documents));
            }

            string json = JsonConvert.SerializeObject(document);

            var request = new HttpRequestMessage(HttpMethod.Post,
                    $"/donetask.ashx?apikey={_apiKey}&server={_email}&taskId={taskId}&method=done");
            request.Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            _logger.LogDebug($"sending request: {new Uri(_httpClient.BaseAddress, request.RequestUri)}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"received response: {result}");
            }
            else
            {
                _logger.LogDebug($"response error: {response.StatusCode.ToString()}");
                throw new HttpRequestException($"GetFileToAnalyzeAsync failed with {response.StatusCode}");
            }
        }
    }
}