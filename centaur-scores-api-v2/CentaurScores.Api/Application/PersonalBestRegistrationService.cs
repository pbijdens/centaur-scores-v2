using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

// What a match participant contributed to the personal bests before it was relinked: captured by the caller
// before changing the participant, because afterwards the old member's data is no longer reachable from it.
public sealed record PersonalBestParticipantSnapshot(Guid ParticipantListMemberId, string? FederationNumber, Dictionary<Guid, int> Categories, int Total)
{
    // Only list-linked participants ever register personal bests (see RegisterOnDeactivationAsync), so a free
    // participant has nothing to undo and yields null.
    public static PersonalBestParticipantSnapshot? Capture(MatchParticipant participant) =>
        participant.ParticipantListMemberId is { } memberId
            ? new(memberId, participant.ParticipantListMember?.FederationNumber, new Dictionary<Guid, int>(participant.Categories), participant.Scores.Sum(score => score.Value))
            : null;
}

// UC10: automatic personal-best registration whenever a match is deactivated. Called from
// MatchesController.SetOpen (single match, IsOpen true->false), DeactivateAll and ClaimScope, and - for
// one participant - when a closed match's participant is linked to a different list member.
public interface IPersonalBestRegistrationService
{
    Task RegisterOnDeactivationAsync(Match match, CancellationToken cancellationToken);

    // Re-processes one participant of a closed match after it was linked to a different (or no) list member:
    // withdraws what the previous member got from this match and registers the result for the new member.
    Task ReprocessParticipantAsync(Match match, Guid participantId, PersonalBestParticipantSnapshot? previous, CancellationToken cancellationToken);
}

public sealed class PersonalBestRegistrationService(ApplicationDbContext db, IPersonalBestContext personalBestContext, IPersonalBestEngine engine) : IPersonalBestRegistrationService
{
    private sealed record DisciplineMapping(Guid CategoryId, int ValueId, string Name);

    private sealed record RegistrationSetup(Guid OwningTenantId, string Classifier, IReadOnlyList<DisciplineMapping> Mappings);

