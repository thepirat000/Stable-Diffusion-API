using CompVis_StableDiffusion_Api.Dto;
using Hangfire;
using Hangfire.Server;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                return new DiffusionResponse() { Error = "Duplicated job" };
            }

            // Validate limits
            var jobsProcessingCount  = (await _storageService.QueryDocumentsAsync(clientId, false, -1, 100, 24)).Count;
            if (jobsProcessingCount >= _settings.QueueLimitByUser)
            {
                return new DiffusionResponse() { Error = "Too many jobs queued" };
            }

            // Generate a new document ID
            var documentId = Guid.NewGuid().ToString("N")[0..16];

            // Save the init image to the cache
            Attachment initImage = null;
            if (initImageFormFile != null)
            {
                // Resize the init image if needed
                var initImageFilePath = await ExecuteFixInitImageScriptAsync(documentId, initImageFormFile.OpenReadStream(), Path.GetExtension(initImageFormFile.FileName));
                initImage = new Attachment(initImageFilePath);
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

            return new DiffusionResponse() { DocumentId = document.Id, JobId = jobId };
        }
        
        public async Task<DiffusionDocument> GetDocumentAsync(string clientId, string documentId, bool includeImageContent)
        {
            var document = await _storageService.GetDocumentAsync(documentId, includeImageContent);
            
            if (document?.ClientId != null && document.ClientId != clientId) 
            {
                throw new ArgumentException("Wrong client ID");
            }
            return document;
        }

        public async Task<List<DiffusionDocument>> QueryDocumentsAsync(string clientId, bool includeImageContent,
            int? statusGreaterThan = null, int? statusLowerThan = null,
            int hoursFromNow = 24)
        {
            return await _storageService.QueryDocumentsAsync(clientId, includeImageContent, statusGreaterThan, statusLowerThan, hoursFromNow);
        }
        
        public async Task<bool> CancelJobAsync(string clientId, string documentId)
        {
            var document = await _storageService.GetDocumentAsync(documentId, false);
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
        [JobDisplayName("img2img {0} {1}")]
        public async Task ProcessImageToImageAsync(string clientId, string documentId, DiffusionRequest request, string initImageFilePath, int? strength, PerformContext context)
        {
            await ProcessImplAsync(clientId, documentId, context.BackgroundJob.Id, request, initImageFilePath, strength);
        }

        // Main Job Process method for Txt2Img
        [JobDisplayName("txt2img {0} {1}")]
        public async Task ProcessTextToImageAsync(string clientId, string documentId, DiffusionRequest request, PerformContext context)
        {
            await ProcessImplAsync(clientId, documentId, context.BackgroundJob.Id, request, null, null);
        }

        private async Task ProcessImplAsync(string clientId, string documentId, string jobId, DiffusionRequest request, string initImageFilePath, int? strength)
        {
            var now = DateTimeOffset.Now;

            // Start the job
            _log.EphemeralLog($"Starting job {jobId}. Document {documentId}. Client {clientId} for '{request.Prompt}'");

            // Update the document with the job ID
            await _storageService.StartAsync(documentId, jobId);

            string error = null;
            string[] outputFiles = null;
            ExecuteResult execResult = null;
            try
            {
                // Execute Conda commands
                execResult = await ExecuteCondaScriptAsync(documentId, request, initImageFilePath, strength);
                // Fix output
                await ExecuteFixOutputImagesScriptAsync(documentId);
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
            var status = error == null ? "Successfully" : "with Errors";
            _log.EphemeralLog($"Job {jobId} Completed {status}. Document {documentId} for '{request.Prompt}' {error}");
        }

        // Executes the script to resize input image, returns the path o the image
        private async Task<string> ExecuteFixInitImageScriptAsync(string documentId, Stream initImageStream, string extension)
        {
            var inputFolder = GetInputFolder(documentId);
            Directory.CreateDirectory(inputFolder);

            var originalFilePath = Path.Combine(inputFolder, $"input{extension}");
            using (var fileStream = new FileStream(originalFilePath, FileMode.Create, FileAccess.Write))
            {
                initImageStream.CopyTo(fileStream);
            }

            var resizeFilePath = Path.Combine(inputFolder, $"input_resize.jpg");
            var commands = new string[]
            {
                @$"./FixInputImage.ps1 ""{originalFilePath}"" ""{resizeFilePath}"""
            };
            
            var result = await _shellService.ExecuteWithTimeoutAsync(commands, GetScriptsFolder(), 3, null, null, "powershell.exe");

            if (result.ExitCode != 0 || !string.IsNullOrEmpty(result.StdError))
            {
                var err = $"ERROR processing input image: ExitCode {result.ExitCode}. {result.StdError}";
                _log.EphemeralLog(err, true);
                throw new ArgumentException(err);
            }

            if (File.Exists(resizeFilePath))
            {
                return resizeFilePath;
            }
            else
            {
                return originalFilePath;
            }
        }

        private async Task ExecuteFixOutputImagesScriptAsync(string documentId)
        {
            var outDir = GetOutputFolder(documentId);
            var commands = new string[]
            {
                @$"./FixOutputImage.ps1 ""{outDir}"""
            };

            var result = await _shellService.ExecuteWithTimeoutAsync(commands, GetScriptsFolder(), 3, null, null, "powershell.exe");

            if (result.ExitCode != 0 || !string.IsNullOrEmpty(result.StdError))
            {
                var err = $"ERROR processing output images: ExitCode {result.ExitCode}. {result.StdError}";
                _log.EphemeralLog(err, true);
                throw new ArgumentException(err);
            }
        }

        private async Task<ExecuteResult> ExecuteCondaScriptAsync(string documentId, DiffusionRequest request, string initImageFilePath, int? strength)
        {
            var prompt = request.Prompt = request.Prompt.Replace("\\", "").Replace("\"", "");
            var seed = request.Seed <= 0 ? new Random().Next() : request.Seed;
            var workingDir = _settings.WorkingDir;
            var outputDir = Path.Combine(_settings.CacheDir, documentId);

            var sizeParams = request.Width.HasValue ? $" --W {request.Width}" : "";
            sizeParams += request.Height.HasValue ? $" --H {request.Height}" : "";

            string pythonCommand;
            if (initImageFilePath != null)
            {
                // img2img
                double strengthDbl = (double)strength.GetValueOrDefault(80) / 100;
                pythonCommand = $"python scripts/img2img.py --prompt \"{prompt}\"{sizeParams} --ckpt sd-v{request.Version}.ckpt --skip_grid --n_samples 1 --n_iter {request.Samples} --ddim_steps {request.Steps} --seed {seed} --init-img \"{initImageFilePath}\" --strength {strengthDbl} --outdir {outputDir}";
            }
            else
            {
                // txt2img
                pythonCommand = $"python scripts/txt2img.py --prompt \"{prompt}\"{sizeParams} --plms --ckpt sd-v{request.Version}.ckpt --skip_grid --n_samples 1 --n_iter {request.Samples} --ddim_steps {request.Steps} --seed {seed} --outdir {outputDir}";
            }

            var commands = new string[]
            {
                @"C:\tools\miniconda3\Scripts\activate.bat ldm",
                @$"cd ""{workingDir}""",
                pythonCommand,
                "conda deactivate"
            };

            var result = await _shellService.ExecuteWithTimeoutAsync(
                commands,
                workingDir,
                14,
                e =>
                {
                    _log.EphemeralLog("STDERR: " + e);
                },
                o =>
                {
                    _log.EphemeralLog("STDOUT: " + o);
                });
            
            return result;
        }

        private string[] GetOutputFiles(string documentId)
        {
            var path = GetOutputFolder(documentId);
            return Directory.GetFiles(path, "*.jpg").Union(Directory.GetFiles(path, "*.png")).ToArray();
        }

        private string GetOutputFolder(string documentId)
        {
            return Path.Combine(_settings.CacheDir, documentId, "samples");
        }
        private string GetInputFolder(string documentId)
        {
            return Path.Combine(_settings.CacheDir, documentId, "input");
        }

        private string GetScriptsFolder()
        {
            return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "PSScripts");
        }
    }
}
