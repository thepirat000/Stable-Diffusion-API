using Microsoft.AspNetCore.StaticFiles;
using System.IO;

namespace CompVis_StableDiffusion_Api.Dto
{
    public class Attachment
    {
        /// <summary>
        /// Stores the file from a stream its full file system path
        /// </summary>
        public static Attachment CreateFromStream(Stream stream, string fullPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }
            return new Attachment(fullPath);
        }

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