    public async Task RegisterOnDeactivationAsync(Match match, CancellationToken cancellationToken)
    {
        var setup = await LoadSetupAsync(match, cancellationToken);
        if (setup is null) return;

        var participants = await db.MatchParticipants.AsNoTracking()
            .Include(item => item.Scores)
            .Include(item => item.ParticipantListMember)
            .Where(item => item.MatchId == match.Id && item.TenantId == match.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants)
        {
            await RegisterAsync(setup, match, participant, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReprocessParticipantAsync(Match match, Guid participantId, PersonalBestParticipantSnapshot? previous, CancellationToken cancellationToken)
    {
        if (match.IsOpen) return;
        var setup = await LoadSetupAsync(match, cancellationToken);
        if (setup is null) return;

        if (previous is not null) await WithdrawAsync(setup, match, previous, cancellationToken);

        var participant = await db.MatchParticipants.AsNoTracking()
            .Include(item => item.Scores)
            .Include(item => item.ParticipantListMember)
            .SingleOrDefaultAsync(item => item.Id == participantId && item.MatchId == match.Id && item.TenantId == match.TenantId, cancellationToken);
        if (participant is not null) await RegisterAsync(setup, match, participant, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    // The match only takes part in personal-best registration when the feature is enabled for its tenant
    // (here or on an ancestor), the match has an existing classifier, and disciplines map this tenant's categories.
    private async Task<RegistrationSetup?> LoadSetupAsync(Match match, CancellationToken cancellationToken)
    {
        var owningTenantId = await personalBestContext.ResolveOwningTenantIdAsync(match.TenantId, cancellationToken);
        if (owningTenantId is null) return null;
        if (string.IsNullOrWhiteSpace(match.PersonalBestClassifier)) return null;

        var classifierExists = await db.PersonalBestClassifiers.AnyAsync(item => item.TenantId == owningTenantId && item.Name == match.PersonalBestClassifier, cancellationToken);
        if (!classifierExists) return null;

        var mappings = await (
            from mapping in db.PersonalBestDisciplineMappings
            join discipline in db.PersonalBestDisciplines on mapping.DisciplineId equals discipline.Id
            where mapping.TenantId == owningTenantId && mapping.SourceTenantId == match.TenantId
            select new DisciplineMapping(mapping.CategoryId, mapping.ValueId, discipline.Name)
        ).ToListAsync(cancellationToken);
        if (mappings.Count == 0) return null;

        return new(owningTenantId.Value, match.PersonalBestClassifier, mappings);
    }

    // Exactly one discipline must match the participant's categories; otherwise nothing is registered.
    private static string? ResolveDiscipline(RegistrationSetup setup, Dictionary<Guid, int> categories)
    {
        var matched = setup.Mappings
            .Where(mapping => categories.TryGetValue(mapping.CategoryId, out var valueId) && valueId == mapping.ValueId)
            .Select(mapping => mapping.Name)
            .Distinct()
            .ToList();
        return matched.Count == 1 ? matched[0] : null;
    }

    private async Task RegisterAsync(RegistrationSetup setup, Match match, MatchParticipant participant, CancellationToken cancellationToken)
    {
        // Only the moderated participant list counts: free entries are typed in by anyone on a scoring device.
        // The number is read from the list member itself, never through MatchParticipant.FederationNumber,
        // which falls back to the participant's own (unmoderated, or restored-from-backup) number.
        if (participant.ParticipantListMemberId is null) return;
        var member = participant.ParticipantListMember;
        if (member is null || string.IsNullOrWhiteSpace(member.FederationNumber)) return;
        var discipline = ResolveDiscipline(setup, participant.Categories);
        if (discipline is null) return;

        var federationNumber = member.FederationNumber;
        var name = string.IsNullOrWhiteSpace(member.FullName) ? member.LastName : member.FullName;
        await engine.EnsureArcherNameAsync(setup.OwningTenantId, federationNumber, name, cancellationToken);

        var total = participant.Scores.Sum(score => score.Value);
        await engine.TryInsertAsync(setup.OwningTenantId, federationNumber, discipline, setup.Classifier, total, match.Date, DateTime.UtcNow, "automatic", cancellationToken);
    }

    // Removes the automatic entry this match registered for the previous member, then re-registers that
    // member's later closed matches: registering this match may have pruned later, lower entries (the log
    // only keeps improvements), and those come back this way. Pruned imported entries cannot be restored.
    private async Task WithdrawAsync(RegistrationSetup setup, Match match, PersonalBestParticipantSnapshot previous, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previous.FederationNumber)) return;
        var discipline = ResolveDiscipline(setup, previous.Categories);
        if (discipline is null) return;

        var registered = await db.PersonalBestLogEntries
            .Where(item => item.TenantId == setup.OwningTenantId
                && item.FederationNumber == previous.FederationNumber
                && item.Discipline == discipline
                && item.MatchClassifier == setup.Classifier
                && item.Date == match.Date
                && item.Score == previous.Total
                && item.Source == "automatic")
            .ToListAsync(cancellationToken);
        if (registered.Count == 0) return;
        db.PersonalBestLogEntries.RemoveRange(registered);
        await db.SaveChangesAsync(cancellationToken);

        var laterMatches = await (
            from participant in db.MatchParticipants
            join other in db.Matches on participant.MatchId equals other.Id
            where participant.ParticipantListMemberId == previous.ParticipantListMemberId
                && other.Id != match.Id
                && !other.IsOpen
                && other.Date > match.Date
            orderby other.Date
            select other
        ).AsNoTracking().ToListAsync(cancellationToken);

        foreach (var other in laterMatches)
        {
            var otherSetup = await LoadSetupAsync(other, cancellationToken);
            if (otherSetup is null) continue;
            var otherParticipant = await db.MatchParticipants.AsNoTracking()
                .Include(item => item.Scores)
                .Include(item => item.ParticipantListMember)
                .SingleAsync(item => item.MatchId == other.Id && item.ParticipantListMemberId == previous.ParticipantListMemberId, cancellationToken);
            await RegisterAsync(otherSetup, other, otherParticipant, cancellationToken);
            // Saved per match so the next TryInsertAsync sees the entries this one added.
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
