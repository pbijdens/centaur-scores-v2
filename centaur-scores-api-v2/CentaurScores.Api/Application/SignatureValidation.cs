using CentaurScores.Api.Contracts;

namespace CentaurScores.Api.Application;

/// <summary>
/// Shared validation for the archer/marker signature images stored on MatchParticipant (see
/// documentation/SIGNING-SCORECARDS.md). Images are base64 data-URL strings (same convention as
/// Tenant.LogoUrl); the spec estimates well under 64KB even at 300dpi, so a generous cap here is
/// purely a defensive guard against an abusive/broken client bloating the database, not a real limit.
/// </summary>
public static class SignatureValidation
{
    public const int MaxDataUrlLength = 200_000;

    public static ApiError? Validate(string? archerSignatureDataUrl, string? markerSignatureDataUrl) =>
        (archerSignatureDataUrl?.Length ?? 0) > MaxDataUrlLength || (markerSignatureDataUrl?.Length ?? 0) > MaxDataUrlLength
            ? new ApiError("SIGNATURE_TOO_LARGE", $"A signature image exceeds the {MaxDataUrlLength} character limit.")
            : null;
}
