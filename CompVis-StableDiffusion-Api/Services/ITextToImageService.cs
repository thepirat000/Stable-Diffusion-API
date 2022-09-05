using CompVis_StableDiffusion_Api.Dto;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface ITextToImageService
    {
        TextToImageResponse EnqueueJob(TextToImageRequest request);
        Task<TextToImageDocument> GetJobStatus(string jobId);
        Task<bool> CancelJobAsync(string jobId);
    }
}
