using CompVis_StableDiffusion_Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Audit.WebApi;

namespace CompVis_StableDiffusion_Api.Api
{
    [Route("api/sd")]
    [ApiController]
    public class StableDiffusionController : ControllerBase
    {
        private readonly IStableDiffusionService _txtToImgService;
        private readonly Settings _settings;

        public StableDiffusionController(IStableDiffusionService txtToImgService, Settings settings)
        {
            _txtToImgService = txtToImgService;
            _settings = settings;
        }

        /// <summary>
        /// Enqueue a Text to Image process
        /// </summary>
        /// <param name="request">The request data</param>
        [HttpPost("txt2img")]
        public async Task<IActionResult> ProcessTextToImage([FromBody]Dto.DiffusionRequest request)
        {
            if (request?.Prompt == null || request.Prompt.Length < 3)
            {
                return BadRequest("Invalid prompt");
            }
            if (request.Version == null || request.Version.Length != 3 || request.Version[1] != '-')
            {
                return BadRequest("Invalid version, must be 1-1, 1-2, 1-3 or 1-4");
            }
            if (request.Samples < 1 || request.Samples > 9)
            {
                return BadRequest("Invalid samples, must be 1 to 9");
            }
            if (request.Steps < 10 || request.Steps > 100)
            {
                return BadRequest("Invalid steps, must be 10 to 100");
            }

            var response = await _txtToImgService.EnqueueJobAsync(GetCurrentClientId(), request, null, null);
            if (response?.BadRequestError != null)
            {
                return BadRequest(response.BadRequestError);
            }

            return Ok(response);
        }

        /// <summary>
        /// Enqueue an Image to Image process
        /// </summary>
        /// <param name="strength">Controls the amount of noise that is added to the input image. Values that approach 100 allow for lots of variations but will also produce images that are not semantically consistent with the input. Default is 80.</param>
        /// <param name="request">The request data</param>
        [HttpPost("img2img")]
        public async Task<IActionResult> ProcessImageToImage([FromForm] Dto.DiffusionRequestWithInitImage request)
        {
            if (request?.Prompt == null || request.Prompt.Length < 3)
            {
                return BadRequest("Invalid prompt");
            }
            if (request.Version == null || request.Version.Length != 3 || request.Version[1] != '-')
            {
                return BadRequest("Invalid version, must be 1-1, 1-2, 1-3 or 1-4");
            }
            if (request.Samples < 1 || request.Samples > 9)
            {
                return BadRequest("Invalid samples, must be 1 to 9");
            }
            if (request.Steps < 10 || request.Steps > 100)
            {
                return BadRequest("Invalid steps, must be 10 to 100");
            }
            if (request.Strength < 1 || request.Strength > 100)
            {
                return BadRequest("Invalid strength, must be 1 to 100");
            }
            if (request.InitImage == null)
            {
                return BadRequest("Invalid Init Image");
            }
            
            var response = await _txtToImgService.EnqueueJobAsync(GetCurrentClientId(), request, request.InitImage, request.Strength);
            if (response?.BadRequestError != null)
            {
                return BadRequest(response.BadRequestError);
            }

            return Ok(response);
        }

        /// <summary>
        /// Returns the status of a job processing
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Status([FromQuery(Name = "d")] string docId)
        {
            if (string.IsNullOrEmpty(docId))
            {
                return BadRequest("Invalid Document ID");
            }
            var document = await _txtToImgService.GetDocumentAsync(GetCurrentClientId(), docId);
            if (document == null)
            {
                return new NoContentResult();
            }

            return Ok(document);
        }

        /// <summary>
        /// Returns the jobs for the current client
        /// </summary>
        /// <returns></returns>
        [HttpGet("query")]
        public async Task<IActionResult> Query()
        {
            var documents = await _txtToImgService.GetDocumentsForClientAsync(GetCurrentClientId());
            if (documents == null || documents.Count == 0)
            {
                return new NoContentResult();
            }

            return Ok(documents);
        }

        /// <summary>
        /// Cancels a running job
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IActionResult> Cancel([FromQuery(Name = "d")] string docId)
        {
            if (string.IsNullOrEmpty(docId))
            {
                return BadRequest("Invalid document ID");
            }
            var deleted = await _txtToImgService.CancelJobAsync(GetCurrentClientId(), docId);
            
            return deleted ? Ok() : NoContent();
        }

        /// <summary>
        /// Downloads an image result of a job
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <param name="imageIndex">The image index to download. Default is 0.</param>
        /// <param name="mode">The download mode (0=direct, 1=attachment). Default is 0.</param>
        /// <returns></returns>
        [HttpGet("dl")]
        public async Task<IActionResult> Download([FromQuery(Name = "d")] string docId, [FromQuery(Name = "i")] int imageIndex = 0, [FromQuery(Name = "m")]int mode = 0)
        {
            if (string.IsNullOrEmpty(docId))
            {
                return BadRequest("Invalid document ID");
            }
            if (imageIndex < 0 || imageIndex > 9)
            {
                return BadRequest("Invalid image index");
            }
            var document = await _txtToImgService.GetDocumentAsync(GetCurrentClientId(), docId);
            if (document == null)
            {
                return NoContent();
            }
            if (document.Status != 100)
            {
                return BadRequest("Job not finished");
            }
            if (document.FileRefs.Count <= imageIndex)
            {
                return BadRequest("Invalid image index");
            }
            var attachment = new Dto.Attachment(document.FileRefs[imageIndex]);
            
            if (mode == 0)
            {
                return new FileStreamResult(attachment.Stream, attachment.MimeType);
            }
            else
            {
                return File(attachment.Stream, attachment.MimeType, $"{docId}_{attachment.FileName}");
            }
        }

        private string GetCurrentClientId()
        {
            return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
