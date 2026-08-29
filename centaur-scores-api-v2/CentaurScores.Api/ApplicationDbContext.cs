using CentaurScores.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace CentaurScores.Api.Infrastructure;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryValue> CategoryValues => Set<CategoryValue>();
    public DbSet<ParticipantList> ParticipantLists => Set<ParticipantList>();
    public DbSet<ParticipantListMember> ParticipantListMembers => Set<ParticipantListMember>();
    public DbSet<MatchTemplate> MatchTemplates => Set<MatchTemplate>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<ArrowScore> ArrowScores => Set<ArrowScore>();
    public DbSet<ScoreDevice> ScoreDevices => Set<ScoreDevice>();
    public DbSet<LiveScoreScope> LiveScoreScopes => Set<LiveScoreScope>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionRound> CompetitionRounds => Set<CompetitionRound>();
    public DbSet<CompetitionRoundMatch> CompetitionRoundMatches => Set<CompetitionRoundMatch>();
    public DbSet<CompetitionScoreRule> CompetitionScoreRules => Set<CompetitionScoreRule>();
    public DbSet<PersonalBestClassifier> PersonalBestClassifiers => Set<PersonalBestClassifier>();
    public DbSet<PersonalBestDiscipline> PersonalBestDisciplines => Set<PersonalBestDiscipline>();
    public DbSet<PersonalBestDisciplineMapping> PersonalBestDisciplineMappings => Set<PersonalBestDisciplineMapping>();
    public DbSet<PersonalBestExportConfig> PersonalBestExportConfigs => Set<PersonalBestExportConfig>();
    public DbSet<PersonalBestExportColumn> PersonalBestExportColumns => Set<PersonalBestExportColumn>();
    public DbSet<PersonalBestImportConfig> PersonalBestImportConfigs => Set<PersonalBestImportConfig>();
    public DbSet<PersonalBestArcherName> PersonalBestArcherNames => Set<PersonalBestArcherName>();
    public DbSet<PersonalBestLogEntry> PersonalBestLogEntries => Set<PersonalBestLogEntry>();
    public DbSet<PersonalBestImportBatch> PersonalBestImportBatches => Set<PersonalBestImportBatch>();
    public DbSet<PersonalBestImportConflict> PersonalBestImportConflicts => Set<PersonalBestImportConflict>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().ToTable("tenants").HasKey(item => item.Id);
        modelBuilder.Entity<Account>().ToTable("accounts").HasKey(item => item.Id);
        modelBuilder.Entity<Category>().ToTable("categories").HasKey(item => item.Id);
        modelBuilder.Entity<CategoryValue>().ToTable("category_values").HasKey(item => new { item.CategoryId, item.ValueId });
        modelBuilder.Entity<ParticipantList>().ToTable("participant_lists").HasKey(item => item.Id);
        modelBuilder.Entity<ParticipantListMember>().ToTable("participant_list_members").HasKey(item => item.Id);
        modelBuilder.Entity<MatchTemplate>().ToTable("match_templates").HasKey(item => item.Id);
        modelBuilder.Entity<Match>().ToTable("matches").HasKey(item => item.Id);
        modelBuilder.Entity<MatchParticipant>().ToTable("match_participants").HasKey(item => item.Id);
        modelBuilder.Entity<ArrowScore>().ToTable("arrow_scores").HasKey(item => item.Id);
        modelBuilder.Entity<ScoreDevice>().ToTable("score_devices").HasKey(item => item.Id);
        modelBuilder.Entity<LiveScoreScope>().ToTable("live_score_scopes").HasKey(item => item.Id);
        modelBuilder.Entity<Competition>().ToTable("competitions").HasKey(item => item.Id);
        modelBuilder.Entity<CompetitionRound>().ToTable("competition_rounds").HasKey(item => item.Id);
        modelBuilder.Entity<CompetitionRoundMatch>().ToTable("competition_round_matches").HasKey(item => item.Id);
        modelBuilder.Entity<CompetitionScoreRule>().ToTable("competition_score_rules").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestClassifier>().ToTable("personal_best_classifiers").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestDiscipline>().ToTable("personal_best_disciplines").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestDisciplineMapping>().ToTable("personal_best_discipline_mappings").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestExportConfig>().ToTable("personal_best_export_configs").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestExportColumn>().ToTable("personal_best_export_columns").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestImportConfig>().ToTable("personal_best_import_configs").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestArcherName>().ToTable("personal_best_archer_names").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestLogEntry>().ToTable("personal_best_log_entries").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestImportBatch>().ToTable("personal_best_import_batches").HasKey(item => item.Id);
        modelBuilder.Entity<PersonalBestImportConflict>().ToTable("personal_best_import_conflicts").HasKey(item => item.Id);
        // Explicit lengths keep these string columns as indexable varchar rather than the longtext that a
        // bare `string` property maps to by default - without this, the 5-column composite index below
        // exceeds MySQL's 3072-byte max key length.
        modelBuilder.Entity<PersonalBestClassifier>().Property(item => item.Name).HasMaxLength(100);
        modelBuilder.Entity<PersonalBestDiscipline>().Property(item => item.Name).HasMaxLength(100);
        modelBuilder.Entity<PersonalBestArcherName>().Property(item => item.FederationNumber).HasMaxLength(32);
        modelBuilder.Entity<PersonalBestLogEntry>().Property(item => item.FederationNumber).HasMaxLength(32);
        modelBuilder.Entity<PersonalBestLogEntry>().Property(item => item.Discipline).HasMaxLength(100);
        modelBuilder.Entity<PersonalBestLogEntry>().Property(item => item.MatchClassifier).HasMaxLength(100);
        modelBuilder.Entity<PersonalBestClassifier>().HasIndex(item => new { item.TenantId, item.Name }).IsUnique();
        modelBuilder.Entity<PersonalBestDiscipline>().HasIndex(item => new { item.TenantId, item.Name }).IsUnique();
        modelBuilder.Entity<PersonalBestDisciplineMapping>().HasIndex(item => new { item.TenantId, item.SourceTenantId, item.CategoryId, item.ValueId }).IsUnique();
        modelBuilder.Entity<PersonalBestArcherName>().HasIndex(item => new { item.TenantId, item.FederationNumber }).IsUnique();
        modelBuilder.Entity<PersonalBestLogEntry>().HasIndex(item => new { item.TenantId, item.FederationNumber, item.Discipline, item.MatchClassifier, item.Date });
        modelBuilder.Entity<PersonalBestDiscipline>().HasMany(item => item.Mappings).WithOne().HasForeignKey(item => item.DisciplineId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PersonalBestExportConfig>().HasMany(item => item.Columns).WithOne().HasForeignKey(item => item.ExportConfigId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PersonalBestImportBatch>().HasMany(item => item.Conflicts).WithOne().HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Account>().Property(item => item.Authorization).HasConversion(
            value => value == AuthorizationProfile.Administrator ? "admin" : value == AuthorizationProfile.Manager ? "manager" : "viewer",
            value => value == "admin" ? AuthorizationProfile.Administrator : value == "manager" ? AuthorizationProfile.Manager : AuthorizationProfile.Viewer);
        modelBuilder.Entity<CategoryValue>().HasIndex(item => new { item.CategoryId, item.ValueId }).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(item => item.Username).IsUnique();
        // Non-unique - keeps a supporting index on tenant_id after the composite (tenant_id, username)
        // index above was replaced by the username-only one. The live database has an undocumented
        // FK_accounts_tenants_tenant_id foreign key (schema drift, never captured in the EF model or an
        // earlier migration) that requires an index with tenant_id as its leading column; dropping the
        // composite index without this one fails with "needed in a foreign key constraint".
        modelBuilder.Entity<Account>().HasIndex(item => item.TenantId);
        modelBuilder.Entity<ParticipantListMember>().Property(item => item.Categories).HasJsonConversion();
        modelBuilder.Entity<MatchParticipant>().Property(item => item.Categories).HasJsonConversion();
        modelBuilder.Entity<Match>().HasMany(item => item.Participants).WithOne().HasForeignKey(item => item.MatchId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Match>().HasMany(item => item.Devices).WithOne().HasForeignKey(item => item.MatchId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Match>().HasMany(item => item.LiveScopes).WithOne().HasForeignKey(item => item.MatchId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MatchParticipant>().HasMany(item => item.Scores).WithOne().HasForeignKey(item => item.MatchParticipantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Competition>().HasMany(item => item.Rounds).WithOne().HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Competition>().HasMany(item => item.ScoringRules).WithOne().HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompetitionRound>().HasMany(item => item.Matches).WithOne().HasForeignKey(item => item.CompetitionRoundId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompetitionScoreRule>().Property(item => item.SortOrder).HasDefaultValue(0);
    }
}

internal static class JsonPropertyBuilderExtensions
{
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class, new()
    {
        var converter = new ValueConverter<T, string>(value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), value => JsonSerializer.Deserialize<T>(value, (JsonSerializerOptions?)null) ?? new T());
        var comparer = new ValueComparer<T>((left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null), value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(), value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new T());
        property.HasConversion(converter);
        property.Metadata.SetValueComparer(comparer);
        return property;
    }
}