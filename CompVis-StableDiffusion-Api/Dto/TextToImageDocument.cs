using System.Collections.Generic;

namespace CompVis_StableDiffusion_Api.Dto
{
    public class TextToImageDocument
    {
        public string JobId { get; set; }
        public TextToImageRequest Request { get; set; }
        public int Status { get; set; }
        public string Error { get; set; }
        public List<string> FileRefs { get; set; }
    }
}
