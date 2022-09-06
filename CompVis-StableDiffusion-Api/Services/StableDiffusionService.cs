using CompVis_StableDiffusion_Api.Dto;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public class StableDiffusionService : IStableDiffusionService
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IStorageService _storageService;
        private readonly IShellService _shellService;
        private readonly ILogService _log;
        private readonly Settings _settings;
        
        public StableDiffusionService(IBackgroundJobClient backgroundJobClient, IStorageService storageService, IShellService shellService, ILogService logService, Settings settings)
        {
            _backgroundJobClient = backgroundJobClient;
            _storageService = storageService;
            _shellService = shellService;
            _log = logService;
            _settings = settings;
        }

        public async Task<DiffusionResponse> EnqueueJobAsync(string clientId, DiffusionRequest request, IFormFile initImageFormFile, int? strength)
        {
            // Validate duplications
            if (await _storageService.IsDuplicatedAsync(clientId, request, initImageFormFile?.FileName))
            {
                return new DiffusionResponse() { BadRequestError = "Duplicated job" };
            }

            // Generate a new document ID
            var documentId = Guid.NewGuid().ToString("N")[0..16];

            // Save the init image to the cache
            Attachment initImage = null;
            if (initImageFormFile != null)
            {
                var initImageFilePath = Path.Combine(_settings.CacheDir, documentId, "input", initImageFormFile.FileName);
                initImage = Attachment.CreateFromStream(initImageFormFile.OpenReadStream(), initImageFilePath);
            }

            // Create the document on DB
            _log.EphemeralLog($"Storing job. Client {clientId} for '{request.Prompt}'");
            var document = await _storageService.CreateAsync(clientId, documentId, request, initImage);
            
            // -- Enqueue job --
            string jobId;
            if (initImage != null)
            {
                jobId = _backgroundJobClient.Enqueue(() => ProcessImageToImageAsync(clientId, documentId, request, initImage.FilePath, strength, null));
                _log.EphemeralLog($"Enqueueing Img2Img job {jobId}. Client {clientId} for image '{initImage.FilePath}' prompt '{request.Prompt}'");
            }
            else
            {
                jobId = _backgroundJobClient.Enqueue(() => ProcessTextToImageAsync(clientId, documentId, request, null));
                _log.EphemeralLog($"Enqueueing Txt2Img job {jobId}. Client {clientId} for '{request.Prompt}'");
            }
            

            // Update the document with the job ID
            await _storageService.StartAsync(document.Id, jobId);

            return new DiffusionResponse() { DocumentId = document.Id, JobId = jobId };
        }
        
        public async Task<DiffusionDocument> GetDocumentAsync(string clientId, string documentId)
        {
            var document = await _storageService.GetDocumentAsync(documentId);
            
            if (document?.ClientId != null && document.ClientId != clientId) 
            {
                throw new ArgumentException("Wrong client ID");
            }
            return document;
        }

        public async Task<List<DiffusionDocument>> GetDocumentsForClientAsync(string clientId)
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

        // Main Job Process method for Img2Img
        [JobDisplayName("Image to Image Job")]
        public async Task ProcessImageToImageAsync(string clientId, string documentId, DiffusionRequest request, string initImageFilePath, int? strength, PerformContext context)
        {
            await ProcessImplAsync(clientId, documentId, context.BackgroundJob.Id, request, initImageFilePath, strength);
        }

        // Main Job Process method for Txt2Img
        [JobDisplayName("Text to Image Job")]
        public async Task ProcessTextToImageAsync(string clientId, string documentId, DiffusionRequest request, PerformContext context)
        {
            await ProcessImplAsync(clientId, documentId, context.BackgroundJob.Id, request, null, null);
        }

        private async Task ProcessImplAsync(string clientId, string documentId, string jobId, DiffusionRequest request, string initImageFilePath, int? strength)
        { 
            var now = DateTimeOffset.Now;

            // Start the job
            _log.EphemeralLog($"Starting job {jobId}. Document {documentId}. Client {clientId} for '{request.Prompt}'");

            string error = null;
            string[] outputFiles = null;
            ExecuteResult execResult = null;
            try
            {
                // Process
                execResult = await ExecuteCondaScriptAsync(documentId, request, initImageFilePath, strength);
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
                outputFiles = GetOutputFiles(documentId);
                if (outputFiles.Length == 0)
                {
                    error = "No files found";
                }
                else
                {
                    // Attach files
                    _log.EphemeralLog($"Attaching {outputFiles.Length} files to job {jobId} document {documentId} for '{request.Prompt}'");
                    await _storageService.AttachAsync(documentId, outputFiles.Select(f => new Attachment(f)).ToArray());
                }
            }

            // Update status to Completed or Error
            await _storageService.EndAsync(documentId, jobId, error);
        }

        private async Task<ExecuteResult> ExecuteCondaScriptAsync(string documentId, DiffusionRequest request, string initImageFilePath, int? strength)
        {
            var prompt = request.Prompt = request.Prompt.Replace("\\", "").Replace("\"", "");
            var seed = request.Seed <= 0 ? new Random().Next() : request.Seed;
            var workingDir = _settings.WorkingDir;
            var outputDir = Path.Combine(_settings.CacheDir, documentId);
            
            string pythonCommand;
            if (initImageFilePath != null)
            {
                // img2img
                double strengthDbl = (double)strength.GetValueOrDefault(80) / 100;
                pythonCommand = $"python scripts/img2img.py --prompt \"{prompt}\" --ckpt sd-v{request.Version}.ckpt --skip_grid --n_samples 1 --n_iter {request.Samples} --ddim_steps {request.Steps} --seed {seed} --init-img \"{initImageFilePath}\" --strength {strengthDbl} --outdir {outputDir}";
            }
            else
            {
                // txt2img
                pythonCommand = $"python scripts/txt2img.py --prompt \"{prompt}\" --plms --ckpt sd-v{request.Version}.ckpt --skip_grid --n_samples 1 --n_iter {request.Samples} --ddim_steps {request.Steps} --seed {seed} --outdir {outputDir}";
            }

            var commands = new string[]
            {
                @"C:\tools\miniconda3\Scripts\activate.bat ldm",
                @$"cd ""{workingDir}""",
                pythonCommand,
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

        private string[] GetOutputFiles(string documentId)
        {
            var path = Path.Combine(_settings.CacheDir, documentId, "samples");
            return Directory.GetFiles(path, "*.png");
        }
    }
}
