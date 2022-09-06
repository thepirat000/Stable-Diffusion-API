using CompVis_StableDiffusion_Api.Dto;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public class TextToImageService : ITextToImageService
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IStorageService _storageService;
        private readonly IShellService _shellService;
        private readonly ILogService _log;
        private readonly Settings _settings;
        
        public TextToImageService(IBackgroundJobClient backgroundJobClient, IStorageService storageService, IShellService shellService, ILogService logService, Settings settings)
        {
            _backgroundJobClient = backgroundJobClient;
            _storageService = storageService;
            _shellService = shellService;
            _log = logService;
            _settings = settings;
        }

        public async Task<TextToImageResponse> EnqueueJobAsync(string clientId, TextToImageRequest request)
        {
            // Validate duplications
            if (await _storageService.IsDuplicatedAsync(clientId, request))
            {
                return new TextToImageResponse() { BadRequestError = "Duplicated job" };
            }

            // Create the document on DB
            _log.EphemeralLog($"Storing job. Client {clientId} for '{request.Prompt}'");
            var document = await _storageService.CreateAsync(clientId, request);

            // Enqueue job
            var jobId = _backgroundJobClient.Enqueue(() => ProcessAsync(document, null));
            _log.EphemeralLog($"Enqueueing job {jobId}. Client {clientId} for '{request.Prompt}'");

            // Update the document with the job ID
            await _storageService.StartAsync(document.Id, jobId);

            return new TextToImageResponse() { DocumentId = document.Id, JobId = jobId };
        }
        
        public async Task<TextToImageDocument> GetDocumentAsync(string clientId, string documentId)
        {
            var document = await _storageService.GetDocumentAsync(documentId);
            
            if (document?.ClientId != null && document.ClientId != clientId) 
            {
                throw new ArgumentException("Wrong client ID");
            }
            return document;
        }

        public async Task<List<TextToImageDocument>> GetDocumentsForClientAsync(string clientId)
        {
            return await _storageService.GetDocumentsForClientAsync(clientId);
        }
        
        public async Task<bool> CancelJobAsync(string clientId, string documentId)
        {
            var document = await _storageService.GetDocumentAsync(documentId);
            if (document != null && document.ClientId != clientId)
            {
                throw new ArgumentException("Wrong client ID");
            }
            
            var jobId = document.JobId;
            bool deleted = false;
            if (jobId != null)
            {
                deleted = _backgroundJobClient.Delete(jobId);
                if (document != null)
                {
                    await _storageService.CancelAsync(documentId);
                }
            }
            
            return deleted;
        }

        // Main Job Process method
        [JobDisplayName("Txt2Img Job")]
        public async Task ProcessAsync(TextToImageDocument document, PerformContext context)
        {
            var request = document.Request;
            var clientId = document.ClientId;
            var documentId = document.Id;
            var jobId = context.BackgroundJob.Id;
            var now = DateTimeOffset.Now;

            // Start the job
            _log.EphemeralLog($"Starting job {jobId}. Document {documentId}. Client {clientId} for '{request.Prompt}'");

            string error = null;
            string[] files = null;
            ExecuteResult execResult = null;
            try
            {
                // Process
                execResult = await ExecuteCondaScriptAsync(request, jobId);
            }
            catch (Exception ex)
            {
                _log.EphemeralLog($"ERROR: {ex}", true);
                error = ex.ToString();
            }

            if (error == null && execResult.ExitCode != 0)
            {
                _log.EphemeralLog($"ERROR: ExitCode {execResult.ExitCode}. {execResult.StdError}", true);
                error = execResult.StdError;
            }
            
            if (error == null)
            {
                files = GetOutputFiles(jobId);
                if (files.Length == 0)
                {
                    error = "No files found";
                }
                else
                {
                    // Attach files
                    _log.EphemeralLog($"Attaching {files.Length} files to job {jobId} document {documentId} for '{request.Prompt}'");
                    await _storageService.AttachAsync(documentId, files.Select(f => new Attachment(f)).ToArray());
                }
            }

            // Update status to Completed or Error
            await _storageService.EndAsync(documentId, error);
        }

        private async Task<ExecuteResult> ExecuteCondaScriptAsync(TextToImageRequest request, string jobId)
        {
            var prompt = request.Prompt = request.Prompt.Replace("\\", "").Replace("\"", "");
            var seed = request.Seed <= 0 ? new Random().Next() : request.Seed;
            var workingDir = _settings.WorkingDir;
            var outputDir = Path.Combine(_settings.OutputDir, jobId);
            var txt2imgCommand = $"python scripts/txt2img.py --prompt \"{prompt}\" --plms --ckpt sd-v{request.Version}.ckpt --skip_grid --n_samples 1 --n_iter {request.Samples} --ddim_steps {request.Steps} --seed {seed} --outdir {outputDir}";

            var commands = new string[]
            {
                @"C:\tools\miniconda3\Scripts\activate.bat ldm",
                @$"cd ""{workingDir}""",
                txt2imgCommand,
                "conda deactivate"
            };

            var sbStdErr = new StringBuilder();
            var sbStdOut = new StringBuilder();
            var result = await _shellService.ExecuteWithTimeoutAsync(
                commands,
                workingDir,
                14,
                e =>
                {
                    sbStdErr.AppendLine(e);
                    _log.EphemeralLog("STDERR: " + e);
                },
                o =>
                {
                    sbStdOut.AppendLine(o);
                    _log.EphemeralLog("STDOUT: " + o);
                });
            
            return result;
        }

        private string[] GetOutputFiles(string jobId)
        {
            var path = Path.Combine(_settings.OutputDir, jobId, "samples");
            return Directory.GetFiles(path, "*.png");
        }
    }
}
