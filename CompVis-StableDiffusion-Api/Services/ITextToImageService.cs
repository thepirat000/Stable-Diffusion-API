using CompVis_StableDiffusion_Api.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface ITextToImageService
    {
        Task<TextToImageResponse> EnqueueJobAsync(string clientId, TextToImageRequest request);
        Task<TextToImageDocument> GetDocumentAsync(string clientId, string documentId);
        Task<bool> CancelJobAsync(string clientId, string documentId);
        Task<List<TextToImageDocument>> GetDocumentsForClientAsync(string clientId);
    }
}
