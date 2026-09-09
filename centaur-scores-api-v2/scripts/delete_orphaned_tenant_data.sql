-- Deletes rows left behind by the tenant-delete cascade bug fixed on 2026-09-08
-- (TenantsController.Delete used to remove only the Tenant row itself, leaving every
-- match/competition/category/account/etc. underneath a deleted tenant in place).
--
-- BEFORE RUNNING:
--   1. Take a full backup of the database (e.g. `mysqldump`). This script does not
--      touch any currently-existing tenant's data, but back up anyway - it is a
--      real, committed DELETE against production.
--   2. Run report_orphaned_tenant_data.sql first and read the counts. If a count
--      looks larger than you expect for a table, stop and investigate before
--      running this script.
--
-- Safe to run more than once - a second run will find 0 rows everywhere and change
-- nothing. New tenant deletions after this fix is deployed no longer produce orphans,
-- so this is a one-time cleanup, not something to schedule.
--
-- "Orphaned" here means: this row's tenant_id (or, for personal-best discipline
-- mappings, its source_tenant_id) does not match any row currently in `tenants`. This
-- deliberately does NOT touch a tenant whose own row still exists, even if that
-- tenant's parent_tenant_id is now dangling (see report_orphaned_tenant_data.sql's
-- last query) - such a tenant is still real and in use, only unreachable as anyone's
-- listed "child"; deleting its data would be actively harmful, not a cleanup.
--
-- Deletion order matches TenantDeletionService.cs (Application/TenantDeletionService.cs):
-- children before parents, and specifically match_participants before
-- participant_list_members, since match_participants.participant_list_member_id ->
-- participant_list_members is a Restrict foreign key.
--
-- Usage: mysql --database=<db> < delete_orphaned_tenant_data.sql

START TRANSACTION;

DELETE FROM arrow_scores WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM match_participants WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM score_devices WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM live_score_scopes WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM matches WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM match_templates WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM participant_list_members WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM participant_lists WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM category_values WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM categories WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM competition_round_matches WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM competition_score_rules WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM competition_rounds WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM competitions WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM accounts WHERE tenant_id NOT IN (SELECT id FROM tenants);

DELETE FROM personal_best_discipline_mappings WHERE tenant_id NOT IN (SELECT id FROM tenants) OR source_tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_disciplines WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_classifiers WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_export_columns WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_export_configs WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_import_configs WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_archer_names WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_log_entries WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_import_conflicts WHERE tenant_id NOT IN (SELECT id FROM tenants);
DELETE FROM personal_best_import_batches WHERE tenant_id NOT IN (SELECT id FROM tenants);

-- Review the row counts MySQL prints for each DELETE above before trusting this commit.
-- If anything looks wrong, run `ROLLBACK;` instead of letting this script finish.
COMMIT;
