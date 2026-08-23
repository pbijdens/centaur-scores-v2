namespace CentaurScores.Api.Domain;

public static class MatchDefaults
{
    public const string KeyboardJson = "{\"categoryOrder\":[],\"keyboard\":[{\"keyId\":\"10\",\"label\":\"10\",\"color\":\"Yellow\",\"value\":10},{\"keyId\":\"9\",\"label\":\"9\",\"color\":\"Yellow\",\"value\":9},{\"keyId\":\"8\",\"label\":\"8\",\"color\":\"Red\",\"value\":8},{\"keyId\":\"7\",\"label\":\"7\",\"color\":\"Red\",\"value\":7},{\"keyId\":\"6\",\"label\":\"6\",\"color\":\"Blue\",\"value\":6},{\"keyId\":\"5\",\"label\":\"5\",\"color\":\"Blue\",\"value\":5},{\"keyId\":\"4\",\"label\":\"4\",\"color\":\"Black\",\"value\":4},{\"keyId\":\"3\",\"label\":\"3\",\"color\":\"Black\",\"value\":3},{\"keyId\":\"2\",\"label\":\"2\",\"color\":\"White\",\"value\":2},{\"keyId\":\"1\",\"label\":\"1\",\"color\":\"White\",\"value\":1},{\"keyId\":\"M\",\"label\":\"M\",\"color\":\"White\",\"value\":0}]}";
}

public abstract class TenantOwnedEntity
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
}

public sealed class Tenant
{
    public Guid Id { get; init; }
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public Guid? ParentTenantId { get; set; }
}

public sealed class Account : TenantOwnedEntity
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public AuthorizationProfile Authorization { get; set; } = AuthorizationProfile.Viewer;
}

public enum AuthorizationProfile { Viewer, Manager, Administrator }

public sealed class Category : TenantOwnedEntity
{
    public string Name { get; set; } = "";
    public bool IsUsed { get; set; }
    public List<CategoryValue> Values { get; set; } = [];
}

public sealed class CategoryValue : TenantOwnedEntity
{
    public Guid CategoryId { get; init; }
    public int ValueId { get; init; }
    public string Name { get; set; } = "";
}

public sealed class ParticipantList : TenantOwnedEntity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<ParticipantListMember> Members { get; set; } = [];
}

public sealed class ParticipantListMember : TenantOwnedEntity
{
    public Guid ParticipantListId { get; init; }
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? FederationNumber { get; set; }
    public Dictionary<Guid, int> Categories { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public sealed class MatchTemplate : TenantOwnedEntity
{
    public string Name { get; set; } = "";
    public Guid? ParticipantListId { get; set; }
    public bool AllowFreeParticipants { get; set; } = true;
    public string DeviceSelectionMode { get; set; } = "list-and-free";
    public string ConfigurationJson { get; set; } = MatchDefaults.KeyboardJson;
}

public sealed class Match : TenantOwnedEntity
{
    public string Name { get; set; } = "";
    public DateOnly Date { get; set; }
    public string? ShortCode { get; set; }
    public bool IsOpen { get; set; }
    public Guid? ParticipantListId { get; set; }
    public string DeviceSelectionMode { get; set; } = "list-and-free";
    public int Ends { get; set; }
    public int ArrowsPerEnd { get; set; }
    public int? GroupEnds { get; set; }
    public bool AllowFreeParticipants { get; set; } = true;
    public string KeyboardJson { get; set; } = MatchDefaults.KeyboardJson;
    public string ScoringRulesJson { get; set; } = "[]";
    public List<MatchParticipant> Participants { get; set; } = [];
    public List<ScoreDevice> Devices { get; set; } = [];
    public List<LiveScoreScope> LiveScopes { get; set; } = [];
}

public sealed class MatchParticipant : TenantOwnedEntity
{
    public Guid MatchId { get; init; }
    public Guid? ParticipantListMemberId { get; set; }
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? FederationNumber { get; set; }
    public Dictionary<Guid, int> Categories { get; set; } = [];
    public Guid? DeviceId { get; set; }
    public int? DeviceOrder { get; set; }
    public List<ArrowScore> Scores { get; set; } = [];
}

public sealed class ArrowScore : TenantOwnedEntity
{
    public Guid MatchParticipantId { get; init; }
    public int End { get; init; }
    public int Arrow { get; init; }
    public string KeyId { get; set; } = "";
    public int Value { get; set; }
}

public sealed class ScoreDevice : TenantOwnedEntity
{
    public Guid MatchId { get; init; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class LiveScoreScope : TenantOwnedEntity
{
    public Guid MatchId { get; init; }
    public string Scope { get; set; } = "all";
    public string GroupByCategoryIdsJson { get; set; } = "[]";
    public bool IncludeAverage { get; set; }
    public bool IncludeGroupScores { get; set; }
    public bool IncludeEqualizers { get; set; }
    public bool IncludePersonalBest { get; set; }
}

public sealed class Competition : TenantOwnedEntity
{
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string GroupByCategoryIdsJson { get; set; } = "[]";
    public List<CompetitionRound> Rounds { get; set; } = [];
    public List<CompetitionScoreRule> ScoringRules { get; set; } = [];
}

public sealed class CompetitionRound : TenantOwnedEntity
{
    public Guid CompetitionId { get; init; }
    public int Order { get; set; }
    public string ShortName { get; set; } = "";
    public string LongName { get; set; } = "";
    public List<CompetitionRoundMatch> Matches { get; set; } = [];
}

public sealed class CompetitionRoundMatch : TenantOwnedEntity
{
    public Guid CompetitionRoundId { get; init; }
    public Guid MatchId { get; init; }
}

public sealed class CompetitionScoreRule : TenantOwnedEntity
{
    public Guid CompetitionId { get; init; }
    public string Name { get; set; } = "";
    public string RoundIdsJson { get; set; } = "[]";
    public int HighestScores { get; set; }
    public int MinimumScores { get; set; }
    public string Aggregation { get; set; } = "total";
    public int SortOrder { get; set; }
}