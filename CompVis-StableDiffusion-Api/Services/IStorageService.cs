using CompVis_StableDiffusion_Api.Dto;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface IStorageService
    {
        Task<DiffusionDocument> CreateAsync(string clientId, string documentId, DiffusionRequest request, Attachment initImage);
        Task<DiffusionDocument> StartAsync(string documentId, string jobId);
        Task<DiffusionDocument> EndAsync(string documentId, string jobId, string error = null);
        Task<DiffusionDocument> CancelAsync(string documentId);
        Task<DiffusionDocument> AttachAsync(string documentId, Attachment[] attachments);
        Task<DiffusionDocument> GetDocumentAsync(string documentId, bool includeImageContent);
        Task<List<DiffusionDocument>> QueryDocumentsAsync(string clientId, bool includeImageContent,
            int? statusGreaterThan = null, int? statusLowerThan = null,
            int hoursFromNow = 24);
        Task<bool> IsDuplicatedAsync(string clientId, DiffusionRequest request, string initImageName);
    }
}
