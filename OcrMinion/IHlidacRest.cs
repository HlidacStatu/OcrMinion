using System.Threading.Tasks;

namespace OcrMinion
{
    internal interface IHlidacRest
    {
        Task<HlidacTask> GetTaskAsync();

        Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId);

        Task SendResultAsync(string taskId, HlidacDocument document);
    }
}