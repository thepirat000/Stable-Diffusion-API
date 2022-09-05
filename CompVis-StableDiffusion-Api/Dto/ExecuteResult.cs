using System;

namespace CompVis_StableDiffusion_Api.Dto
{
    public class ExecuteResult
    {
        public int ExitCode { get; set; }
        public string StdOutput { get; set; }
        public string StdError { get; set; }
        public string Output =>
            string.IsNullOrEmpty(StdError)
                ? StdOutput
                : StdError + Environment.NewLine + Environment.NewLine + StdOutput;
    }
}
