using CentaurScores.Api.Contracts;

namespace CentaurScores.Api.Application;

// Rules for Category.ApiName, the property name a category gets in the external results endpoint
// (POST api/matches/find, see documentation/FIND-MATCHES.md).
public static class CategoryApiNames
{
    public const int MaxLength = 64;

    // Fixed properties of every find-endpoint result row. A category may never resolve to one of these
    // names, otherwise it would overwrite the participant's name, score or federation number.
    public static readonly IReadOnlySet<string> ReservedNames = new HashSet<string>(["name", "score", "lidnummer"], StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? apiName) => string.IsNullOrWhiteSpace(apiName) ? null : apiName.Trim();

    // Validates an already normalized API name; returns null when it is acceptable.
    public static ApiError? Validate(string? apiName)
    {
        if (apiName is null) return null;
        if (apiName.Length > MaxLength) return new ApiError("CATEGORY_API_NAME_INVALID", $"The API name can be at most {MaxLength} characters.");
        if (ReservedNames.Contains(apiName)) return new ApiError("CATEGORY_API_NAME_RESERVED", $"'{apiName}' is a reserved API name.");
        return null;
    }
}
