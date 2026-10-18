using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

/// <summary>
/// Shared sign/unsign logic for the manager API and anonymous scorekeeper devices. The signed flag lives
/// on MatchParticipant; the images live in their own table (see MatchParticipantSignature for why), and a
/// row only exists when at least one image was captured. Neither method saves - the caller does.
/// </summary>
public static class ScorecardSigning
{
    public static async Task SignAsync(ApplicationDbContext db, MatchParticipant participant, string? archerSignatureDataUrl, string? markerSignatureDataUrl, CancellationToken cancellationToken)
    {
        participant.Signed = true;
        participant.SignedAtUtc = DateTime.UtcNow;
        var existing = await db.MatchParticipantSignatures.SingleOrDefaultAsync(item => item.MatchParticipantId == participant.Id, cancellationToken);
        if (archerSignatureDataUrl is null && markerSignatureDataUrl is null)
        {
            if (existing is not null) db.MatchParticipantSignatures.Remove(existing);
            return;
        }
        if (existing is null)
        {
            db.MatchParticipantSignatures.Add(new MatchParticipantSignature { MatchParticipantId = participant.Id, TenantId = participant.TenantId, ArcherSignatureDataUrl = archerSignatureDataUrl, MarkerSignatureDataUrl = markerSignatureDataUrl });
            return;
        }
        existing.ArcherSignatureDataUrl = archerSignatureDataUrl;
        existing.MarkerSignatureDataUrl = markerSignatureDataUrl;
    }

    // Withdrawing a signature is a full reset, not just a flag flip - the stored images are removed too,
    // so a subsequent re-sign never risks showing a stale image if it were re-signed without new ones.
    public static async Task UnsignAsync(ApplicationDbContext db, MatchParticipant participant, CancellationToken cancellationToken)
    {
        participant.Signed = false;
        participant.SignedAtUtc = null;
        if (await db.MatchParticipantSignatures.SingleOrDefaultAsync(item => item.MatchParticipantId == participant.Id, cancellationToken) is { } existing)
            db.MatchParticipantSignatures.Remove(existing);
    }
}
