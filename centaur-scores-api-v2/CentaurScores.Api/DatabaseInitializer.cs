using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CentaurScores.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (await db.Tenants.AnyAsync(cancellationToken)) return;

        var rootId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        db.Tenants.Add(new Tenant { Id = rootId, Name = "Root Tenant" });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            TenantId = rootId,
            Username = "centaurscores",
            PasswordHash = Passwords.Hash("centaurscores"),
            DisplayName = "Centaur Scores",
            Authorization = AuthorizationProfile.Administrator
        });
        db.Matches.AddRange(
            new Match { Id = Guid.NewGuid(), TenantId = rootId, Name = "Spring Open 18m", Date = new DateOnly(2026, 8, 22), IsOpen = true, Ends = 10, ArrowsPerEnd = 3 },
            new Match { Id = Guid.NewGuid(), TenantId = rootId, Name = "Club night · 25m", Date = new DateOnly(2026, 8, 28), Ends = 10, ArrowsPerEnd = 3 });
        db.Competitions.Add(new Competition { Id = Guid.NewGuid(), TenantId = rootId, Name = "Club Series 2026", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 9, 30) });
        await db.SaveChangesAsync(cancellationToken);
    }
}