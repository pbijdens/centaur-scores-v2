using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CentaurScores.Api.Application;

/// <summary>
/// Slugs under which match or competition results are published anonymously (web UI route /results/{slug}).
/// One slug namespace is shared by all tenants, matches and competitions, since the public URL carries nothing else.
/// </summary>
public static class PublishedSlugs
{
    public const int MaxLength = 100;

    /// <summary>
    /// Lowercases, strips diacritics and turns every run of characters outside [a-z0-9] into a single dash,
    /// without leading/trailing dashes: "Clubkampioenschap Één 2026!" becomes "clubkampioenschap-een-2026".
    /// Returns an empty string when nothing usable is left. Keep in sync with normalizeSlug in the web UI.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingDash = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && builder.Length > 0) builder.Append('-');
                pendingDash = false;
                builder.Append(character);
            }
            else
            {
                pendingDash = true;
            }
        }
        var slug = builder.ToString();
        return slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
    }

    /// <summary>True when another match or competition (in any tenant) already publishes its results under the slug.</summary>
    public static async Task<bool> IsTakenAsync(ApplicationDbContext db, string slug, Guid? exceptMatchId, Guid? exceptCompetitionId, CancellationToken cancellationToken)
    {
        var takenByMatch = await db.Matches.AnyAsync(item => item.PublishedSlug == slug && item.Id != exceptMatchId, cancellationToken);
        if (takenByMatch) return true;
        return await db.Competitions.AnyAsync(item => item.PublishedSlug == slug && item.Id != exceptCompetitionId, cancellationToken);
    }

    /// <summary>
    /// Validates a publication change for one match or competition. Returns the slug to store (null = unpublish), or a coded
    /// error: PUBLISHED_SLUG_INVALID (publishing with a slug that normalizes to nothing) or PUBLISHED_SLUG_TAKEN.
    /// </summary>
    public static async Task<(string? Slug, ApiError? Error)> ResolveAsync(ApplicationDbContext db, UpdatePublicationRequest request, Guid? matchId, Guid? competitionId, CancellationToken cancellationToken)
    {
        if (!request.Published) return (null, null);
        var slug = Normalize(request.Slug);
        if (slug.Length == 0) return (null, new ApiError(InvalidCode, "The slug must contain at least one letter or digit."));
        if (await IsTakenAsync(db, slug, matchId, competitionId, cancellationToken)) return (null, new ApiError(TakenCode, "Another match or competition already publishes its results under this slug."));
        return (slug, null);
    }

    public const string InvalidCode = "PUBLISHED_SLUG_INVALID";
    public const string TakenCode = "PUBLISHED_SLUG_TAKEN";
}
