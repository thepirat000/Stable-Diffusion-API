using Audit.Core;

namespace CompVis_StableDiffusion_Api.Services
{
    public class LogService : ILogService
    {
        public void EphemeralLog(string text, bool important = false)
        {
            AuditScope.Log("Ephemeral", new { Status = text });
        }
    }
}
