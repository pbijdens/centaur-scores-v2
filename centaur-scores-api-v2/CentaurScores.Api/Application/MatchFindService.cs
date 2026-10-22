using System.Globalization;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public enum MatchFindOutcome { Found, InvalidInput, NotFound, Ambiguous }

public sealed record MatchFindResult(MatchFindOutcome Outcome, FoundMatch? Match = null, ApiError? Error = null);

public interface IMatchFindService
{
    Task<MatchFindResult> FindAsync(Guid accountId, FindMatchRequest request, CancellationToken cancellationToken);
}

// Finds exactly one match by date + start time (the match's ShortCode) across every tenant the account is
// authorized for, and projects its results for an external system. See documentation/FIND-MATCHES.md.
public sealed class MatchFindService(ApplicationDbContext db, IAuthService authService, IScoringService scoring) : IMatchFindService
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm"];

    public async Task<MatchFindResult> FindAsync(Guid accountId, FindMatchRequest request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return new MatchFindResult(MatchFindOutcome.InvalidInput, Error: new ApiError("INVALID_DATE", "date must be a valid date in YYYY-MM-DD format."));
        if (TryParseTime(request.Starttime) is not { } startTime)
            return new MatchFindResult(MatchFindOutcome.InvalidInput, Error: new ApiError("INVALID_STARTTIME", "starttime must be a valid time in hh:mm (24h) format."));

        var tenants = await authService.GetAuthorizedTenantsAsync(accountId, cancellationToken);
        var tenantNames = tenants.ToDictionary(item => item.TenantId, item => item.TenantName);
        var tenantIds = tenantNames.Keys.ToList();

        // Candidates are fetched without participants/scores; the full match is only loaded once it is known
        // to be the single hit. ShortCode is free text, so the time comparison happens in memory after parsing.
        var candidates = await db.Matches.AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId) && item.Date == date)
            .Select(item => new { item.Id, item.TenantId, item.Name, item.ShortCode })
            .ToListAsync(cancellationToken);
        var hits = candidates.Where(item => TryParseTime(item.ShortCode) == startTime).ToList();

        if (hits.Count == 0)
            return new MatchFindResult(MatchFindOutcome.NotFound, Error: new ApiError("MATCH_NOT_FOUND", $"No match found on {date:yyyy-MM-dd} at {startTime:HH:mm}."));
        if (hits.Count > 1)
        {
            var descriptions = hits.Select(item => Describe(tenantNames[item.TenantId], item.Name)).Order(StringComparer.Ordinal);
            return new MatchFindResult(MatchFindOutcome.Ambiguous, Error: new ApiError("MATCH_AMBIGUOUS", $"More than one match found on {date:yyyy-MM-dd} at {startTime:HH:mm}: {string.Join("; ", descriptions)}."));
        }

        var hit = hits[0];
        var match = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.Participants).ThenInclude(item => item.ParticipantListMember)
            .SingleAsync(item => item.Id == hit.Id, cancellationToken);
        var categories = await LoadCategoryPropertiesAsync(match.TenantId, cancellationToken);

        var participantsById = match.Participants.ToDictionary(item => item.Id);
        var results = scoring.Rank(match.Participants, match)
            .Select(ranked => BuildResult(participantsById[ranked.ParticipantId], ranked.Total, categories))
            .ToList();

        return new MatchFindResult(MatchFindOutcome.Found, new FoundMatch(
            Describe(tenantNames[match.TenantId], match.Name),
            match.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            match.ShortCode,
            results));
    }

    private static string Describe(string tenantName, string matchName) => $"{tenantName} - {matchName}";

    private static TimeOnly? TryParseTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static Dictionary<string, object?> BuildResult(MatchParticipant participant, int total, IReadOnlyList<CategoryProperty> categories)
    {
        var result = new Dictionary<string, object?>
        {
            ["name"] = participant.FullName,
            ["score"] = total,
            ["lidnummer"] = participant.FederationNumber
        };
        var participantCategories = participant.Categories;
        foreach (var category in categories)
        {
            if (participantCategories.TryGetValue(category.CategoryId, out var valueId) && category.ValueNames.TryGetValue(valueId, out var valueName))
                result[category.PropertyName] = valueName;
        }
        return result;
    }

    // Resolves each category of the tenant to its output property name (API name, else category name),
    // ordered by category name. Reserved names are dropped so they can never overwrite name/score/lidnummer,
    // and on a (case-insensitive) duplicate only the first category keeps the property.
    private async Task<IReadOnlyList<CategoryProperty>> LoadCategoryPropertiesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .Include(item => item.Values)
            .Where(item => item.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = new List<CategoryProperty>();
        foreach (var category in categories.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var propertyName = CategoryApiNames.Normalize(category.ApiName) ?? category.Name.Trim();
            if (propertyName.Length == 0 || CategoryApiNames.ReservedNames.Contains(propertyName) || !usedNames.Add(propertyName)) continue;
            properties.Add(new CategoryProperty(category.Id, propertyName, category.Values.ToDictionary(value => value.ValueId, value => value.Name)));
        }
        return properties;
    }

    private sealed record CategoryProperty(Guid CategoryId, string PropertyName, IReadOnlyDictionary<int, string> ValueNames);
}
