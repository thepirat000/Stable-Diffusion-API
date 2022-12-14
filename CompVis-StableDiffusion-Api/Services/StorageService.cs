using CompVis_StableDiffusion_Api.Dto;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents.Operations.Attachments;
using Raven.Client.Documents.Session;

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

        public async Task<DiffusionDocument> CreateAsync(string clientId, string documentId, DiffusionRequest request, Attachment initImage)
        {
            var now = DateTimeOffset.Now;
            var document = new DiffusionDocument()
            {
                Id = documentId,
                ClientId = clientId,
                Request = request,
                Status = 0,
                InitImageName = initImage?.FileName,
                CreatedDate = now,
                ModifiedDate = now
            };
            using (var session = _documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(document);
                if (initImage != null)
                {
                    session.Advanced.Attachments.Store(documentId, initImage.FileName, initImage.Stream, initImage.MimeType);
                    initImage.Stream.Seek(0L, SeekOrigin.Begin);
                }
                await session.SaveChangesAsync();
            }
            
            return document;
        }

        public async Task<DiffusionDocument> StartAsync(string documentId, string jobId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<DiffusionDocument>(documentId);
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

        public async Task<DiffusionDocument> EndAsync(string documentId, string jobId, string error = null)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<DiffusionDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                doc.Status = error == null ? 100 : -1;
                doc.Error = error;
                doc.ModifiedDate = DateTimeOffset.Now;
                doc.JobId = jobId;
                await session.SaveChangesAsync();
                return doc;
            }
        }

        public async Task<DiffusionDocument> CancelAsync(string documentId)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<DiffusionDocument>(documentId);
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

        public async Task<DiffusionDocument> AttachAsync(string documentId, Attachment[] attachments)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<DiffusionDocument>(documentId);
                if (doc == null)
                {
                    throw new ArgumentException("Document ID not found");
                }
                for (int i = 0; i < attachments.Length; i++)
                {
                    var attachment = attachments[i];
                    session.Advanced.Attachments.Store(documentId, attachment.FileName ?? (i.ToString("00000") + ".jpg"), attachment.Stream, attachment.MimeType ?? "image/jpg");
                }
                doc.FileRefs = attachments.Select(a => a.FilePath).ToList();
                doc.ModifiedDate = DateTimeOffset.Now;
                await session.SaveChangesAsync();
                return doc;
            }
        }

        public async Task<DiffusionDocument> GetDocumentAsync(string documentId, bool includeImageContent)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var doc = await session.LoadAsync<DiffusionDocument>(documentId);
                if (includeImageContent)
                {
                    await OverrideFileRefsWithImageBytesAsync(doc, session);
                }

                return doc;
            }
        }

        public async Task<List<DiffusionDocument>> QueryDocumentsAsync(string clientId, bool includeImageContent, 
            int? statusGreaterThan = null, int? statusLowerThan = null,
            int hoursFromNow = 24)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var query = session.Query<DiffusionDocument>()
                    .Where(d => d.ClientId == clientId && d.CreatedDate > DateTime.UtcNow.AddHours(-hoursFromNow));
                if (statusGreaterThan.HasValue)
                {
                    query = query.Where(d => d.Status > statusGreaterThan.Value);
                }
                if (statusLowerThan.HasValue)
                {
                    query = query.Where(d => d.Status < statusLowerThan.Value);
                }
                var documents = await query.ToListAsync();
                if (includeImageContent)
                {
                    foreach (var doc in documents)
                    {
                        await OverrideFileRefsWithImageBytesAsync(doc, session);
                    }
                }

                return documents;
            }
        }

        public async Task<bool> IsDuplicatedAsync(string clientId, DiffusionRequest request, string initImageName)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var query = session.Query<DiffusionDocument>()
                    .Where(d => d.ClientId == clientId
                                && d.Request.Prompt == request.Prompt
                                && d.Status >= 0
                                && d.Request.Steps == request.Steps
                                && d.Request.Version == request.Version
                                && d.Request.Seed == request.Seed);
                if (initImageName != null)
                {
                    query = query.Where(d => d.InitImageName != null);
                }
                return await query.AnyAsync();
            }
        }

        private async Task OverrideFileRefsWithImageBytesAsync(DiffusionDocument document, IAsyncDocumentSession session)
        {
            if (document?.FileRefs != null)
            {
                for (int i = 0; i < document.FileRefs.Count; i++)
                {
                    var filePath = document.FileRefs[i];
                    var filename = Path.GetFileName(filePath);

                    using (var attachment = await session.Advanced.Attachments.GetAsync(document.Id, filename))
                    {
                        if (attachment != null)
                        {
                            using (var ms = new MemoryStream())
                            {
                                await attachment.Stream.CopyToAsync(ms);
                                document.FileRefs[i] = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                        else
                        {
                            document.FileRefs[i] = "";
                        }
                    }
                }
            }
        }
    }
}
