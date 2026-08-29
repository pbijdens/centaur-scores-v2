using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CentaurScores.Api.Controllers;

[Route("api/backup")]
public sealed class BackupController(ITenantContext tenantContext, IBackupService backupService, IRestoreService restoreService) : ApiControllerBase(tenantContext)
{
    [HttpPost("export")]
    public async Task<IActionResult> Export(CreateBackupRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var (zipBytes, fileName) = await backupService.CreateBackupAsync(TenantId, request.IncludeSubTenants, cancellationToken);
        return File(zipBytes, "application/zip", fileName);
    }

    [HttpPost("restore")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Restore(IFormFile file, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        if (file is null || file.Length == 0) return BadRequest(new ApiError("RESTORE_FILE_MISSING", "No file was uploaded."));

        try
        {
            using var stream = file.OpenReadStream();
            var result = await restoreService.RestoreAsync(TenantId, stream, cancellationToken);
            return Ok(result);
        }
        catch (BackupRestoreException exception)
        {
            return BadRequest(new ApiError(exception.Code, exception.Message));
        }
    }
}
