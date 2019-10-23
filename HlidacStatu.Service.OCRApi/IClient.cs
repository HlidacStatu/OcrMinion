using System.Threading.Tasks;

namespace HlidacStatu.Service.OCRApi
{
    public interface IClient
    {
        Task<OCRTask> GetTaskAsync();

        Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId);

        Task SendResultAsync(string taskId, Document document);
    }
}