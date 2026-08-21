using CentaurScores.Api.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CentaurScores.Api.Application;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid AccountId { get; }
    bool IsAdministrator { get; }
    bool CanManage { get; }
}

public sealed class TenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException());
    public Guid AccountId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new UnauthorizedAccessException());
    public bool IsAdministrator => User.IsInRole(AuthorizationProfile.Administrator.ToString());
    public bool CanManage => IsAdministrator || User.IsInRole(AuthorizationProfile.Manager.ToString());
}