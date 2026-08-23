using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CentaurScores.Api.Application;

public sealed record ScorekeeperContext(Match Match, ScoreDevice Device);

public interface IScorekeeperService
{
    Task<ScorekeeperContext?> FindAsync(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken);
    Task<ScorekeeperMatch> GetMatchAsync(ScorekeeperContext context, CancellationToken cancellationToken);
    Task<ApiError?> SetParticipantsAsync(ScorekeeperContext context, IReadOnlyList<ScorekeeperParticipantRequest> request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScorekeeperScoreUpdateResult>> UpdateScoresAsync(ScorekeeperContext context, IReadOnlyList<ScorekeeperScoreUpdates> request, CancellationToken cancellationToken);
    Task<ScorekeeperParticipantOptions> GetParticipantOptionsAsync(ScorekeeperContext context, CancellationToken cancellationToken);
}

public sealed class ScorekeeperService(ApplicationDbContext db) : IScorekeeperService
{
    public async Task<ScorekeeperContext?> FindAsync(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken)
    {
        var match = await db.Matches
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.Devices)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == matchId, cancellationToken);
        var device = match?.Devices.SingleOrDefault(item => item.Id == deviceId);
        return match is null || device is null ? null : new(match, device);
    }

    public async Task<ScorekeeperMatch> GetMatchAsync(ScorekeeperContext context, CancellationToken cancellationToken)
    {
        var categories = await CategoriesAsync(context.Match, cancellationToken);
        var keyboard = ParseKeyboard(context.Match.KeyboardJson).Keys;
        var participants = context.Match.Participants
            .Where(item => item.DeviceId == context.Device.Id)
            .OrderBy(item => item.DeviceOrder).ThenBy(item => item.LastName)
            .Select(item => ToMatchParticipant(item, context.Match, categories))
            .ToList();
        return new(context.Device.Name, context.Match.Name, context.Match.Ends, context.Match.ArrowsPerEnd, context.Match.GroupEnds,
            categories, context.Match.DeviceSelectionMode != "restricted", context.Match.AllowFreeParticipants, keyboard, participants);
    }

    public async Task<ApiError?> SetParticipantsAsync(ScorekeeperContext context, IReadOnlyList<ScorekeeperParticipantRequest> request, CancellationToken cancellationToken)
    {
        if (context.Match.DeviceSelectionMode == "restricted")
            return new("PARTICIPANT_LIST_FIXED", "Participants cannot be modified for this match.");

        var categories = await CategoriesAsync(context.Match, cancellationToken);
        var members = context.Match.ParticipantListId is { } listId
            ? await db.ParticipantListMembers.Where(item => item.TenantId == context.Match.TenantId && item.ParticipantListId == listId && item.IsActive).ToDictionaryAsync(item => item.Id, cancellationToken)
            : new Dictionary<Guid, ParticipantListMember>();
        var byId = context.Match.Participants.ToDictionary(item => item.Id);
        var wanted = new HashSet<Guid>();

        for (var index = 0; index < request.Count; index++)
        {
            var item = request[index];
            MatchParticipant participant;
            if (item.TenantParticipantId is { } tenantParticipantId)
            {
                if (!members.TryGetValue(tenantParticipantId, out var member))
                    return new("CUSTOM_PARTICIPANT_NOT_ALLOWED", "The participant is not in the match participant list.");
                participant = byId.Values.FirstOrDefault(value => value.ParticipantListMemberId == member.Id)
                    ?? new MatchParticipant { Id = Guid.NewGuid(), TenantId = context.Match.TenantId, MatchId = context.Match.Id, ParticipantListMemberId = member.Id };
                if (!byId.ContainsKey(participant.Id))
                {
                    db.MatchParticipants.Add(participant);
                    byId[participant.Id] = participant;
                }
                participant.LastName = member.LastName;
                participant.FullName = member.FullName;
                participant.FederationNumber = member.FederationNumber;
                participant.Categories = member.Categories;
            }
            else if (item.MatchParticipantId is { } matchParticipantId && byId.TryGetValue(matchParticipantId, out participant!))
            {
                if (participant.ParticipantListMemberId is not null && !SameValues(participant, item, categories))
                    return new("PARTICIPANT_UPDATE_NOT_ALLOWED", "List participants cannot be changed through this endpoint.");
                if (participant.ParticipantListMemberId is null)
                    ApplyValues(participant, item, categories);
            }
            else
            {
                if (!context.Match.AllowFreeParticipants)
                    return new("CUSTOM_PARTICIPANT_NOT_ALLOWED", "Custom participants are not allowed for this match.");
                participant = new MatchParticipant { Id = Guid.NewGuid(), TenantId = context.Match.TenantId, MatchId = context.Match.Id };
                ApplyValues(participant, item, categories);
                db.MatchParticipants.Add(participant);
                byId[participant.Id] = participant;
            }
            wanted.Add(participant.Id);
            participant.DeviceId = context.Device.Id;
            participant.DeviceOrder = index;
        }

        var participantsToUnassign = context.Match.Participants
            .Where(item => item.DeviceId == context.Device.Id && !wanted.Contains(item.Id))
            .ToList();
        foreach (var participant in participantsToUnassign)
        {
            participant.DeviceId = null;
            participant.DeviceOrder = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<IReadOnlyList<ScorekeeperScoreUpdateResult>> UpdateScoresAsync(ScorekeeperContext context, IReadOnlyList<ScorekeeperScoreUpdates> request, CancellationToken cancellationToken)
    {
        var conflicts = new List<ScorekeeperScoreUpdateResult>();
        var keys = ParseKeyboard(context.Match.KeyboardJson).Keys;
        foreach (var batch in request)
        {
            var participant = context.Match.Participants.SingleOrDefault(item => item.Id == batch.MatchParticipantId && item.DeviceId == context.Device.Id);
            if (participant is null)
            {
                conflicts.Add(new(batch.MatchParticipantId, "PARTICIPANT_CONFLICT", []));
                continue;
            }
            var participantConflicts = new List<ScorekeeperScoreConflict>();
            foreach (var update in batch.Updates)
            {
                if (update.Index < 0 || update.Index >= context.Match.Ends * context.Match.ArrowsPerEnd)
                {
                    participantConflicts.Add(new(update.Index, null, update.Old, update.New));
                    continue;
                }
                var end = update.Index / context.Match.ArrowsPerEnd + 1;
                var arrow = update.Index % context.Match.ArrowsPerEnd + 1;
                var score = participant.Scores.SingleOrDefault(item => item.End == end && item.Arrow == arrow);
                var current = score?.KeyId;
                if (current != update.New && current != update.Old)
                {
                    participantConflicts.Add(new(update.Index, current, update.Old, update.New));
                    continue;
                }
                if (current == update.New)
                    continue;
                var key = keys.SingleOrDefault(item => item.Id == update.New);
                if (key is null && update.New is not null)
                {
                    participantConflicts.Add(new(update.Index, current, update.Old, update.New));
                    continue;
                }
                if (score is null)
                {
                    score = new ArrowScore { Id = Guid.NewGuid(), TenantId = context.Match.TenantId, MatchParticipantId = participant.Id, End = end, Arrow = arrow };
                    db.ArrowScores.Add(score);
                }
                score.KeyId = key?.Id ?? "";
                score.Value = key?.Value ?? 0;
            }
            if (participantConflicts.Count > 0)
                conflicts.Add(new(batch.MatchParticipantId, "SCORE_CONFLICT", participantConflicts));
        }
        await db.SaveChangesAsync(cancellationToken);
        return conflicts;
    }

    public async Task<ScorekeeperParticipantOptions> GetParticipantOptionsAsync(ScorekeeperContext context, CancellationToken cancellationToken)
    {
        var categories = await CategoriesAsync(context.Match, cancellationToken);
        var all = context.Match.Participants.OrderBy(item => item.DeviceOrder).ThenBy(item => item.LastName).Select(item => ToInfo(item, categories)).ToList();
        var assigned = all.Where(item => context.Match.Participants.Single(p => p.Id == item.MatchParticipantId).DeviceId == context.Device.Id).ToList();
        var unassigned = all.Where(item => context.Match.Participants.Single(p => p.Id == item.MatchParticipantId).DeviceId is null).ToList();
        var used = context.Match.Participants.Where(item => item.ParticipantListMemberId is not null).Select(item => item.ParticipantListMemberId!.Value).ToHashSet();
        var potential = context.Match.ParticipantListId is { } listId
            ? await db.ParticipantListMembers.AsNoTracking().Where(item => item.ParticipantListId == listId && item.TenantId == context.Match.TenantId && item.IsActive && !used.Contains(item.Id)).OrderBy(item => item.LastName).Select(item => new ScorekeeperParticipantInfo(null, item.Id, item.FederationNumber, item.FullName, null, new List<ScorekeeperParticipantCategory>())).ToListAsync(cancellationToken)
            : [];
        for (var index = 0; index < potential.Count; index++)
        {
            var item = potential[index];
            var member = await db.ParticipantListMembers.AsNoTracking().SingleAsync(value => value.Id == item.TenantParticipantId, cancellationToken);
            var info = Info(member.Categories, categories);
            potential[index] = item with { Info = info, Categories = CategoryValues(member.Categories, categories) };
        }
        return new(unassigned, assigned, potential);
    }

    private async Task<List<ScorekeeperCategory>> CategoriesAsync(Match match, CancellationToken cancellationToken)
    {
        var order = ParseKeyboard(match.KeyboardJson).CategoryOrder;
        var all = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == match.TenantId).ToListAsync(cancellationToken);
        return order.Concat(all.Select(item => item.Id)).Distinct().Select(id => all.SingleOrDefault(item => item.Id == id)).Where(item => item is not null).Select(item => new ScorekeeperCategory(item!.Id, item.Name, item.Values.OrderBy(value => value.ValueId).Select(value => new ScorekeeperCategoryValue(value.ValueId, value.Name)).ToList())).ToList();
    }

    private static ScorekeeperMatchParticipant ToMatchParticipant(MatchParticipant item, Match match, IReadOnlyList<ScorekeeperCategory> categories) => new(item.FederationNumber, item.FullName, Info(item.Categories, categories), CategoryValues(item.Categories, categories), item.Id, item.ParticipantListMemberId, null, Enumerable.Range(0, match.Ends * match.ArrowsPerEnd).Select(index => item.Scores.SingleOrDefault(score => (score.End - 1) * match.ArrowsPerEnd + score.Arrow - 1 == index)?.KeyId).ToList());
    private static ScorekeeperParticipantInfo ToInfo(MatchParticipant item, IReadOnlyList<ScorekeeperCategory> categories) => new(item.Id, item.ParticipantListMemberId, item.FederationNumber, item.FullName, Info(item.Categories, categories), CategoryValues(item.Categories, categories));
    private static IReadOnlyList<ScorekeeperParticipantCategory> CategoryValues(Dictionary<Guid, int> values, IReadOnlyList<ScorekeeperCategory> categories) => categories.Select(category => new ScorekeeperParticipantCategory(category.Id, category.Name, category.Values.FirstOrDefault(value => values.GetValueOrDefault(category.Id) == value.Id)?.Name)).ToList();
    private static string? Info(Dictionary<Guid, int> values, IReadOnlyList<ScorekeeperCategory> categories) { var text = string.Join(" / ", CategoryValues(values, categories).Where(item => item.Value is not null).Select(item => item.Value)); return text.Length == 0 ? null : text; }
    private static void ApplyValues(MatchParticipant participant, ScorekeeperParticipantRequest item, IReadOnlyList<ScorekeeperCategory> categories) { participant.FederationNumber = item.FederationNumber; participant.FullName = item.Name ?? ""; participant.LastName = item.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? ""; participant.Categories = item.Categories?.Where(category => category.Value is not null).ToDictionary(category => category.Id, category => categories.SingleOrDefault(value => value.Id == category.Id)?.Values.FirstOrDefault(value => value.Name == category.Value)?.Id ?? 0) ?? []; }
    private static bool SameValues(MatchParticipant participant, ScorekeeperParticipantRequest item, IReadOnlyList<ScorekeeperCategory> categories) => participant.FederationNumber == item.FederationNumber && participant.FullName == item.Name && participant.Categories.SequenceEqual(item.Categories?.Where(category => category.Value is not null).ToDictionary(category => category.Id, category => categories.SingleOrDefault(value => value.Id == category.Id)?.Values.FirstOrDefault(value => value.Name == category.Value)?.Id ?? 0) ?? []);
    private static (List<Guid> CategoryOrder, List<ScorekeeperKey> Keys) ParseKeyboard(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var order = root.TryGetProperty("categoryOrder", out var categories) ? categories.EnumerateArray().Where(item => Guid.TryParse(item.GetString(), out _)).Select(item => item.GetGuid()).ToList() : [];
            var keys = root.TryGetProperty("keyboard", out var keyboard) ? keyboard.EnumerateArray().Select(item => new ScorekeeperKey(item.GetProperty("keyId").GetString() ?? "", item.GetProperty("label").GetString() ?? "", item.TryGetProperty("value", out var value) ? value.GetInt32() : 0, item.TryGetProperty("color", out var color) ? color.GetString() ?? "White" : "White")).ToList() : [];
            return (order, keys);
        }
        catch (JsonException) { return ([], []); }
    }
}
