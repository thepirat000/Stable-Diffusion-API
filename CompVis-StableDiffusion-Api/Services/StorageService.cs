using CompVis_StableDiffusion_Api.Dto;
using Raven.Client.Documents;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    /// <summary>
    /// Storage service using Raven DB
    /// </summary>
    public class StorageService : IStorageService
    {
        private readonly IDocumentStore _documentStore;
        
        public StorageService(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public async Task InsertAsync(TextToImageDocument document)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(document, document.JobId);
                await session.SaveChangesAsync();
            }
        }

        public async Task UpdateStatusAsync(string documentId, int status, string error = null)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                doc.Status = status;
                doc.Error = error;
                await session.SaveChangesAsync();
            }
        }

        public async Task AttachAsync(string documentId, Attachment[] attachments)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                for (int i = 0; i < attachments.Length; i++)
                {
                    var attachment = attachments[i];
                    session.Advanced.Attachments.Store(documentId, attachment.Filename ?? (i.ToString("00000") + ".jpg"), attachment.Stream, attachment.MimeType ?? "image/jpg");
                }
                doc.FileRefs = attachments.Select(a => a.Filepath).ToList();
                await session.SaveChangesAsync();
            }
        }

        public async Task<TextToImageDocument> GetAsync(string documentId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                return doc;
            }
        }

    }
}
