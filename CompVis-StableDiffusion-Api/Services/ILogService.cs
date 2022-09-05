namespace CompVis_StableDiffusion_Api.Services
{
    public interface ILogService
    {
        void EphemeralLog(string text, bool important = false);
    }
}
