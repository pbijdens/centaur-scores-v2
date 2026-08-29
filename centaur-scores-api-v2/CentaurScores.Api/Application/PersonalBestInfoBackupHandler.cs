using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

/// <summary>
/// Bundles all personal-best configuration/log rows for one tenant into a single file
/// (personal-best-info/{tenant-guid}.json), since PersonalBestClassifier/Discipline/etc. are owned by a
/// tenant as a set rather than being independently addressable objects.
///
/// PersonalBestImportBatch/PersonalBestImportConflict are deliberately excluded - they're transient,
/// "deleted once resolved" operational state (see Domain.cs), not durable configuration or log data.
/// </summary>
public sealed class PersonalBestInfoBackupHandler : IBackupHandler
{
    public string FolderName => "personal-best-info";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var classifiers = await db.PersonalBestClassifiers.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var disciplines = await db.PersonalBestDisciplines.AsNoTracking().Include(item => item.Mappings).Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var exportConfigs = await db.PersonalBestExportConfigs.AsNoTracking().Include(item => item.Columns).Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var importConfigs = await db.PersonalBestImportConfigs.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var archerNames = await db.PersonalBestArcherNames.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var logEntries = await db.PersonalBestLogEntries.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);

        var result = new List<(Guid, object)>();
        foreach (var tenantId in tenantIds)
        {
            var tenantClassifiers = classifiers.Where(item => item.TenantId == tenantId).Select(item => new PersonalBestClassifierBackup(item.Name)).ToList();
            // A mapping whose SourceTenantId falls outside the exported tenant set is dropped here - it would
            // otherwise point at a category the backup doesn't contain, which restore could never resolve.
            var tenantDisciplines = disciplines.Where(item => item.TenantId == tenantId).Select(discipline => new PersonalBestDisciplineBackup(
                discipline.Name,
                discipline.Mappings.Where(mapping => tenantIds.Contains(mapping.SourceTenantId)).Select(mapping => new PersonalBestDisciplineMappingBackup(mapping.SourceTenantId, mapping.CategoryId, mapping.ValueId)).ToList())).ToList();
            var exportConfig = exportConfigs.Where(item => item.TenantId == tenantId)
                .Select(item => new PersonalBestExportConfigBackup(item.ExportMode, item.TableName, item.Columns.Select(column => new PersonalBestExportColumnBackup(column.SortOrder, column.ColumnName, column.Field, column.DateFormat)).ToList()))
                .FirstOrDefault();
            var importConfig = importConfigs.Where(item => item.TenantId == tenantId)
                .Select(item => new PersonalBestImportConfigBackup(item.TableName, item.DateColumn, item.FederationNumberColumn, item.NameColumn, item.DisciplineColumn, item.MatchClassifierColumn, item.ScoreColumn, item.UpdateDateColumn))
                .FirstOrDefault();
            var tenantArcherNames = archerNames.Where(item => item.TenantId == tenantId).Select(item => new PersonalBestArcherNameBackup(item.FederationNumber, item.Name)).ToList();
            var tenantLogEntries = logEntries.Where(item => item.TenantId == tenantId).Select(item => new PersonalBestLogEntryBackup(item.FederationNumber, item.Discipline, item.MatchClassifier, item.Score, item.Date, item.RecordedAt, item.Source)).ToList();

            if (tenantClassifiers.Count == 0 && tenantDisciplines.Count == 0 && exportConfig is null && importConfig is null && tenantArcherNames.Count == 0 && tenantLogEntries.Count == 0) continue;

            result.Add((tenantId, new PersonalBestInfoBackup(tenantId, tenantClassifiers, tenantDisciplines, exportConfig, importConfig, tenantArcherNames, tenantLogEntries)));
        }
        return result;
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<PersonalBestInfoBackup>(FolderName);
        var warnings = new List<string>();
        var classifiers = new List<PersonalBestClassifier>();
        var disciplines = new List<PersonalBestDiscipline>();
        var exportConfigs = new List<PersonalBestExportConfig>();
        var importConfigs = new List<PersonalBestImportConfig>();
        var archerNames = new List<PersonalBestArcherName>();
        var logEntries = new List<PersonalBestLogEntry>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add("Skipped personal-best data for a tenant that was not part of this backup.");
                continue;
            }

            classifiers.AddRange(entry.Classifiers.Select(item => new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = newTenantId, Name = item.Name }));

            foreach (var discipline in entry.Disciplines)
            {
                var newDisciplineId = Guid.NewGuid();
                var mappings = new List<PersonalBestDisciplineMapping>();
                foreach (var mapping in discipline.Mappings)
                {
                    if (!context.TryRemap(mapping.SourceTenantId, out var newSourceTenantId) || !context.TryRemap(mapping.CategoryId, out var newCategoryId))
                    {
                        warnings.Add($"Dropped a personal-best discipline mapping for '{discipline.Name}': its category was outside this backup.");
                        continue;
                    }
                    mappings.Add(new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = newTenantId, DisciplineId = newDisciplineId, SourceTenantId = newSourceTenantId, CategoryId = newCategoryId, ValueId = mapping.ValueId });
                }
                disciplines.Add(new PersonalBestDiscipline { Id = newDisciplineId, TenantId = newTenantId, Name = discipline.Name, Mappings = mappings });
            }

            if (entry.ExportConfig is { } exportConfig)
            {
                var newExportConfigId = Guid.NewGuid();
                exportConfigs.Add(new PersonalBestExportConfig
                {
                    Id = newExportConfigId,
                    TenantId = newTenantId,
                    ExportMode = exportConfig.ExportMode,
                    TableName = exportConfig.TableName,
                    Columns = exportConfig.Columns.Select(column => new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = newTenantId, ExportConfigId = newExportConfigId, SortOrder = column.SortOrder, ColumnName = column.ColumnName, Field = column.Field, DateFormat = column.DateFormat }).ToList()
                });
            }

            if (entry.ImportConfig is { } importConfig)
            {
                importConfigs.Add(new PersonalBestImportConfig
                {
                    Id = Guid.NewGuid(),
                    TenantId = newTenantId,
                    TableName = importConfig.TableName,
                    DateColumn = importConfig.DateColumn,
                    FederationNumberColumn = importConfig.FederationNumberColumn,
                    NameColumn = importConfig.NameColumn,
                    DisciplineColumn = importConfig.DisciplineColumn,
                    MatchClassifierColumn = importConfig.MatchClassifierColumn,
                    ScoreColumn = importConfig.ScoreColumn,
                    UpdateDateColumn = importConfig.UpdateDateColumn
                });
            }

            archerNames.AddRange(entry.ArcherNames.Select(item => new PersonalBestArcherName { Id = Guid.NewGuid(), TenantId = newTenantId, FederationNumber = item.FederationNumber, Name = item.Name }));
            logEntries.AddRange(entry.LogEntries.Select(item => new PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = newTenantId, FederationNumber = item.FederationNumber, Discipline = item.Discipline, MatchClassifier = item.MatchClassifier, Score = item.Score, Date = item.Date, RecordedAt = item.RecordedAt, Source = item.Source }));
        }

        context.Db.PersonalBestClassifiers.AddRange(classifiers);
        context.Db.PersonalBestDisciplines.AddRange(disciplines);
        context.Db.PersonalBestExportConfigs.AddRange(exportConfigs);
        context.Db.PersonalBestImportConfigs.AddRange(importConfigs);
        context.Db.PersonalBestArcherNames.AddRange(archerNames);
        context.Db.PersonalBestLogEntries.AddRange(logEntries);
        await context.Db.SaveChangesAsync(cancellationToken);

        return new BackupImportOutcome(classifiers.Count + disciplines.Count + archerNames.Count + logEntries.Count, warnings);
    }
}
