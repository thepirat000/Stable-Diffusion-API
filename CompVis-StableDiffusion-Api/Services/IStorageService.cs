using CompVis_StableDiffusion_Api.Dto;
using System.IO;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface IStorageService
    {
        Task InsertAsync(TextToImageDocument document);
        Task UpdateStatusAsync(string documentId, int status, string error = null);
        Task AttachAsync(string documentId, Attachment[] attachments);
        Task<TextToImageDocument> GetAsync(string documentId);
    }
}
