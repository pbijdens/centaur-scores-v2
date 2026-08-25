# Updating CentaurScores: blue/green + percentage rollout

There are always two slots, **blue** and **green**. At any moment one is
**active** (serves `100 - candidate_weight`% of new sessions) and the other
is the **candidate** (serves `candidate_weight`%, 0-100). A visitor's
browser is pinned to whichever slot it was first assigned, via a `csv_slot`
cookie (30-day lifetime) - so nobody bounces between old and new mid-session.

All three components (`centaur-scores-api-v2`, `centaur-scores-web-ui`,
`centaur-scores-mobile-web-scoring`) are built and deployed **together** as
one unit per slot, from one branch of the monorepo, because that's how the
source is structured - one clone, one branch, all three build outputs.

Every script below lives in `update/` and must be run as root from inside
`centaur-scores-setup-and-maintenance/`.

## The scripts

| Script | What it does |
|---|---|
| `deploy.sh <blue\|green> <branch>` | Clones `branch` from GitHub, builds all three components locally, and ships the result into the given slot. Restarts that slot's API and waits for it to report healthy. **Does not change traffic routing.** |
| `set-traffic.sh <0-100>` | Sends this percentage of *new* sessions to the candidate slot (the one that isn't active). |
| `promote.sh` | Makes the candidate slot the new active slot (100% of new sessions). The old active slot keeps running as the new candidate at 0%, ready for a fast rollback. |
| `rollback.sh` | Emergency: swaps the active slot back to the other one immediately, weight to 0%. Use if something promoted (or mid-canary) turns out to be broken. |
| `status.sh` | Shows current routing, each slot's deployed branch/commit, systemd status, and a health check. |
| `stop-slot.sh <blue\|green>` | Stops and disables a slot's API service (e.g. to free resources once you're done with it). Refuses to stop the active slot without `--force`. |

## Typical rollout

Say `blue` is active (100%) and you want to ship a new build.

```sh
cd update

# 1. Build and ship the new code into the idle slot. Does not affect traffic.
sudo ./deploy.sh green feature/my-branch

# 2. Send a small slice of new sessions to it and watch logs/metrics.
sudo ./set-traffic.sh 10
tail -f /srv/centaur-scores/logs/api-green.log

# 3. Ramp up as confidence grows.
sudo ./set-traffic.sh 50

# 4. Happy? Make it the new active slot.
sudo ./promote.sh
# green is now active (100%), blue is now the candidate at 0% - still
# running, so you can roll back instantly if something surfaces late.

# 5. Once you're confident you won't need to roll back, free the old slot.
sudo ./stop-slot.sh blue
```

If step 2 or 3 shows problems, just `sudo ./set-traffic.sh 0` to send
everyone back to blue - green stays deployed for you to debug, nothing to
undo. If problems only show up *after* `promote.sh`, run `sudo
./rollback.sh` to instantly swap back.

### A same-slot hotfix (no canary)

You can also deploy straight into the currently active slot - e.g. a tiny,
low-risk fix everyone should get immediately, with no gradual rollout:

```sh
sudo ./deploy.sh blue hotfix/typo
```

`deploy.sh` detects this and asks for a typed confirmation, since it
restarts the live API slot in place (a few seconds of downtime for that
slot while it restarts - the other slot, if serving any traffic, is
unaffected).

## What `deploy.sh` actually does

For the given `<slot>` and `<branch>`:

1. `git clone --branch <branch> --depth 1` the monorepo into a scratch
   workspace (`/srv/centaur-scores/build/<slot>/`).
2. **API**: `dotnet publish centaur-scores-api-v2/CentaurScores.Api -c Release`
   into a new timestamped build directory.
3. **web-ui**: `npm ci && npm run build`, with `VITE_API_BASE_URL` baked in
   at build time to `https://<PUBLIC_HOSTNAME><PUBLIC_API_VDIR>` (derived
   from `config.env` - both slots always point at the same public API URL,
   since nginx - not the build - decides which backend actually answers it).
4. **mobile scoring**: `npm ci && npm run build` (its Vite config already
   uses a relative base path so it works under `/scores/` regardless of slot).
5. Atomically flips the `releases/{api,web-ui,scoring}/<slot>` symlinks to
   point at the new build directories (via a `mv -T` rename, so nginx and
   systemd never see a half-updated slot).
6. Restarts `centaur-scores-api-<slot>.service` and polls
   `http://127.0.0.1:<slot-port>/health` for up to 30s. If it doesn't come
   up healthy, the script **fails loudly and does not touch traffic
   routing** - the previous routing state is untouched either way, since
   routing is a separate, later step.
7. Prunes old build directories for that slot, keeping the last
   `RELEASES_TO_KEEP` (default 3) per component, for quick manual rollback
   or inspection.

## Database migrations across slots

There is **one shared MariaDB database** for both slots (see
[INSTALL.md](INSTALL.md)). The API runs EF Core migrations automatically on
startup (`DatabaseInitializer.InitializeAsync`). This means:

- While a candidate slot is receiving any percentage of traffic (or even at
  0%, since its service is still running against the same DB), **both API
  versions are live against the same schema at the same time**.
- A migration in the new version must be backward-compatible with the old
  version still running - i.e. purely additive (new nullable columns, new
  tables) rather than destructive (dropped/renamed columns, changed types)
  for the duration of the rollout.
- Once you've promoted and stopped the old slot (`stop-slot.sh`), it's safe
  to ship a follow-up migration that cleans up the now-unused old schema
  shape, since only one version is running against the database at that point.

This is a standard constraint of any blue/green deployment sharing one
database, not something these scripts can enforce for you - review schema
changes with this in mind before deploying them as a candidate.

## Rendered config: what changes when

- `deploy.sh` never touches nginx config. It only swaps symlinks and
  restarts one systemd service.
- `set-traffic.sh`, `promote.sh`, `rollback.sh` only rewrite
  `/etc/nginx/conf.d/centaur-scores-upstreams.conf` (the blue/green
  percentage split) and reload nginx - a few-millisecond, zero-downtime
  operation.
- `/etc/nginx/sites-available/centaur-scores.conf` (the TLS/server block
  certbot edited during install) is never touched by any update script.

## Troubleshooting

- `update/status.sh` is the first stop - it shows routing state, per-slot
  version, systemd status, and a health probe.
- `sudo systemctl status centaur-scores-api-<slot>.service`
- `sudo journalctl -u centaur-scores-api-<slot>.service -n 200`
- `tail -n 200 /srv/centaur-scores/logs/api-<slot>.log`
- `sudo nginx -t` to check the current config is syntactically valid before
  it's reloaded (all traffic-routing scripts already do this automatically
  and abort the reload if it fails).
