using CompVis_StableDiffusion_Api.Dto;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public class FakeShellService : IShellService
    {
        private readonly Random _rnd = new Random();
        private readonly ILogService _log;
        private readonly Settings _settings;
        public FakeShellService(ILogService logService, Settings settings)
        {
            _log = logService;
            _settings = settings;
        }
        
        public async Task<ExecuteResult> ExecuteWithTimeoutAsync(string[] commands, string? workingDirectory = null, int timeoutMinutes = 15, Action<string> stdErrDataReceivedCallback = null, Action<string> stdOutDataReceivedCallback = null, string processName = "cmd.exe")
        {
            double percentage = 0;
            int i = 0;
            foreach(var command in commands)
            {
                i++;
                percentage += i / commands.Length * 100;
                _log.EphemeralLog("Fake executing command: " + command);
                await Task.Delay(_rnd.Next(2000, 5000));
                stdOutDataReceivedCallback.Invoke($"Fake output: {percentage}%");
            }
            var dir = commands[2].Split("--outdir")[1].Trim();
            //this image is a single pixel (black)
            byte[] bytes = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAAAAACH5BAAAAAAALAAAAAABAAEAAAICTAEAOw==");
            Directory.CreateDirectory(Path.Combine(dir, "samples"));
            File.WriteAllBytes(Path.Combine(dir, "samples", "00001.png"), bytes);
            File.WriteAllBytes(Path.Combine(dir, "samples", "00002.png"), bytes);

            return new ExecuteResult()
            {
                ExitCode = 0,
                StdOutput = "complete output blah blah blah"
            };
        }
    }
}
