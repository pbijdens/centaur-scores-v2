using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CentaurScores.Api.Application;

// Builds the per-participant "personal best, expressed as an arrow average" map used to render the
// "PB: x.xx" live-scoring line (see documentation/PERSONAL-BEST-FEATUE.md, "During a match"). Kept out of
// LiveScoringService (which stays DB-free) - callers build this once per request and pass it in.
public interface IPersonalBestLiveLookup
{
    Task<IReadOnlyDictionary<Guid, double>> BuildAsync(Match match, LiveScoreScope scope, CancellationToken cancellationToken);

    // Call after any change to a match's participants (add/remove/edit federation number or categories)
    // so the next BuildAsync recomputes instead of serving a stale cached result for up to 15 minutes.
    void Invalidate(Guid matchId);
}

public sealed class PersonalBestLiveLookup(ApplicationDbContext db, IPersonalBestContext personalBestContext, IPersonalBestEngine engine, IMemoryCache cache) : IPersonalBestLiveLookup
{
    // Every open narrowcast/live-scoring screen re-requests this on its poll timer (see LiveScoringPage's
    // hardcoded 15s timeout), which was hammering the database with one query per participant on every poll.
    // Caching per match collapses that back down to one computation per match per window.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private static string CacheKey(Guid matchId) => $"personal-best-live:{matchId}";

    public async Task<IReadOnlyDictionary<Guid, double>> BuildAsync(Match match, LiveScoreScope scope, CancellationToken cancellationToken)
    {
        if (!scope.IncludePersonalBest) return new Dictionary<Guid, double>();

        var cacheKey = CacheKey(match.Id);
        if (cache.TryGetValue(cacheKey, out IReadOnlyDictionary<Guid, double>? cached)) return cached!;

        var result = await ComputeAsync(match, cancellationToken);
        cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    public void Invalidate(Guid matchId) => cache.Remove(CacheKey(matchId));

    private async Task<IReadOnlyDictionary<Guid, double>> ComputeAsync(Match match, CancellationToken cancellationToken)
    {
        var empty = new Dictionary<Guid, double>();

        // If the tenant has since disabled the feature entirely, ignore the scope's request - see the
        // doc's Q&A: "the request for PR information in the scope settings must be ignored."
        var owningTenantId = await personalBestContext.ResolveOwningTenantIdAsync(match.TenantId, cancellationToken);
        if (owningTenantId is null) return empty;
        if (string.IsNullOrWhiteSpace(match.PersonalBestClassifier)) return empty;

        var totalArrows = match.Ends * match.ArrowsPerEnd;
        if (totalArrows <= 0) return empty;

        var mappings = await (
            from mapping in db.PersonalBestDisciplineMappings
            join discipline in db.PersonalBestDisciplines on mapping.DisciplineId equals discipline.Id
            where mapping.TenantId == owningTenantId && mapping.SourceTenantId == match.TenantId
            select new { mapping.CategoryId, mapping.ValueId, discipline.Name }
        ).ToListAsync(cancellationToken);
        if (mappings.Count == 0) return empty;

        var result = new Dictionary<Guid, double>();
        foreach (var participant in match.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.FederationNumber)) continue;
            var matchedDisciplines = mappings
                .Where(mapping => participant.Categories.TryGetValue(mapping.CategoryId, out var valueId) && valueId == mapping.ValueId)
                .Select(mapping => mapping.Name)
                .Distinct()
                .ToList();
            if (matchedDisciplines.Count != 1) continue;

            var best = await engine.GetCurrentBestAsync(owningTenantId.Value, participant.FederationNumber!, matchedDisciplines[0], match.PersonalBestClassifier!, cancellationToken);
            if (best is null) continue;
            result[participant.Id] = (double)best.Score / totalArrows;
        }

        return result;
    }
}
