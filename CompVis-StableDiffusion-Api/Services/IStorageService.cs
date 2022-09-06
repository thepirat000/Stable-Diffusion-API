using CompVis_StableDiffusion_Api.Dto;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface IStorageService
    {
        Task<TextToImageDocument> CreateAsync(string clientId, TextToImageRequest request);
        Task<TextToImageDocument> StartAsync(string documentId, string jobId);
        Task<TextToImageDocument> EndAsync(string documentId, string error = null);
        Task<TextToImageDocument> CancelAsync(string documentId);
        Task<TextToImageDocument> AttachAsync(string documentId, Attachment[] attachments);
        Task<TextToImageDocument> GetDocumentAsync(string documentId);
        Task<List<TextToImageDocument>> GetDocumentsForClientAsync(string clientId);
        Task<bool> IsDuplicatedAsync(string clientId, TextToImageRequest request);
    }
}
