-- Reports rows left behind by the tenant-delete cascade bug fixed on 2026-09-08
-- (TenantsController.Delete used to remove only the Tenant row itself).
--
-- Read-only: this script never modifies anything. Run it first, review the counts,
-- and only then run delete_orphaned_tenant_data.sql.
--
-- "Orphaned" here means: this row's tenant_id (or, for personal-best discipline
-- mappings, its source_tenant_id) does not match any row currently in `tenants`.
-- It deliberately does NOT look at Tenant.parent_tenant_id chains - a tenant whose
-- parent was deleted by the old bug is still a real, currently-existing tenant (it
-- has its own row in `tenants`, its accounts can still log in, etc.), so its data
-- must not be touched by this cleanup. Only data whose owning tenant row is fully
-- gone counts as orphaned.
--
-- Some tables may show 0 here even on a database that has real leftover data from the
-- bug, because a handful of tables (confirmed on one environment: accounts, matches,
-- competitions - check with `SHOW CREATE TABLE <table>` on yours, since this drift is
-- undocumented and not guaranteed identical everywhere) carry an undocumented database
-- foreign key straight to `tenants(id)` with ON DELETE CASCADE, so deleting a tenant's
-- row already silently cascaded those specific tables away - it's tables WITHOUT such a
-- drifted FK (typically categories, participant_lists/members, match_templates, the
-- personal_best_* tables) that are left as true orphans by the old bug. The far more
-- likely shape of "matches and competitions remain" is the *other* query at the bottom
-- of this script: a tenant that had children was deleted without recursing into them,
-- so a whole child tenant (still a real row in `tenants`, with all its own matches and
-- competitions correctly tagged with its own still-existing tenant_id - never orphaned
-- in the FK sense at all) becomes unreachable/invisible in the app instead.
--
-- Usage: mysql --database=<db> < report_orphaned_tenant_data.sql

SELECT 'accounts' AS table_name, COUNT(*) AS orphaned_rows FROM accounts WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'categories', COUNT(*) FROM categories WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'category_values', COUNT(*) FROM category_values WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'participant_lists', COUNT(*) FROM participant_lists WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'participant_list_members', COUNT(*) FROM participant_list_members WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'match_templates', COUNT(*) FROM match_templates WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'matches', COUNT(*) FROM matches WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'match_participants', COUNT(*) FROM match_participants WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'arrow_scores', COUNT(*) FROM arrow_scores WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'score_devices', COUNT(*) FROM score_devices WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'live_score_scopes', COUNT(*) FROM live_score_scopes WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'competitions', COUNT(*) FROM competitions WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'competition_rounds', COUNT(*) FROM competition_rounds WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'competition_round_matches', COUNT(*) FROM competition_round_matches WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'competition_score_rules', COUNT(*) FROM competition_score_rules WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_classifiers', COUNT(*) FROM personal_best_classifiers WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_disciplines', COUNT(*) FROM personal_best_disciplines WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_discipline_mappings', COUNT(*) FROM personal_best_discipline_mappings WHERE tenant_id NOT IN (SELECT id FROM tenants) OR source_tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_export_configs', COUNT(*) FROM personal_best_export_configs WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_export_columns', COUNT(*) FROM personal_best_export_columns WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_import_configs', COUNT(*) FROM personal_best_import_configs WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_archer_names', COUNT(*) FROM personal_best_archer_names WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_log_entries', COUNT(*) FROM personal_best_log_entries WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_import_batches', COUNT(*) FROM personal_best_import_batches WHERE tenant_id NOT IN (SELECT id FROM tenants)
UNION ALL
SELECT 'personal_best_import_conflicts', COUNT(*) FROM personal_best_import_conflicts WHERE tenant_id NOT IN (SELECT id FROM tenants);

-- Separately worth knowing about (NOT cleaned up by delete_orphaned_tenant_data.sql,
-- and not a data-loss risk): whole "zombie" tenants - a directly-broken-parent tenant
-- plus, recursively, any of its own descendants - left behind because the old code
-- deleted a tenant's row without recursing into its children at all. These are still
-- real, currently-usable tenants with their own intact matches/competitions/etc (never
-- orphaned in the FK sense - see the note above) - just no longer reachable as anyone's
-- "child" in the tenant-picker/admin UI. This is almost certainly what "matches and
-- competitions remain" actually looked like. Decide case by case whether to re-parent
-- each one (UPDATE tenants SET parent_tenant_id = '<a-real-tenant-id>' WHERE id = '...')
-- or delete it outright with everything under it - for the latter, since the normal
-- DELETE /api/tenants/{id} endpoint requires being logged into the tenant's *direct*
-- parent (which no longer exists here), ask for a one-off script targeting that
-- specific tenant id instead of guessing at a bulk deletion.
WITH RECURSIVE broken_root AS (
    SELECT t.id
    FROM tenants t
    WHERE t.parent_tenant_id IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM tenants p WHERE p.id = t.parent_tenant_id)
),
zombie_tenants AS (
    SELECT id FROM broken_root
    UNION
    SELECT c.id FROM tenants c JOIN zombie_tenants z ON c.parent_tenant_id = z.id
)
SELECT
    t.id,
    t.name,
    t.parent_tenant_id,
    (SELECT COUNT(*) FROM matches m WHERE m.tenant_id = t.id) AS own_matches,
    (SELECT COUNT(*) FROM competitions c WHERE c.tenant_id = t.id) AS own_competitions,
    (SELECT COUNT(*) FROM accounts a WHERE a.tenant_id = t.id) AS own_accounts
FROM tenants t
JOIN zombie_tenants z ON z.id = t.id
ORDER BY t.name;
