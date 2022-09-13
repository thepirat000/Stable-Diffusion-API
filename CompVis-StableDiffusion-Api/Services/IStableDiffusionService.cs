using CompVis_StableDiffusion_Api.Dto;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface IStableDiffusionService
    {
        Task<DiffusionResponse> EnqueueJobAsync(string clientId, DiffusionRequest request, IFormFile initImageFormFile, int? strength);
        Task<DiffusionDocument> GetDocumentAsync(string clientId, string documentId, bool includeImageContent);
        Task<bool> CancelJobAsync(string clientId, string documentId);
        Task<List<DiffusionDocument>> QueryDocumentsAsync(string clientId, bool includeImageContent,
            int? statusGreaterThan = null, int? statusLowerThan = null,
            int hoursFromNow = 24);
    }
}
