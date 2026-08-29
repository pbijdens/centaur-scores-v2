using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CentaurScores.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase(ITenantContext tenantContext) : ControllerBase, IActionFilter
{
    protected Guid TenantId => tenantContext.TenantId;
    protected bool CanManage => tenantContext.CanManage;
    protected bool IsAdministrator => tenantContext.IsAdministrator;

    // A freshly issued login token carries tenant_id = Guid.Empty (no tenant selected yet) but still
    // carries the account's real Role claim, since role is home-tenant-level, not tenant-selection-level.
    // Without this guard, that token could pass IsAdministrator/CanManage checks and reach tenant-scoped
    // actions (or get TenantId = Guid.Empty stamped onto a new row) before a tenant is ever chosen.
    // AuthController does not derive from this base, so /auth/me and /auth/select-tenant stay reachable.
    // Implemented explicitly (not as public methods) - ASP.NET Core's controller action discovery treats
    // any public method on a controller subclass as a candidate action, so a plain public implementation
    // of OnActionExecuting/OnActionExecuted gets registered as a routable action itself and collides with
    // every real action on the controller (AmbiguousMatchException). MVC still invokes filter interfaces
    // through the interface reference regardless of visibility, so this keeps the guard working.
    void IActionFilter.OnActionExecuting(ActionExecutingContext context)
    {
        if (TenantId == Guid.Empty)
            context.Result = new ObjectResult(new ApiError("TENANT_NOT_SELECTED", "Select a tenant before using this endpoint.")) { StatusCode = StatusCodes.Status403Forbidden };
    }

    void IActionFilter.OnActionExecuted(ActionExecutedContext context) { }
}