using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed record PersonalBestInsertOutcome(bool Inserted, bool CannotInsert, IReadOnlyList<Guid> OutrankingEntryIds);

// Implements the shared "is this an improvement" rule from UC7 (import) and UC10 (automatic registration
// on match deactivation): the log entries table is a chronological record of PB improvements only.
public interface IPersonalBestEngine
{
    Task<PersonalBestInsertOutcome> TryInsertAsync(Guid owningTenantId, string federationNumber, string discipline, string matchClassifier, int score, DateOnly date, DateTime recordedAt, string source, CancellationToken cancellationToken);

    Task<PersonalBestLogEntry?> GetCurrentBestAsync(Guid owningTenantId, string federationNumber, string discipline, string matchClassifier, CancellationToken cancellationToken);

    Task EnsureArcherNameAsync(Guid owningTenantId, string federationNumber, string name, CancellationToken cancellationToken);
}

public sealed class PersonalBestEngine(ApplicationDbContext db) : IPersonalBestEngine
{
    public async Task<PersonalBestInsertOutcome> TryInsertAsync(Guid owningTenantId, string federationNumber, string discipline, string matchClassifier, int score, DateOnly date, DateTime recordedAt, string source, CancellationToken cancellationToken)
    {
        var entries = await db.PersonalBestLogEntries
            .Where(item => item.TenantId == owningTenantId && item.FederationNumber == federationNumber && item.Discipline == discipline && item.MatchClassifier == matchClassifier)
            .ToListAsync(cancellationToken);

        if (entries.Any(item => item.Date == date && item.Score == score))
        {
            return new PersonalBestInsertOutcome(false, false, []);
        }

        // The entry with the highest date on or before the proposed date; ties broken by highest score.
        var priorOnOrBefore = entries
            .Where(item => item.Date <= date)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Score)
            .FirstOrDefault();

        if (priorOnOrBefore is null || priorOnOrBefore.Score < score)
        {
            db.PersonalBestLogEntries.Add(new PersonalBestLogEntry
            {
                Id = Guid.NewGuid(),
                TenantId = owningTenantId,
                FederationNumber = federationNumber,
                Discipline = discipline,
                MatchClassifier = matchClassifier,
                Score = score,
                Date = date,
                RecordedAt = recordedAt,
                Source = source
            });

            var superseded = entries.Where(item => item.Date > date && item.Score <= score).ToList();
            if (superseded.Count > 0) db.PersonalBestLogEntries.RemoveRange(superseded);
            return new PersonalBestInsertOutcome(true, false, []);
        }

        var outranking = entries.Where(item => item.Date <= date && item.Score > score).Select(item => item.Id).ToList();
        return new PersonalBestInsertOutcome(false, true, outranking);
    }

    public Task<PersonalBestLogEntry?> GetCurrentBestAsync(Guid owningTenantId, string federationNumber, string discipline, string matchClassifier, CancellationToken cancellationToken) =>
        db.PersonalBestLogEntries.AsNoTracking()
            .Where(item => item.TenantId == owningTenantId && item.FederationNumber == federationNumber && item.Discipline == discipline && item.MatchClassifier == matchClassifier)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Score)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task EnsureArcherNameAsync(Guid owningTenantId, string federationNumber, string name, CancellationToken cancellationToken)
    {
        var existing = await db.PersonalBestArcherNames.SingleOrDefaultAsync(item => item.TenantId == owningTenantId && item.FederationNumber == federationNumber, cancellationToken);
        if (existing is null)
        {
            db.PersonalBestArcherNames.Add(new PersonalBestArcherName { Id = Guid.NewGuid(), TenantId = owningTenantId, FederationNumber = federationNumber, Name = name });
        }
        else
        {
            existing.Name = name;
        }
    }
}
