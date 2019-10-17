using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace OcrMinion
{
    class HlidacRest : IHlidacRest
    {

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _server;

        public HlidacRest(HttpClient client, IOptionsMonitor<HlidacOption> options)
        {
            _httpClient = client;
            _apiKey = options.CurrentValue.ApiKey;
            _server = options.CurrentValue.Server;
        }

        /*
            1) Získání tasku
            GET https://ocr.hlidacstatu.cz/task.ashx?apikey=APIKEY&server=DockerXYZ&minPriority=0&maxPriority=99&type=image
            pokud pridate &demo=1, pak to vrati testovaci task(ten ma nulove GUID). pri demo = 1 muze byt APIKEY cokoliv to vrati
            {"TaskId":"00000000-0000-0000-0000-000000000000","Priority":5,"Intensity":0,"OrigFilename":"testfile.jpg","localTempFile":null}
            anebo to nevrati nic.Pak zadny task ve fronte neni.
            server= DockerXYZ je jmeno serveru, ktery o task zada (rekneme jmeno Docker stroje nebo neco takoveho)
        */
        public async Task<HlidacTask> GetTaskAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/task.ashx?apikey={_apiKey}&server=DockerXYZ&minPriority=0&maxPriority=99&type=image");
                        
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonResult = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<HlidacTask>(jsonResult);
            }
            else
            {
                throw new HttpRequestException($"GetTaskAsync failed with {response.StatusCode}");
            }
        }
        /* throw new HttpRequestException($"GetFileToAnalyzeAsync failed with {response.StatusCode}");
            2) Pokud je nejaky task, je nutne ziskat soubor k analyze
            GET https://ocr.hlidacstatu.cz/task.ashx?taskid=00000000-0000-0000-0000-000000000000
            to vrati binarku souboru(v tomto pripade vzdy JPEG). U nuloveho GUID vzdy stejny testovaci soubor.
        */
        public async Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/task.ashx?apikey={_apiKey}&taskId={taskId}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStreamAsync();
            }
            else
            {
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
        public async Task SendResultAsync(string text)
        {
            throw new NotImplementedException();
        }
    }
}
