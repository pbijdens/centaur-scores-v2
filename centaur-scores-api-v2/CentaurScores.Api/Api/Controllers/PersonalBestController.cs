using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CentaurScores.Api.Controllers;

[Route("api/personal-best")]
public sealed class PersonalBestController(ApplicationDbContext db, ITenantContext tenantContext, IPersonalBestContext personalBestContext, IPersonalBestEngine engine, IPersonalBestExcelService excelService) : ApiControllerBase(tenantContext)
{
    private Task<Guid?> RequireOwningTenantAsync(CancellationToken cancellationToken) => personalBestContext.ResolveOwningTenantIdAsync(TenantId, cancellationToken);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken) => Ok(await personalBestContext.GetStatusAsync(TenantId, cancellationToken));

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var existingOwner = await personalBestContext.ResolveOwningTenantIdAsync(TenantId, cancellationToken);
        if (existingOwner is not null) return Conflict(new ApiError("PERSONAL_BEST_ALREADY_ENABLED", "Personal best tracking is already enabled for this tenant or an ancestor tenant."));

        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        tenant.PersonalBestEnabled = true;

        db.PersonalBestExportConfigs.Add(new PersonalBestExportConfig
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ExportMode = "all",
            TableName = "PersonalBestExport",
            Columns =
            [
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 0, ColumnName = "Datum", Field = "date", DateFormat = "ymd" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 1, ColumnName = "Bondsnummer", Field = "federationNumber" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 2, ColumnName = "Naam", Field = "fullName" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 3, ColumnName = "Discipline", Field = "discipline" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 4, ColumnName = "Wedstrijd", Field = "matchClassifier" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 5, ColumnName = "Score", Field = "score" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), TenantId = TenantId, SortOrder = 6, ColumnName = "Toegevoegd", Field = "exportDate", DateFormat = "ymd" }
            ]
        });
        db.PersonalBestImportConfigs.Add(new PersonalBestImportConfig { Id = Guid.NewGuid(), TenantId = TenantId });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PersonalBestStatus(true, true, TenantId));
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        // Data (log, configuration) is preserved on disable - only the flag flips. See the feature doc's Q&A.
        tenant.PersonalBestEnabled = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PersonalBestStatus(false, false, null));
    }

    [HttpGet("classifiers")]
    public async Task<IActionResult> GetClassifiers(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        return Ok(await db.PersonalBestClassifiers.AsNoTracking().Where(item => item.TenantId == owningTenantId).OrderBy(item => item.Name).Select(item => item.Name).ToListAsync(cancellationToken));
    }

    [HttpPut("classifiers")]
    public async Task<IActionResult> SaveClassifiers(SavePersonalBestClassifiersRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var names = request.Classifiers.Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            return BadRequest(new ApiError("DUPLICATE_CLASSIFIER", "Match classifiers must be unique."));

        db.PersonalBestClassifiers.RemoveRange(await db.PersonalBestClassifiers.Where(item => item.TenantId == owningTenantId).ToListAsync(cancellationToken));
        db.PersonalBestClassifiers.AddRange(names.Select(name => new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = owningTenantId.Value, Name = name }));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(names.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    [HttpGet("disciplines")]
    public async Task<IActionResult> GetDisciplines(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        return Ok(await BuildDisciplineViewsAsync(owningTenantId.Value, cancellationToken));
    }

    [HttpGet("disciplines/available-values")]
    public async Task<IActionResult> GetAvailableValues(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var allTenants = await db.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        var scopeTenantIds = DescendantsIncludingSelf(owningTenantId.Value, allTenants);
        var tenantNameById = allTenants.ToDictionary(item => item.Id, item => item.Name);
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => scopeTenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        var takenBy = await db.PersonalBestDisciplineMappings.AsNoTracking().Where(item => item.TenantId == owningTenantId)
            .ToDictionaryAsync(item => (item.SourceTenantId, item.CategoryId, item.ValueId), item => item.DisciplineId, cancellationToken);

        var values = categories
            .SelectMany(category => category.Values.Select(value => new PersonalBestAvailableValue(
                category.TenantId, tenantNameById.GetValueOrDefault(category.TenantId, "?"),
                category.Id, category.Name, value.ValueId, value.Name,
                takenBy.TryGetValue((category.TenantId, category.Id, value.ValueId), out var disciplineId) ? disciplineId : null)))
            .OrderBy(item => item.TenantName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ValueName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Ok(values);
    }

    [HttpPut("disciplines")]
    public async Task<IActionResult> SaveDisciplines(SavePersonalBestDisciplinesRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var names = request.Disciplines.Select(item => item.Name.Trim()).ToList();
        if (names.Any(name => name.Length == 0) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            return BadRequest(new ApiError("DUPLICATE_DISCIPLINE", "Disciplines must have a unique, non-empty name."));

        var allValueRefs = request.Disciplines.SelectMany(discipline => discipline.Values.Select(value => (value.TenantId, value.CategoryId, value.ValueId))).ToList();
        if (allValueRefs.Distinct().Count() != allValueRefs.Count)
            return BadRequest(new ApiError("VALUE_ALREADY_MAPPED", "A category value cannot be attached to more than one discipline."));

        var existingDisciplines = await db.PersonalBestDisciplines.Include(item => item.Mappings).Where(item => item.TenantId == owningTenantId).ToListAsync(cancellationToken);
        db.PersonalBestDisciplineMappings.RemoveRange(existingDisciplines.SelectMany(item => item.Mappings));
        db.PersonalBestDisciplines.RemoveRange(existingDisciplines);

        foreach (var discipline in request.Disciplines)
        {
            var disciplineId = Guid.NewGuid();
            db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
            {
                Id = disciplineId,
                TenantId = owningTenantId.Value,
                Name = discipline.Name.Trim(),
                Mappings = discipline.Values.Select(value => new PersonalBestDisciplineMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = owningTenantId.Value,
                    DisciplineId = disciplineId,
                    SourceTenantId = value.TenantId,
                    CategoryId = value.CategoryId,
                    ValueId = value.ValueId
                }).ToList()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildDisciplineViewsAsync(owningTenantId.Value, cancellationToken));
    }

    [HttpGet("export-config")]
    public async Task<IActionResult> GetExportConfig(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        var config = await db.PersonalBestExportConfigs.AsNoTracking().Include(item => item.Columns).SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        return config is null ? NotFound() : Ok(ToView(config));
    }

    [HttpPut("export-config")]
    public async Task<IActionResult> SaveExportConfig(SavePersonalBestExportConfigRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.TableName)) return BadRequest(new ApiError("INVALID_EXPORT_CONFIG", "Table name may not be empty."));
        if (request.Columns.Any(column => string.IsNullOrWhiteSpace(column.ColumnName))) return BadRequest(new ApiError("INVALID_EXPORT_CONFIG", "A column name may not be empty."));

        var config = await db.PersonalBestExportConfigs.Include(item => item.Columns).SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        if (config is null) return NotFound();

        config.ExportMode = request.ExportMode == "changesSinceLastImport" ? "changesSinceLastImport" : "all";
        config.TableName = request.TableName.Trim();
        // Deliberately don't reassign config.Columns here: replacing an already-tracked entity's collection
        // navigation right after RemoveRange-ing its old contents makes EF Core double-track the removal of
        // the old rows (it treats them as orphaned by the new collection too), and the second DELETE for an
        // already-deleted row throws DbUpdateConcurrencyException ("affected 0 rows"). Adding the new
        // columns directly avoids touching the navigation on the still-tracked parent.
        db.PersonalBestExportColumns.RemoveRange(config.Columns);
        var newColumns = request.Columns.Select((column, index) => new PersonalBestExportColumn
        {
            Id = Guid.NewGuid(),
            TenantId = owningTenantId.Value,
            ExportConfigId = config.Id,
            SortOrder = index,
            ColumnName = column.ColumnName.Trim(),
            Field = column.Field,
            DateFormat = column.Field is "date" or "exportDate" ? column.DateFormat : null
        }).ToList();
        db.PersonalBestExportColumns.AddRange(newColumns);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PersonalBestExportConfigView(config.ExportMode, config.TableName, newColumns.Select(item => new PersonalBestExportColumnView(item.ColumnName, item.Field, item.DateFormat)).ToList()));
    }

    [HttpGet("import-config")]
    public async Task<IActionResult> GetImportConfig(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        var config = await db.PersonalBestImportConfigs.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        return config is null ? NotFound() : Ok(config);
    }

    [HttpPut("import-config")]
    public async Task<IActionResult> SaveImportConfig(SavePersonalBestImportConfigRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var fields = new[] { request.TableName, request.DateColumn, request.FederationNumberColumn, request.NameColumn, request.DisciplineColumn, request.MatchClassifierColumn, request.ScoreColumn, request.UpdateDateColumn };
        if (fields.Any(string.IsNullOrWhiteSpace)) return BadRequest(new ApiError("INVALID_IMPORT_CONFIG", "All import configuration fields are required."));

        var config = await db.PersonalBestImportConfigs.SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        if (config is null) return NotFound();
        config.TableName = request.TableName.Trim();
        config.DateColumn = request.DateColumn.Trim();
        config.FederationNumberColumn = request.FederationNumberColumn.Trim();
        config.NameColumn = request.NameColumn.Trim();
        config.DisciplineColumn = request.DisciplineColumn.Trim();
        config.MatchClassifierColumn = request.MatchClassifierColumn.Trim();
        config.ScoreColumn = request.ScoreColumn.Trim();
        config.UpdateDateColumn = request.UpdateDateColumn.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(config);
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        if (file.Length == 0) return BadRequest(new ApiError("IMPORT_FILE_MISSING", "No file was uploaded."));

        var importConfig = await db.PersonalBestImportConfigs.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        if (importConfig is null) return NotFound();

        PersonalBestImportParseResult parsed;
        try
        {
            using var stream = file.OpenReadStream();
            parsed = excelService.ParseImport(stream, importConfig);
        }
        catch (PersonalBestImportException exception)
        {
            return BadRequest(new ApiError(exception.Code, exception.Message));
        }

        var classifierNames = await db.PersonalBestClassifiers.AsNoTracking().Where(item => item.TenantId == owningTenantId).Select(item => item.Name).ToListAsync(cancellationToken);
        var disciplineNames = await db.PersonalBestDisciplines.AsNoTracking().Where(item => item.TenantId == owningTenantId).Select(item => item.Name).ToListAsync(cancellationToken);
        var knownArchers = new HashSet<string>(
            await db.PersonalBestArcherNames.AsNoTracking().Where(item => item.TenantId == owningTenantId).Select(item => item.FederationNumber).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>(parsed.Warnings);
        var newRegistrations = 0;
        var processedArchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var offered = new Dictionary<(string FederationNumber, string Discipline, string MatchClassifier), (int MaxScore, int Count)>();
        var cannotInsertConflicts = new List<PersonalBestImportConflictView>();

        foreach (var row in parsed.Rows)
        {
            var discipline = disciplineNames.FirstOrDefault(name => string.Equals(name, row.Discipline, StringComparison.OrdinalIgnoreCase));
            var classifier = classifierNames.FirstOrDefault(name => string.Equals(name, row.MatchClassifier, StringComparison.OrdinalIgnoreCase));
            if (discipline is null || classifier is null)
            {
                warnings.Add($"Skipped federation number '{row.FederationNumber}': '{row.Discipline}'/'{row.MatchClassifier}' is not a configured discipline/match classifier.");
                continue;
            }

            await engine.EnsureArcherNameAsync(owningTenantId.Value, row.FederationNumber, row.Name, cancellationToken);
            processedArchers.Add(row.FederationNumber);

            // Saved per-row (not batched) so that a subsequent row for the same archer/discipline/classifier
            // in this same file sees the effect of earlier rows when TryInsertAsync re-reads the log.
            var outcome = await engine.TryInsertAsync(owningTenantId.Value, row.FederationNumber, discipline, classifier, row.Score, row.Date, row.RecordedAt, "import", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            if (outcome.Inserted) newRegistrations++;
            if (outcome.CannotInsert) cannotInsertConflicts.Add(new PersonalBestImportConflictView(row.FederationNumber, discipline, classifier, "cannotInsertLowerScore", false));

            var key = (row.FederationNumber, discipline, classifier);
            (int MaxScore, int Count) current = offered.TryGetValue(key, out var existing) ? existing : (0, 0);
            offered[key] = (Math.Max(current.MaxScore, row.Score), current.Count + 1);
        }

        var actionableConflicts = new List<PersonalBestImportConflict>();
        foreach (var (key, stats) in offered)
        {
            if (stats.Count == 0) continue;
            var remainingHigher = await db.PersonalBestLogEntries.AsNoTracking()
                .Where(item => item.TenantId == owningTenantId && item.FederationNumber == key.FederationNumber && item.Discipline == key.Discipline && item.MatchClassifier == key.MatchClassifier && item.Score > stats.MaxScore)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (remainingHigher.Count == 0) continue;

            actionableConflicts.Add(new PersonalBestImportConflict
            {
                Id = Guid.NewGuid(),
                TenantId = owningTenantId.Value,
                FederationNumber = key.FederationNumber,
                Discipline = key.Discipline,
                MatchClassifier = key.MatchClassifier,
                ConflictType = "archerHasHigherScore",
                OffendingLogEntryIdsJson = JsonSerializer.Serialize(remainingHigher)
            });
        }

        Guid? batchId = null;
        if (actionableConflicts.Count > 0)
        {
            batchId = Guid.NewGuid();
            db.PersonalBestImportBatches.Add(new PersonalBestImportBatch { Id = batchId.Value, TenantId = owningTenantId.Value, CreatedAt = DateTime.UtcNow, Conflicts = actionableConflicts });
            await db.SaveChangesAsync(cancellationToken);
        }

        var newArchers = processedArchers.Count(number => !knownArchers.Contains(number));
        var conflicts = cannotInsertConflicts
            .Concat(actionableConflicts.Select(item => new PersonalBestImportConflictView(item.FederationNumber, item.Discipline, item.MatchClassifier, item.ConflictType, true)))
            .ToList();

        return Ok(new PersonalBestImportResult(newArchers, newRegistrations, warnings, batchId, conflicts));
    }

    [HttpPost("import/{batchId:guid}/resolve")]
    public async Task<IActionResult> ResolveConflicts(Guid batchId, ResolvePersonalBestConflictsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var batch = await db.PersonalBestImportBatches.Include(item => item.Conflicts).SingleOrDefaultAsync(item => item.Id == batchId && item.TenantId == owningTenantId, cancellationToken);
        if (batch is null) return NotFound();

        foreach (var resolution in request.Resolutions)
        {
            var conflict = batch.Conflicts.SingleOrDefault(item =>
                item.FederationNumber == resolution.FederationNumber && item.Discipline == resolution.Discipline && item.MatchClassifier == resolution.MatchClassifier);
            if (conflict is null || resolution.Action != "deleteOffending") continue;

            var ids = JsonSerializer.Deserialize<List<Guid>>(conflict.OffendingLogEntryIdsJson) ?? [];
            var offending = await db.PersonalBestLogEntries.Where(item => ids.Contains(item.Id) && item.TenantId == owningTenantId).ToListAsync(cancellationToken);
            db.PersonalBestLogEntries.RemoveRange(offending);
        }

        db.PersonalBestImportBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();
        var config = await db.PersonalBestExportConfigs.AsNoTracking().Include(item => item.Columns).SingleOrDefaultAsync(item => item.TenantId == owningTenantId, cancellationToken);
        if (config is null) return NotFound();

        var entries = await db.PersonalBestLogEntries.AsNoTracking().Where(item => item.TenantId == owningTenantId).ToListAsync(cancellationToken);
        var selected = entries.AsEnumerable();
        if (config.ExportMode == "changesSinceLastImport")
        {
            var watermarks = entries.Where(item => item.Source == "import")
                .GroupBy(item => (item.FederationNumber, item.Discipline, item.MatchClassifier))
                .ToDictionary(group => group.Key, group => group.Max(item => item.Date));
            selected = entries.Where(item => !watermarks.TryGetValue((item.FederationNumber, item.Discipline, item.MatchClassifier), out var watermark) || item.Date > watermark);
        }

        var names = await db.PersonalBestArcherNames.AsNoTracking().Where(item => item.TenantId == owningTenantId).ToDictionaryAsync(item => item.FederationNumber, item => item.Name, cancellationToken);
        var rows = selected
            .OrderBy(item => item.FederationNumber).ThenBy(item => item.Discipline).ThenBy(item => item.MatchClassifier)
            .Select(item => new PersonalBestExportRow(item.FederationNumber, names.GetValueOrDefault(item.FederationNumber, ""), item.Discipline, item.MatchClassifier, item.Score, item.Date))
            .ToList();

        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        var bytes = excelService.Export(config, rows, DateOnly.FromDateTime(DateTime.UtcNow));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{tenant?.Name ?? "tenant"} personal best updates.xlsx");
    }

    [HttpGet("log")]
    public async Task<IActionResult> Log(CancellationToken cancellationToken)
    {
        var owningTenantId = await RequireOwningTenantAsync(cancellationToken);
        if (owningTenantId is null) return NotFound();

        var entries = await db.PersonalBestLogEntries.AsNoTracking().Where(item => item.TenantId == owningTenantId).ToListAsync(cancellationToken);
        var names = await db.PersonalBestArcherNames.AsNoTracking().Where(item => item.TenantId == owningTenantId).ToDictionaryAsync(item => item.FederationNumber, item => item.Name, cancellationToken);

        var rows = entries
            .GroupBy(item => (item.FederationNumber, item.Discipline, item.MatchClassifier))
            .Select(group => group.OrderByDescending(item => item.Date).ThenByDescending(item => item.Score).First())
            .Select(item => new PersonalBestLogRow(item.FederationNumber, names.GetValueOrDefault(item.FederationNumber, ""), item.Discipline, item.MatchClassifier, item.Date, item.Score))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.FederationNumber).ThenBy(item => item.Discipline).ThenBy(item => item.MatchClassifier)
            .ToList();
        return Ok(rows);
    }

    private async Task<List<PersonalBestDisciplineView>> BuildDisciplineViewsAsync(Guid owningTenantId, CancellationToken cancellationToken)
    {
        var disciplines = await db.PersonalBestDisciplines.AsNoTracking().Include(item => item.Mappings).Where(item => item.TenantId == owningTenantId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var tenantIds = disciplines.SelectMany(item => item.Mappings.Select(mapping => mapping.SourceTenantId)).Distinct().ToList();
        var tenantNames = await db.Tenants.AsNoTracking().Where(item => tenantIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var categoryIds = disciplines.SelectMany(item => item.Mappings.Select(mapping => mapping.CategoryId)).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => categoryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);

        return disciplines.Select(discipline => new PersonalBestDisciplineView(
            discipline.Id,
            discipline.Name,
            discipline.Mappings.Select(mapping =>
            {
                var category = categories.GetValueOrDefault(mapping.CategoryId);
                var categoryName = category?.Name ?? "?";
                var valueName = category?.Values.FirstOrDefault(value => value.ValueId == mapping.ValueId)?.Name ?? mapping.ValueId.ToString();
                var tenantName = tenantNames.GetValueOrDefault(mapping.SourceTenantId, "?");
                return new PersonalBestDisciplineValueView(mapping.SourceTenantId, tenantName, mapping.CategoryId, categoryName, mapping.ValueId, valueName);
            })
            .OrderBy(value => value.TenantName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(value => value.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(value => value.ValueName, StringComparer.CurrentCultureIgnoreCase)
            .ToList()))
            .ToList();
    }

    private static HashSet<Guid> DescendantsIncludingSelf(Guid rootId, IReadOnlyList<Tenant> allTenants)
    {
        var result = new HashSet<Guid> { rootId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in allTenants.Where(item => item.ParentTenantId == current))
            {
                if (result.Add(child.Id)) queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private static PersonalBestExportConfigView ToView(PersonalBestExportConfig config) => new(
        config.ExportMode,
        config.TableName,
        config.Columns.OrderBy(item => item.SortOrder).Select(item => new PersonalBestExportColumnView(item.ColumnName, item.Field, item.DateFormat)).ToList());
}
