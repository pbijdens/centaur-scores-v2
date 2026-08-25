# centaur-scores-setup-and-maintenance

Native (non-Docker) install and blue/green update tooling for a Debian VPS
running all three CentaurScores web components behind one nginx gateway:

- `centaur-scores-api-v2` (.NET 10 Web API)
- `centaur-scores-web-ui` (Svelte, served at `/`)
- `centaur-scores-mobile-web-scoring` (Svelte, served at `/scores/`)

(`centaur-scores`, the Flutter app, is out of scope - it is not installed on
this server.)

This mirrors what `centaur-scores-docker-v2` does in a container, but builds
and runs the three components directly on the host via systemd, and adds a
blue/green deployment model with percentage-based canary traffic.

- **[INSTALL.md](INSTALL.md)** - system requirements, packages, and the
  first-time setup procedure (run the numbered scripts in `setup/` in order).
- **[UPDATE.md](UPDATE.md)** - how to ship a new version alongside the
  running one, shift a percentage of traffic to it, and promote or roll back
  (use the scripts in `update/`).
- `config.env.example` - copy to `config.env` and fill in your values; every
  script reads its configuration from there.
- `lib/common.sh` - shared shell functions sourced by every script.
- `templates/` - nginx, systemd and logrotate config templates rendered by
  the scripts.
