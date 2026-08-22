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
        modelBuilder.Entity<Account>().Property(item => item.Authorization).HasConversion(
            value => value == AuthorizationProfile.Administrator ? "admin" : value == AuthorizationProfile.Manager ? "manager" : "viewer",
            value => value == "admin" ? AuthorizationProfile.Administrator : value == "manager" ? AuthorizationProfile.Manager : AuthorizationProfile.Viewer);
        modelBuilder.Entity<CategoryValue>().HasIndex(item => new { item.CategoryId, item.ValueId }).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(item => new { item.TenantId, item.Username }).IsUnique();
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