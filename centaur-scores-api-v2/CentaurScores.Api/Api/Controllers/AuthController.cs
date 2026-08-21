using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, ApplicationDbContext db, ITenantContext tenantContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.AuthenticateAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(new { token = result.Token, expiresAt = result.ExpiresAt, account = new { result.Account.Id, result.Account.Username, result.Account.DisplayName, result.Account.Email } });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tenantContext.AccountId, cancellationToken);
        return account is null ? NotFound() : Ok(new { account.Id, account.Username, account.DisplayName, account.Email, Authorization = account.Authorization.ToString() });
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == tenantContext.AccountId, cancellationToken);
        if (account is null) return NotFound();
        account.DisplayName = request.DisplayName;
        account.Email = request.Email;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { account.Id, account.Username, account.DisplayName, account.Email, Authorization = account.Authorization.ToString() });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == tenantContext.AccountId, cancellationToken);
        if (account is null) return NotFound();
        if (!Passwords.Verify(request.CurrentPassword, account.PasswordHash)) return BadRequest(new { message = "Current password is incorrect." });
        if (string.IsNullOrWhiteSpace(request.NewPassword)) return BadRequest(new { message = "New password is required." });
        account.PasswordHash = Passwords.Hash(request.NewPassword);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}