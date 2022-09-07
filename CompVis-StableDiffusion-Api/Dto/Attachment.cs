using Microsoft.AspNetCore.StaticFiles;
using System.IO;

namespace CompVis_StableDiffusion_Api.Dto
{
    public class Attachment
    {
        /// <summary>
        /// Read the file from its full file system path
        /// </summary>
        public Attachment(string fullPath)
        {
            FilePath = fullPath;
            FileName = Path.GetFileName(fullPath);
            Extension = Path.GetExtension(fullPath);
            MimeType = GetMimeType(fullPath);
            Stream = File.OpenRead(fullPath);
        }
        
        private string GetMimeType(string fileName)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out var contentType))
            {
                contentType = "application/octet-stream";
            }
            return contentType;
        }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public string MimeType { get; set; }
        public Stream Stream { get; set; }
    }
}
