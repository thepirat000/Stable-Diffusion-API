namespace CompVis_StableDiffusion_Api
{
    public class Settings
    {
        public string StorageConnectionString { get; set; } = "http://127.0.0.1:8080";
        public string StorageDatabase { get; set; } = "Diffusion";
        public string WorkingDir { get; set; } = @"C:\GIT\stable-diffusion";
        public string OutputDir { get; set; } = @"C:\cache\diffusion";
        public int WorkerCount { get; set; } = 2;
    }
}
