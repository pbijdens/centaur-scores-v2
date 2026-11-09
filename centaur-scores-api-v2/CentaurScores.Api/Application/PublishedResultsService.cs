using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CentaurScores.Api.Application;

/// <summary>Builds the anonymous published results (web UI /results/{slug}) for a match or a competition.</summary>
public interface IPublishedResultsService
{
    /// <summary>Looks the slug up across all tenants' matches and competitions; null when nothing is published under it.</summary>
    Task<PublishedResults?> FindAsync(string slug, CancellationToken cancellationToken);
    PublishedMatchResults BuildMatch(Match match, IReadOnlyList<Category> categories);
    PublishedCompetitionResults BuildCompetition(CompetitionExportData data);
}

public sealed class PublishedResultsService(ApplicationDbContext db, ILiveScoringService liveScoringService, ICompetitionService competitionService, IMemoryCache? cache = null) : IPublishedResultsService
{
    // Results (and misses) are cached briefly per slug, so many visitors refreshing the same page - or one caller hammering
    // it - cost one computation per window. Score changes and (un)publishing show up within this delay.
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    public async Task<PublishedResults?> FindAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = PublishedSlugs.Normalize(slug);
        if (normalized.Length == 0) return null;
        if (cache is null) return await LoadAsync(normalized, cancellationToken);

        var key = $"published-results:{normalized}";
        if (cache.TryGetValue(key, out PublishedResults? cached)) return cached;
        var results = await LoadAsync(normalized, cancellationToken);
        cache.Set(key, results, CacheDuration);
        return results;
    }

    private async Task<PublishedResults?> LoadAsync(string normalized, CancellationToken cancellationToken)
    {

        var match = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.Participants).ThenInclude(item => item.ParticipantListMember)
            .Include(item => item.LiveScopes)
            .SingleOrDefaultAsync(item => item.PublishedSlug == normalized, cancellationToken);
        if (match is not null)
        {
            var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == match.TenantId, cancellationToken);
            if (tenant is null) return null;
            var activeCount = match.IsOpen ? 1 : 0;
            // A match that is still active and publishes only finished results shows no results yet - just that it is in progress.
            if (match.IsOpen && !match.PublishedIncludesActiveMatches)
                return new PublishedResults("match", tenant.Name, tenant.LogoUrl, match.Name, match.Date, match.Date, false, activeCount, null, null);
            var categories = await LoadCategoriesAsync(match.TenantId, cancellationToken);
            return new PublishedResults("match", tenant.Name, tenant.LogoUrl, match.Name, match.Date, match.Date, true, activeCount, BuildMatch(match, categories), null);
        }

        var competitionId = await db.Competitions.AsNoTracking().Where(item => item.PublishedSlug == normalized).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (competitionId is null) return null;
        var loaded = await CompetitionResultsLoader.LoadAsync(db, competitionId.Value, null, cancellationToken);
        if (loaded is null) return null;
        var (competition, competitionCategories, matchesByRound) = loaded.Value;
        var competitionTenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == competition.TenantId, cancellationToken);
        if (competitionTenant is null) return null;
        var activeMatchCount = matchesByRound.Values.SelectMany(item => item).DistinctBy(item => item.Id).Count(item => item.IsOpen);
        var includeActive = competition.PublishedIncludesActiveMatches;
        // Leaving active matches out means they are treated as not shot yet: an archer's round shows "-" until the match closes.
        var counted = includeActive
            ? matchesByRound
            : matchesByRound.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Match>)pair.Value.Where(item => !item.IsOpen).ToList());
        var data = competitionService.BuildExport(competition, competitionCategories, counted);
        return new PublishedResults("competition", competitionTenant.Name, competitionTenant.LogoUrl, competition.Name, competition.StartDate, competition.EndDate, includeActive, activeMatchCount, null, BuildCompetition(data));
    }

    public PublishedMatchResults BuildMatch(Match match, IReadOnlyList<Category> categories)
    {
        var ungrouped = liveScoringService.BuildPublishedBlocks(match, [], categories).FirstOrDefault()?.Entries ?? [];

        // One grouping per distinct set of group-by categories over the match's narrowcast profiles: the profile names are
        // not shown, and profiles grouping on the same categories collapse into one. Profiles without grouping add nothing
        // beyond the ungrouped list, so they are skipped.
        var categoryById = categories.ToDictionary(item => item.Id);
        var groupings = new List<PublishedMatchGrouping>();
        var seen = new HashSet<string>();
        foreach (var scope in match.LiveScopes.OrderBy(item => item.Scope, StringComparer.OrdinalIgnoreCase))
        {
            var requested = (Deserialize<List<Guid>>(scope.GroupByCategoryIdsJson) ?? []).Where(categoryById.ContainsKey).ToList();
            var categoryIds = liveScoringService.OrderedGroupCategoryIds(match, requested);
            if (categoryIds.Count == 0 || !seen.Add(string.Join(',', categoryIds))) continue;
            groupings.Add(new PublishedMatchGrouping(
                categoryIds.Select(id => categoryById[id].Name).ToList(),
                liveScoringService.BuildPublishedBlocks(match, categoryIds, categories)));
        }

        return new PublishedMatchResults(match.Ends, match.ArrowsPerEnd, ungrouped, groupings);
    }

    public PublishedCompetitionResults BuildCompetition(CompetitionExportData data)
    {
        var f1RuleIndexes = data.Rules.Select((rule, index) => (rule, index)).Where(item => item.rule.Aggregation == "f1").Select(item => item.index).ToList();
        var f1RoundIds = f1RuleIndexes.SelectMany(index => Deserialize<List<Guid>>(data.Rules[index].RoundIdsJson) ?? []).ToHashSet();
        var usesF1 = f1RuleIndexes.Count > 0;

        PublishedCompetitionEntry ToEntry(CompetitionExportParticipant participant, string position) => new(
            position,
            participant.FederationNumber,
            participant.Name,
            CountingAverage(participant),
            participant.Total,
            participant.CountingScore,
            usesF1 ? f1RuleIndexes.Where(index => index < participant.Rules.Count).Sum(index => participant.Rules[index].Points) : null,
            data.Rounds.Select(round => participant.Rounds.TryGetValue(round.Id, out var score)
                ? new PublishedCompetitionRoundScore(round.Id, score.Score, f1RoundIds.Contains(round.Id) ? score.F1Points : null, participant.CountingRoundIds.Contains(round.Id))
                : new PublishedCompetitionRoundScore(round.Id, null, null, false)).ToList());

        // Participants arrive grouped and ranked exactly as on the results screen (BuildExport shares its Compute pass).
        var groups = data.Participants
            .GroupBy(item => item.Group)
            .Select(group => new PublishedCompetitionGroup(group.Key, group.Select(item => ToEntry(item, item.Position)).ToList()))
            .ToList();

        // The ungrouped list keeps each archer's per-group figures (f1 points are earned within the archer's own group, so
        // totals of different groups are not comparable). It is ordered on the average per arrow over the counting matches
        // (descending), then federation number (ascending), then full name. Positions follow that average - equal averages
        // share a position - and archers who did not qualify stay in that order but show "-" and take no position.
        var ordered = data.Participants
            .OrderByDescending(item => CountingAverage(item) ?? double.MinValue)
            .ThenBy(item => item.FederationNumber, FederationNumberComparer.Instance)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var ungrouped = new List<PublishedCompetitionEntry>();
        var rank = 0;
        double? previousAverage = null;
        var previousPosition = "";
        foreach (var participant in ordered)
        {
            if (!participant.Qualified)
            {
                ungrouped.Add(ToEntry(participant, "-"));
                continue;
            }
            rank++;
            var average = CountingAverage(participant);
            var position = rank > 1 && average == previousAverage ? previousPosition : rank.ToString();
            ungrouped.Add(ToEntry(participant, position));
            previousAverage = average;
            previousPosition = position;
        }

        var isGrouped = (Deserialize<List<Guid>>(data.Competition.GroupByCategoryIdsJson) ?? []).Count > 0;
        return new PublishedCompetitionResults(
            usesF1,
            isGrouped,
            data.Rounds.Select(round => new PublishedCompetitionRound(round.Id, round.ShortName, round.LongName)).ToList(),
            groups,
            ungrouped);
    }

    // Average per arrow over the counting matches, rounded as displayed (so archers who look equal also rank equal).
    private static double? CountingAverage(CompetitionExportParticipant participant) =>
        participant.CountingArrows > 0 ? Math.Round((double)participant.CountingScore / participant.CountingArrows, 2) : null;

    /// <summary>Federation numbers ascending: numerically when both are numbers, otherwise ordinally; missing numbers last.</summary>
    private sealed class FederationNumberComparer : IComparer<string?>
    {
        public static readonly FederationNumberComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var xMissing = string.IsNullOrWhiteSpace(x);
            var yMissing = string.IsNullOrWhiteSpace(y);
            if (xMissing || yMissing) return xMissing.CompareTo(yMissing);
            if (long.TryParse(x, out var xNumber) && long.TryParse(y, out var yNumber)) return xNumber.CompareTo(yNumber);
            return string.CompareOrdinal(x!.Trim(), y!.Trim());
        }
    }

    private async Task<List<Category>> LoadCategoriesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return default; }
    }
}

