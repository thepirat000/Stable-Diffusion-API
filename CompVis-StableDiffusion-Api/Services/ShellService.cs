using CompVis_StableDiffusion_Api.Dto;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{

    public class ShellService : IShellService
    {
        private readonly ILogService _log;
        public ShellService(ILogService logService)
        {
            _log = logService;
        }
        
        public async Task<ExecuteResult> ExecuteWithTimeoutAsync(string[] commands, string? workingDirectory = null, int timeoutMinutes = 15,
            Action<string> stdErrDataReceivedCallback = null, Action<string> stdOutDataReceivedCallback = null)
        {
            _log.EphemeralLog("Will execute commands: " + string.Join(Environment.NewLine, commands));
            var stdOutputBuilder = new StringBuilder();
            var stdErrorBuilder = new StringBuilder();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"cmd.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                },
                EnableRaisingEvents = true
            };
            process.ErrorDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdErrDataReceivedCallback?.Invoke(e.Data);
                    stdErrorBuilder.AppendLine(e.Data);
                }
            });
            process.OutputDataReceived += new DataReceivedEventHandler(delegate (object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdOutDataReceivedCallback?.Invoke(e.Data);
                    stdOutputBuilder.AppendLine(e.Data);
                }
            });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    foreach (var command in commands)
                    {
                        await sw.WriteLineAsync(command);
                    }
                }
            }

            var timedout = await WaitOrKill(process, timeoutMinutes);

            return new ExecuteResult()
            {
                ExitCode = timedout ? -1 : process.ExitCode,
                StdError = stdErrorBuilder.ToString(),
                StdOutput = stdOutputBuilder.ToString()
            };
        }
        
        private async Task<bool> WaitOrKill(Process process, int timeoutMinutes)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeoutMinutes * 60 * 1000);
            await process.WaitForExitAsync(cts.Token);
            bool cancelled = false;
            if (cts.IsCancellationRequested)
            {
                cancelled = true;
                _log.EphemeralLog($"---------------> PROCESS EXITED AFTER TIMEOUT. Killing process.", true);
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    try { process.Kill(); } finally { }
                }
            }
            process.CancelOutputRead();
            process.CancelErrorRead();
            return cancelled;
        }

    }
}
