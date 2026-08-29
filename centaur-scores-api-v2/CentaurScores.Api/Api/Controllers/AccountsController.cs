using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/accounts")]
public sealed class AccountsController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => !CanManage ? Forbid() : Ok(await db.Accounts.AsNoTracking().Where(item => item.TenantId == TenantId).Select(item => new { item.Id, item.Username, item.DisplayName, item.Email, Authorization = item.Authorization.ToString() }).OrderBy(item => item.Username).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        return account is null ? NotFound() : Ok(new { account.Id, account.Username, account.DisplayName, account.Email, Authorization = account.Authorization.ToString() });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Username)) return BadRequest(new ApiError("USERNAME_REQUIRED", "Username is required."));
        if (await db.Accounts.AnyAsync(item => item.Username == request.Username, cancellationToken))
            return Conflict(new ApiError("USERNAME_TAKEN", "An account with this username already exists."));
        AuthorizationProfile authorization;
        try { authorization = string.IsNullOrWhiteSpace(request.Authorization) ? AuthorizationProfile.Viewer : Enum.Parse<AuthorizationProfile>(request.Authorization, true); }
        catch (ArgumentException) { return BadRequest(new ApiError("INVALID_AUTHORIZATION", "Unknown authorization profile.")); }
        var password = string.IsNullOrWhiteSpace(request.Password) ? Passwords.GenerateRandom() : request.Password;
        var account = new Account { Id = Guid.NewGuid(), TenantId = TenantId, Username = request.Username, PasswordHash = Passwords.Hash(password), DisplayName = request.DisplayName, Email = request.Email, Authorization = authorization };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/accounts/{account.Id}", new { account.Id, account.Username, account.DisplayName, account.Email, Authorization = account.Authorization.ToString() });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (account is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Username)) return BadRequest(new ApiError("USERNAME_REQUIRED", "Username is required."));
        if (await db.Accounts.AnyAsync(item => item.Id != id && item.Username == request.Username, cancellationToken))
            return Conflict(new ApiError("USERNAME_TAKEN", "An account with this username already exists."));
        account.Username = request.Username;
        account.DisplayName = request.DisplayName;
        account.Email = request.Email;
        if (!string.IsNullOrWhiteSpace(request.Authorization)) account.Authorization = Enum.Parse<AuthorizationProfile>(request.Authorization, true);
        if (!string.IsNullOrWhiteSpace(request.Password)) account.PasswordHash = Passwords.Hash(request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { account.Id, account.Username, account.DisplayName, account.Email, Authorization = account.Authorization.ToString() });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (account is null) return NotFound();
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}