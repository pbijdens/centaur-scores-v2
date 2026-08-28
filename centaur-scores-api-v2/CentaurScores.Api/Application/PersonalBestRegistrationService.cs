using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

// UC10: automatic personal-best registration whenever a match is deactivated. Called from
// MatchesController.Update (single match, IsOpen true->false) and MatchesController.DeactivateAll.
public interface IPersonalBestRegistrationService
{
    Task RegisterOnDeactivationAsync(Match match, CancellationToken cancellationToken);
}

public sealed class PersonalBestRegistrationService(ApplicationDbContext db, IPersonalBestContext personalBestContext, IPersonalBestEngine engine) : IPersonalBestRegistrationService
{
    public async Task RegisterOnDeactivationAsync(Match match, CancellationToken cancellationToken)
    {
        var owningTenantId = await personalBestContext.ResolveOwningTenantIdAsync(match.TenantId, cancellationToken);
        if (owningTenantId is null) return;
        if (string.IsNullOrWhiteSpace(match.PersonalBestClassifier)) return;

        var classifierExists = await db.PersonalBestClassifiers.AnyAsync(item => item.TenantId == owningTenantId && item.Name == match.PersonalBestClassifier, cancellationToken);
        if (!classifierExists) return;

        var mappings = await (
            from mapping in db.PersonalBestDisciplineMappings
            join discipline in db.PersonalBestDisciplines on mapping.DisciplineId equals discipline.Id
            where mapping.TenantId == owningTenantId && mapping.SourceTenantId == match.TenantId
            select new { mapping.CategoryId, mapping.ValueId, discipline.Name }
        ).ToListAsync(cancellationToken);
        if (mappings.Count == 0) return;

        var participants = await db.MatchParticipants.AsNoTracking()
            .Include(item => item.Scores)
            .Where(item => item.MatchId == match.Id && item.TenantId == match.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants)
        {
            if (participant.ParticipantListMemberId is null) continue;
            if (string.IsNullOrWhiteSpace(participant.FederationNumber)) continue;

            var matchedDisciplines = mappings
                .Where(mapping => participant.Categories.TryGetValue(mapping.CategoryId, out var valueId) && valueId == mapping.ValueId)
                .Select(mapping => mapping.Name)
                .Distinct()
                .ToList();
            if (matchedDisciplines.Count != 1) continue;

            var federationNumber = participant.FederationNumber!;
            var name = string.IsNullOrWhiteSpace(participant.FullName) ? participant.LastName : participant.FullName;
            await engine.EnsureArcherNameAsync(owningTenantId.Value, federationNumber, name, cancellationToken);

            var total = participant.Scores.Sum(score => score.Value);
            await engine.TryInsertAsync(owningTenantId.Value, federationNumber, matchedDisciplines[0], match.PersonalBestClassifier!, total, match.Date, DateTime.UtcNow, "automatic", cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
