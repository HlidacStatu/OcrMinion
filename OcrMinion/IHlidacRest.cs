using System.Threading.Tasks;

namespace OcrMinion
{
    interface IHlidacRest
    {
        Task<HlidacTask> GetTaskAsync();
        Task<System.IO.Stream> GetFileToAnalyzeAsync(string taskId);
        Task SendResultAsync(string text);
    }
}
