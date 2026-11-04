using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Authorize(Roles = "Administrator,Manager")]
public class UploadsController : ControllerBase
{
    private readonly ImageStorage _images;

    public UploadsController(ImageStorage images) => _images = images;

    [HttpPost("image")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<ImageUploadResult>> UploadImage(IFormFile? file,
        [FromQuery] string? kind, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse("Choose an image to upload."));

        try
        {
            return Ok(await _images.SaveAsync(file, kind, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }
}
