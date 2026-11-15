using System.Security.Cryptography;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

// Temporary federation numbers ("CS" + 10 random digits) for archers that have no real federation number
// yet. A participant list member created without a usable number (empty, all spaces, or zero) gets one
// automatically, so personal bests and imports can always key on a number. Only the moderated participant
// lists get these: free match participants (entered by anyone on a scoring device) keep exactly what was
// typed and never take part in personal-best registration, so they get no number until they are added to
// a list. Uniqueness is system wide - across every tenant's participant lists and personal-best data - not
// per tenant, because personal-best data can be owned by a super tenant and lists move via backup/restore.
public interface IFederationNumberService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);

    // Returns the value unchanged when it is a usable number, otherwise a freshly generated temporary one.
    Task<string> EnsureAsync(string? federationNumber, CancellationToken cancellationToken);
}

public sealed class FederationNumberService(ApplicationDbContext db) : IFederationNumberService
{
    public const string TemporaryPrefix = "CS";
    private const int DigitCount = 10;
    private const int MaxAttempts = 50;

    // Numbers handed out by this (scoped) instance but possibly not saved yet - e.g. several new rows of
    // one import - so two of them can never collide before SaveChanges makes them visible to the queries.
    private readonly HashSet<string> issued = new(StringComparer.OrdinalIgnoreCase);

    // Empty, all spaces, or a number consisting of zeros only ("0", "0000").
    public static bool IsMissing(string? federationNumber)
    {
        if (string.IsNullOrWhiteSpace(federationNumber)) return true;
        var trimmed = federationNumber.Trim();
        return trimmed.All(character => character == '0');
    }

    public async Task<string> EnsureAsync(string? federationNumber, CancellationToken cancellationToken) =>
        IsMissing(federationNumber) ? await GenerateAsync(cancellationToken) : federationNumber!;

    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        // With 10^10 candidates a collision is very unlikely; the bounded loop only guards against a broken RNG.
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = TemporaryPrefix + RandomDigits();
            if (issued.Contains(candidate)) continue;
            if (await IsInUseAsync(candidate, cancellationToken)) continue;
            issued.Add(candidate);
            return candidate;
        }
        throw new InvalidOperationException("Could not generate an unused temporary federation number.");
    }

    private async Task<bool> IsInUseAsync(string candidate, CancellationToken cancellationToken) =>
        await db.ParticipantListMembers.AnyAsync(item => item.FederationNumber == candidate, cancellationToken)
        || await db.PersonalBestLogEntries.AnyAsync(item => item.FederationNumber == candidate, cancellationToken)
        || await db.PersonalBestArcherNames.AnyAsync(item => item.FederationNumber == candidate, cancellationToken)
        || await db.PersonalBestImportConflicts.AnyAsync(item => item.FederationNumber == candidate, cancellationToken);

    private static string RandomDigits()
    {
        var digits = new char[DigitCount];
        for (var index = 0; index < DigitCount; index++)
        {
            digits[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }
        return new string(digits);
    }
}
