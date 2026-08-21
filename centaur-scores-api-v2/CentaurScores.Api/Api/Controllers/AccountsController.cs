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
    public async Task<IActionResult> List(CancellationToken cancellationToken) => !CanManage ? Forbid() : Ok(await db.Accounts.AsNoTracking().Where(item => item.TenantId == TenantId).Select(item => new { item.Id, item.Username, item.DisplayName, item.Email, item.Authorization }).OrderBy(item => item.Username).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var account = new Account { Id = Guid.NewGuid(), TenantId = TenantId, Username = request.Username, PasswordHash = Passwords.Hash(request.Password ?? throw new ArgumentException("Password is required")), DisplayName = request.DisplayName, Email = request.Email, Authorization = Enum.Parse<AuthorizationProfile>(request.Authorization, true) };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/accounts/{account.Id}", new { account.Id, account.Username, account.DisplayName, account.Email, account.Authorization });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (account is null) return NotFound();
        account.Username = request.Username;
        account.DisplayName = request.DisplayName;
        account.Email = request.Email;
        account.Authorization = Enum.Parse<AuthorizationProfile>(request.Authorization, true);
        if (!string.IsNullOrWhiteSpace(request.Password)) account.PasswordHash = Passwords.Hash(request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { account.Id, account.Username, account.DisplayName, account.Email, account.Authorization });
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