/// <summary>Loads a competition with everything results need: its categories and the matches of every round (participants, scores, members).</summary>
public static class CompetitionResultsLoader
{
    /// <param name="tenantId">The caller's tenant for authenticated use; null for the anonymous published results (the competition's own tenant is used).</param>
    public static async Task<(Competition Competition, List<Category> Categories, Dictionary<Guid, IReadOnlyList<Match>> MatchesByRound)?> LoadAsync(ApplicationDbContext db, Guid competitionId, Guid? tenantId, CancellationToken cancellationToken)
    {
        var competition = await db.Competitions.AsNoTracking()
            .Include(item => item.Rounds).ThenInclude(item => item.Matches)
            .Include(item => item.ScoringRules)
            .SingleOrDefaultAsync(item => item.Id == competitionId && (tenantId == null || item.TenantId == tenantId), cancellationToken);
        if (competition is null) return null;
        var ownerTenantId = competition.TenantId;
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == ownerTenantId).ToListAsync(cancellationToken);
        var matchIds = competition.Rounds.SelectMany(item => item.Matches).Select(item => item.MatchId).Distinct().ToList();
        var matches = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.Participants).ThenInclude(item => item.ParticipantListMember)
            .Where(item => matchIds.Contains(item.Id) && item.TenantId == ownerTenantId).ToListAsync(cancellationToken);
        var matchesById = matches.ToDictionary(item => item.Id);
        var matchesByRound = competition.Rounds.ToDictionary(
            round => round.Id,
            round => (IReadOnlyList<Match>)round.Matches.Select(item => matchesById.GetValueOrDefault(item.MatchId)).Where(item => item is not null).Cast<Match>().ToList());
        return (competition, categories, matchesByRound);
    }
}
