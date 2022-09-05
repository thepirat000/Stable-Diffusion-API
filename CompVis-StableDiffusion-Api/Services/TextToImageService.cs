using CompVis_StableDiffusion_Api.Dto;
using Hangfire;
using Hangfire.Server;
using System;
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

        public TextToImageResponse EnqueueJob(TextToImageRequest request)
        {
            // Enqueue job
            var jobId = _backgroundJobClient.Enqueue(() => ProcessAsync(request, null));
            _log.EphemeralLog($"Enqueueing job {jobId} for '{request.Prompt}'");
            return new TextToImageResponse() { JobId = jobId };
        }
        
        public async Task<TextToImageDocument> GetJobStatus(string jobId)
        {
            var document = await _storageService.GetAsync(jobId);
            if (document == null)
            {
                //
                using (var cnn = JobStorage.Current.GetConnection())
                {
                    var jobData = cnn.GetJobData(jobId);
                    if (jobData != null)
                    {
                        document = new TextToImageDocument()
                        {
                            JobId = jobId,
                            Status = jobData.State == "Enqueued" ? 0 : -1
                        };
                    }
                }
            }
            return document;
        }

        public async Task<bool> CancelJobAsync(string jobId)
        {
            var deleted = _backgroundJobClient.Delete(jobId);
            if (deleted)
            {
                var doc = await _storageService.GetAsync(jobId);
                if (doc != null && doc.Status != 100)
                {
                    await _storageService.UpdateStatusAsync(jobId, -2, "Cancelled by user");
                }
            }
            return deleted;
        }

        // Main Job Process method
        public async Task ProcessAsync(TextToImageRequest request, PerformContext context)
        {
            var jobId = context.BackgroundJob.Id;

            // Store document
            _log.EphemeralLog($"Starting job {jobId} for '{request.Prompt}'");
            var document = new TextToImageDocument()
            {
                JobId = jobId,
                Request = request,
                Status = 1
            };
            await _storageService.InsertAsync(document);

            string error = null;
            string[] files = null;
            ExecuteResult execResult = null;
            try
            {
                // Processing
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
                    _log.EphemeralLog($"Attaching {files.Length} files to job {jobId} for '{request.Prompt}'");
                    await _storageService.AttachAsync(jobId, files.Select(f => new Attachment(f)).ToArray());
                }
            }

            // Update status to Completed or Error
            await _storageService.UpdateStatusAsync(jobId, error == null ? 100 : -1, error);
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
