using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CompVis_StableDiffusion_Api.Dto
{
    /// <summary>
    /// Diffusion Document entity
    /// </summary>
    public class DiffusionDocument
    {
        public string Id { get; set; }

        public string JobId { get; set; }

        public DiffusionRequest Request { get; set; }

        public int Status { get; set; }

        public string Error { get; set; }

        public List<string> FileRefs { get; set; }

        public string InitImageName { get; set; }

        public string ClientId { get; set; }

        public DateTimeOffset CreatedDate { get; set; } 

        public DateTimeOffset ModifiedDate { get; set; }
    }
}
