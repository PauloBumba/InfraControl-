using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Uploads;
using InfraControl.Application.Uploads.Ports;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Uploads;

/// <summary>
/// F24-T008 — upload genérico de imagens (reutilizável por módulo).
/// Multipart <c>file</c> + query <c>module</c> (knowledge|general).
/// Preserva a extensão original; path web em <c>/uploads/{module}/…</c>.
/// </summary>
[Route("api/v1/uploads/images")]
public sealed class ImageUploadsController(IImageUploadStore store) : BaseController
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    [RequestSizeLimit(ImageUploadRules.MaxFileSizeBytes)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string module = "general",
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail(["Nenhum arquivo enviado."]));

        try
        {
            ImageUploadRules.EnsureAllowed(file.FileName, file.ContentType, file.Length);
            var extension = ImageUploadRules.NormalizeExtension(file.FileName);
            await using var stream = file.OpenReadStream();
            var imagePath = await store.SaveAsync(module, extension, stream, cancellationToken);
            return Ok(ApiResponse.Ok(new { imagePath }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail([ex.Message]));
        }
    }
}
