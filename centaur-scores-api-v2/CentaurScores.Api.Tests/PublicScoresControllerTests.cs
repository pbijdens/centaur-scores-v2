using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CentaurScores.Api.Tests;

public sealed class PublicScoresControllerTests
{
    [Fact]
    public async Task Live_scoring_routes_are_scope_scoped_across_all_tenants()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A" };
        var otherTenant = new Tenant { Id = Guid.NewGuid(), Name = "Tenant B" };
        var visibleMatch = MatchFor(tenant.Id, "Visible", "club", true);
        var otherScope = MatchFor(tenant.Id, "Other scope", "finals", true);
        var closedMatch = MatchFor(tenant.Id, "Closed", "club", false);
        var otherTenantMatch = MatchFor(otherTenant.Id, "Other tenant", "club", true);
        db.AddRange(tenant, otherTenant, visibleMatch, otherScope, closedMatch, otherTenantMatch);
        await db.SaveChangesAsync();

        var controller = new PublicScoresController(db, new LiveScoringService(new ScoringService()), new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));

        var listResult = await controller.LiveScoringMatches("club", CancellationToken.None);
        var matches = Assert.IsAssignableFrom<IReadOnlyList<LiveScoringMatch>>(Assert.IsType<OkObjectResult>(listResult.Result).Value);
        Assert.Collection(
            matches,
            match => Assert.Equal(otherTenantMatch.Id, match.Id),
            match => Assert.Equal(visibleMatch.Id, match.Id));

        var otherTenantPageResult = await controller.LiveScoringPage("club", otherTenantMatch.Id, CancellationToken.None);
        var page = Assert.IsType<LiveScoringPage>(Assert.IsType<OkObjectResult>(otherTenantPageResult.Result).Value);
        Assert.Equal(otherTenant.Name, page.Tenant);

        var wrongScopeResult = await controller.LiveScoringPage("finals", otherTenantMatch.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(wrongScopeResult.Result);
    }

    private static Match MatchFor(Guid tenantId, string name, string scope, bool isOpen)
    {
        var matchId = Guid.NewGuid();
        return new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = name,
            Date = new DateOnly(2026, 8, 22),
            IsOpen = isOpen,
            LiveScopes = [new LiveScoreScope { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, Scope = scope }]
        };
    }
}