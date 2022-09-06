using CompVis_StableDiffusion_Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CompVis_StableDiffusion_Api.Api
{
    [Route("api/txt2img")]
    [ApiController]
    public class TextToImageController : ControllerBase
    {
        private readonly ITextToImageService _txtToImgService;

        public TextToImageController(ITextToImageService txtToImgService)
        {
            _txtToImgService = txtToImgService;
        }

        /// <summary>
        /// Enqueue a Text to Image process
        /// </summary>
        /// <param name="request">The request data</param>
        [HttpPost]
        public async Task<IActionResult> Process([FromBody]Dto.TextToImageRequest request)
        {
            if (request?.Prompt == null || request.Prompt.Length < 3)
            {
                return BadRequest("Invalid prompt");
            }
            if (request.Version == null || request.Version.Length != 3 || request.Version[1] != '-')
            {
                return BadRequest("Invalid version, must be 1-1, 1-2, 1-3 or 1-4");
            }
            if (request.Samples <= 0 || request.Samples > 9)
            {
                return BadRequest("Invalid samples, must be 1 to 9");
            }
            if (request.Steps <= 10 || request.Steps > 100)
            {
                return BadRequest("Invalid steps, must be 10 to 100");
            }

            var response = await _txtToImgService.EnqueueJobAsync(GetCurrentClientId(), request);
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
                return File(attachment.Stream, attachment.MimeType, $"{docId}_{attachment.Filename}");
            }
        }

        private string GetCurrentClientId()
        {
            return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
