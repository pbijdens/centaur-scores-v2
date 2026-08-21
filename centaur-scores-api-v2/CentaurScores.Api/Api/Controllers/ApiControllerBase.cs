using CentaurScores.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentaurScores.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase(ITenantContext tenantContext) : ControllerBase
{
    protected Guid TenantId => tenantContext.TenantId;
    protected bool CanManage => tenantContext.CanManage;
    protected bool IsAdministrator => tenantContext.IsAdministrator;
}