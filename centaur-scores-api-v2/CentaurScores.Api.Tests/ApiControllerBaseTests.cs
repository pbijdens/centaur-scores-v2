using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

// The TENANT_NOT_SELECTED guard is an IActionFilter, so it only runs through the MVC action-invoker
// pipeline - the other controller tests in this project call action methods directly and never exercise
// it. These tests build a minimal ActionExecutingContext to invoke the filter itself.
public sealed class ApiControllerBaseTests
{
    [Fact]
    public async Task OnActionExecuting_rejects_requests_with_no_tenant_selected()
    {
        var context = await BuildContextAsync(Guid.Empty);

        ((IActionFilter)context.Controller).OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var error = Assert.IsType<ApiError>(result.Value);
        Assert.Equal("TENANT_NOT_SELECTED", error.Code);
    }

    [Fact]
    public async Task OnActionExecuting_allows_requests_with_a_selected_tenant()
    {
        var context = await BuildContextAsync(Guid.NewGuid());

        ((IActionFilter)context.Controller).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    private static async Task<ActionExecutingContext> BuildContextAsync(Guid tenantId)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var controller = new AccountsController(db, new TestTenantContext(tenantId));
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => true;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}
