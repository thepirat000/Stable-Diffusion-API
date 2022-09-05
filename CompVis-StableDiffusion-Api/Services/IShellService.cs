using CompVis_StableDiffusion_Api.Dto;
using System;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Services
{
    public interface IShellService
    {
        Task<ExecuteResult> ExecuteWithTimeoutAsync(string[] commands, string? workingDirectory = null, int timeoutMinutes = 15,
            Action<string> stdErrDataReceivedCallback = null, Action<string> stdOutDataReceivedCallback = null);
    }
}
