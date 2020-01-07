using HlidacStatu.Service.OCRApi;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Tests
{
    public class RestClientTests
    {
        #region preparation
        /// <summary>
        /// Mock HttpClient where SendAsync method returns statusCode and content from parameters
        /// </summary>
        /// <param name="statusCode">HttpStatusCode which is going to be returned</param>
        /// <param name="content">Content which is going to be returned</param>
        /// <returns>HttpClientMock</returns>
        private HttpClient CreateHttpClientMock(HttpStatusCode statusCode, string content)
        {
            var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            httpHandlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = statusCode,
                   Content = new StringContent(content),
               })
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(httpHandlerMock.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TestUserAgent");

            return httpClient;
        }

        private readonly string _apiKey = "thisIsSecretApiKey";
        private readonly string _email = "thisIs@email.com";

        private IOptionsMonitor<ClientOptions> CreateIOptionsMonitorMock()
        {
            var iom = new Mock<IOptionsMonitor<ClientOptions>>();
            var co = new ClientOptions()
            {
                ApiKey = _apiKey,
                Email = _email
            };

            iom.Setup(expr => expr.CurrentValue).Returns(co);

            return iom.Object;
        }

        private ILogger<RestClient> CreateILoggerMock()
        {
            var ilogger = new Mock<ILogger<RestClient>>();
            return ilogger.Object;
        }

        #endregion

        [Fact(DisplayName ="Check if GetTaskAsync returns OCRTask")]
        public async Task GetTaskAsyncCorrectResponse()
        {
            // ARRANGE
            OCRTask taskToReturn = new OCRTask()
            {
                TaskId = "00000000-0000-0000-0000-000000000000",
                Priority = 5,
                Intensity = 0,
                OrigFileName = "testfile.jpg",
                LocalTempFile = null
            };

            // prepare what is going to be returned by httpclient
            string content = JsonConvert.SerializeObject(taskToReturn);
            var httpClient = CreateHttpClientMock(HttpStatusCode.OK, content);

            var rc = new RestClient(httpClient, CreateIOptionsMonitorMock(), CreateILoggerMock());

            // ACT
            var result = await rc.GetTaskAsync(new CancellationToken());

            // ASSERT
            result.Should().NotBeNull();
            result.TaskId.Should().Be(taskToReturn.TaskId);
            result.Priority.Should().Be(taskToReturn.Priority);
            result.Intensity.Should().Be(taskToReturn.Intensity);
            result.OrigFileName.Should().Be(taskToReturn.OrigFileName);
            result.LocalTempFile.Should().Be(taskToReturn.LocalTempFile);
            result.InternalFileName.Should().NotBe(taskToReturn.InternalFileName); // unique guid

        }

        [Fact(DisplayName = "Check if GetFileToAnalyzeAsync returns correct stream")]
        public async Task GetFileToAnalyzeAsyncCorrectResponse()
        {
            // ARRANGE
            // prepare what is going to be returned by httpclient
            string content = "1234567890+-!sOme Long Text stream representing some picture!";
            var httpClient = CreateHttpClientMock(HttpStatusCode.OK, content);

            var rc = new RestClient(httpClient, CreateIOptionsMonitorMock(), CreateILoggerMock());

            // ACT
            var stream = await rc.GetFileToAnalyzeAsync("taskid123", new CancellationToken());

            string result = "";
            using (StreamReader streamReader = new StreamReader(stream))
                result = streamReader.ReadToEnd();

            // ASSERT
            stream.Should().NotBeNull();
            result.Should().Be(content);

        }
    }
}
