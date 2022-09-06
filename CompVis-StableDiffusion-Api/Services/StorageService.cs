using CompVis_StableDiffusion_Api.Dto;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using System;
using System.Collections.Generic;
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

        public async Task<TextToImageDocument> CreateAsync(string clientId, TextToImageRequest request)
        {
            var now = DateTimeOffset.Now;
            var document = new TextToImageDocument()
            {
                ClientId = clientId,
                Request = request,
                Status = 0,
                CreatedDate = now,
                ModifiedDate = now
            };
            using (var session = _documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(document, document.JobId);
                await session.SaveChangesAsync();
            }
            
            return document;
        }

        public async Task<TextToImageDocument> StartAsync(string documentId, string jobId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                doc.Status = 1;
                doc.Error = null;
                doc.ModifiedDate = DateTimeOffset.Now;
                doc.JobId = jobId;
                await session.SaveChangesAsync();
                return doc;
            }
        }

        public async Task<TextToImageDocument> EndAsync(string documentId, string error = null)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                doc.Status = error == null ? 100 : -1;
                doc.Error = error;
                doc.ModifiedDate = DateTimeOffset.Now;
                await session.SaveChangesAsync();
                return doc;
            }
        }

        public async Task<TextToImageDocument> CancelAsync(string documentId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                if (doc.Status >= 0 && doc.Status < 100)
                {
                    doc.Status = -2;
                    doc.Error = "Cancelled by user";
                    doc.ModifiedDate = DateTimeOffset.Now;
                    await session.SaveChangesAsync();
                }
                return doc;
            }
        }

        public async Task<TextToImageDocument> AttachAsync(string documentId, Attachment[] attachments)
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
                doc.ModifiedDate = DateTimeOffset.Now;
                await session.SaveChangesAsync();
                return doc;
            }
        }

        public async Task<TextToImageDocument> GetDocumentAsync(string documentId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<TextToImageDocument>(documentId);
                return doc;
            }
        }

        public async Task<List<TextToImageDocument>> GetDocumentsForClientAsync(string clientId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                return await session.Query<TextToImageDocument>()
                    .Where(d => d.ClientId == clientId && d.CreatedDate > DateTime.UtcNow.AddHours(-24))
                    .ToListAsync();
            }
        }

        public async Task<bool> IsDuplicatedAsync(string clientId, TextToImageRequest request)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                return await session.Query<TextToImageDocument>()
                    .Where(d => d.ClientId == clientId 
                        && d.Request.Prompt == request.Prompt 
                        && d.Status >= 0
                        && d.CreatedDate > DateTimeOffset.Now.AddHours(-24)
                        && d.Request.Steps == request.Steps
                        && d.Request.Version == request.Version
                        && d.Request.Steps == request.Steps)
                    .AnyAsync();
            }
        }
    }
}
