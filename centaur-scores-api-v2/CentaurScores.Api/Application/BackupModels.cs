namespace CentaurScores.Api.Application;

// JSON shapes written into/read from a backup ZIP. These are deliberately separate from the Domain.cs
// entities (not raw EF entities) so the on-disk format doesn't silently change if the entities gain
// navigation properties or EF-only metadata later.

public sealed record BackupIndex(int FormatVersion, DateTime ExportedAt, Guid RootTenantId, List<Guid> TenantIds, bool IncludeSubTenants, int DroppedPersonalBestDisciplineMappings, string GeneratorVersion);

public sealed record TenantBackup(Guid Id, string Name, string? LogoUrl, Guid? ParentTenantId, bool PersonalBestEnabled);

public sealed record AccountBackup(Guid Id, Guid TenantId, string Username, string PasswordHash, string? DisplayName, string? Email, string Authorization);

public sealed record CategoryValueBackup(int ValueId, string Name);
public sealed record CategoryBackup(Guid Id, Guid TenantId, string Name, bool IsUsed, List<CategoryValueBackup> Values);

public sealed record ParticipantListMemberBackup(Guid Id, string LastName, string FullName, string? FederationNumber, Dictionary<Guid, int> Categories, bool IsActive);
public sealed record ParticipantListBackup(Guid Id, Guid TenantId, string Name, bool IsActive, List<ParticipantListMemberBackup> Members);

public sealed record MatchTemplateBackup(Guid Id, Guid TenantId, string Name, Guid? ParticipantListId, bool AllowFreeParticipants, string DeviceSelectionMode, string ConfigurationJson, string? PersonalBestClassifier);

public sealed record ArrowScoreBackup(int End, int Arrow, string KeyId, int Value);
public sealed record MatchParticipantBackup(Guid Id, Guid? ParticipantListMemberId, string LastName, string FullName, string? FederationNumber, Dictionary<Guid, int> Categories, Guid? DeviceId, int? DeviceOrder, List<ArrowScoreBackup> Scores);
public sealed record ScoreDeviceBackup(Guid Id, string Name, int SortOrder);
public sealed record LiveScoreScopeBackup(Guid Id, string Scope, string GroupByCategoryIdsJson, bool IncludeAverage, bool IncludeGroupScores, bool IncludeEqualizers, bool IncludePersonalBest);
public sealed record MatchBackup(
    Guid Id, Guid TenantId, string Name, DateOnly Date, string? ShortCode, bool IsOpen, Guid? ParticipantListId,
    string DeviceSelectionMode, int Ends, int ArrowsPerEnd, int? GroupEnds, bool AllowFreeParticipants,
    string KeyboardJson, string ScoringRulesJson, string? PersonalBestClassifier,
    List<MatchParticipantBackup> Participants, List<ScoreDeviceBackup> Devices, List<LiveScoreScopeBackup> LiveScopes);

public sealed record CompetitionRoundMatchBackup(Guid MatchId);
public sealed record CompetitionRoundBackup(Guid Id, int Order, string ShortName, string LongName, List<CompetitionRoundMatchBackup> Matches);
public sealed record CompetitionScoreRuleBackup(string Name, string RoundIdsJson, int HighestScores, int MinimumScores, string Aggregation, int SortOrder);
public sealed record CompetitionBackup(Guid Id, Guid TenantId, string Name, DateOnly StartDate, DateOnly EndDate, string GroupByCategoryIdsJson, List<CompetitionRoundBackup> Rounds, List<CompetitionScoreRuleBackup> ScoringRules);

public sealed record PersonalBestClassifierBackup(string Name);
public sealed record PersonalBestDisciplineMappingBackup(Guid SourceTenantId, Guid CategoryId, int ValueId);
public sealed record PersonalBestDisciplineBackup(string Name, List<PersonalBestDisciplineMappingBackup> Mappings);
public sealed record PersonalBestExportColumnBackup(int SortOrder, string ColumnName, string Field, string? DateFormat);
public sealed record PersonalBestExportConfigBackup(string ExportMode, string TableName, List<PersonalBestExportColumnBackup> Columns);
public sealed record PersonalBestImportConfigBackup(string TableName, string DateColumn, string FederationNumberColumn, string NameColumn, string DisciplineColumn, string MatchClassifierColumn, string ScoreColumn, string UpdateDateColumn);
public sealed record PersonalBestArcherNameBackup(string FederationNumber, string Name);
public sealed record PersonalBestLogEntryBackup(string FederationNumber, string Discipline, string MatchClassifier, int Score, DateOnly Date, DateTime RecordedAt, string Source);
public sealed record PersonalBestInfoBackup(
    Guid TenantId,
    List<PersonalBestClassifierBackup> Classifiers,
    List<PersonalBestDisciplineBackup> Disciplines,
    PersonalBestExportConfigBackup? ExportConfig,
    PersonalBestImportConfigBackup? ImportConfig,
    List<PersonalBestArcherNameBackup> ArcherNames,
    List<PersonalBestLogEntryBackup> LogEntries);
