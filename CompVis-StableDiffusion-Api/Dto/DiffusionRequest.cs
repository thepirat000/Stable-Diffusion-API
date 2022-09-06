using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace CompVis_StableDiffusion_Api.Dto
{
    public class DiffusionRequestWithInitImage : DiffusionRequest
    {
        /// <summary>
        /// The initial image
        /// </summary>
        [JsonIgnore]
        public IFormFile InitImage { get; set; }

        /// <summary>
        /// strength for noising/unnoising the init image (0-100). 100 corresponds to full destruction of information in init image.
        /// </summary>
        [DefaultValue(80)]
        public int Strength { get; set; }
    }
    
    public class DiffusionRequest
    {
        /// <summary>
        /// Text prompt
        /// </summary>
        [Required]
        [MinLength(3)]
        public string Prompt { get; set; }
        /// <summary>
        /// Version to use (1-1, 1-2, 1-3, 1-4). Default is latest.
        /// </summary>
        [DefaultValue("1-4")]
        [MaxLength(3)]
        public string Version { get; set; }
        /// <summary>
        /// Samples to generate (1-9). Default is 1.
        /// </summary>
        [DefaultValue(1)]
        public int Samples { get; set; }
        /// <summary>
        /// Process steps (10-100). Default is 50.
        /// </summary>
        [DefaultValue(50)]
        public int Steps { get; set; }
        /// <summary>
        /// Random seed (0-9999). Default is 0.
        /// </summary>
        [DefaultValue(0)]
        public int Seed { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"'{Prompt}' (v{Version}). {Samples} samples, {Steps} steps";
        }
    }
}
