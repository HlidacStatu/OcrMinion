using System.Threading;
using System.Threading.Tasks;

namespace HlidacStatu.Service.OCRApi
{
    public interface IClient
    {
        Task<OCRTask> GetTaskAsync(CancellationToken cancellationToken);

        Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId, CancellationToken cancellationToken);

        Task SendResultAsync(string taskId, Document document, CancellationToken cancellationToken);

        Task CancelTaskAsync(string taskId);

    }
}