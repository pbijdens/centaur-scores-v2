# Installing CentaurScores on a Debian VPS

This installs all three web components natively (no Docker) on one Debian
server, behind a single nginx gateway with Let's Encrypt TLS:

| Path              | Component                          |
|-------------------|-------------------------------------|
| `/`               | `centaur-scores-web-ui`             |
| `/api/`           | `centaur-scores-api-v2`             |
| `/scores/`        | `centaur-scores-mobile-web-scoring` |

It also stands up the blue/green infrastructure used later for updates (see
[UPDATE.md](UPDATE.md)): two identical slots, `blue` and `green`, of which one
is always "active" (serving 100% of traffic by default) and the other is the
"candidate" for the next release.

`centaur-scores` (the Flutter app) is **not** part of this install.

## 1. System requirements

- A Debian VPS (Debian 12 "bookworm" or newer), reachable on the public
  internet, with `sudo`/root access. 2 vCPU / 2GB RAM is comfortable for two
  API instances plus nginx; 1GB RAM works but leaves little headroom.
- A domain name you control, with DNS pointed at this server's public IP
  (A/AAAA record). This is entirely on you - none of these scripts touch
  DNS. Default hostname used throughout this repo's examples:
  `archeryscores.net42.org`, but it's a config value, not a requirement.
- Outbound internet access from the server (to `github.com`, the npm
  registry, the NodeSource/dotnet install endpoints, and Let's Encrypt).
- Ports 80 and 443 open inbound (the setup scripts configure `ufw` for this).

### Packages installed by the setup scripts

You do not need to install these yourself - `setup/01`-`setup/06` do it -
but for reference, this is everything that ends up on the box:

| Purpose                         | Package(s) / source                                         |
|----------------------------------|---------------------------------------------------------------|
| Build tooling, TLS libs, envsubst | `build-essential curl wget gnupg git unzip gettext-base logrotate libicu-dev libssl-dev zlib1g` (apt) |
| Firewall                        | `ufw` (apt)                                                    |
| .NET 10 SDK                     | Microsoft's `dotnet-install.sh`, installed to `/opt/dotnet`    |
| Node.js 24.x                    | NodeSource apt repository (matches the `node:24` image the Docker build uses) |
| Database                        | `mariadb-server` (apt) - see note below                       |
| Reverse proxy                   | `nginx` (apt)                                                  |
| TLS certificates                | `certbot` + `python3-certbot-nginx` (apt)                     |

**Why MariaDB instead of MySQL 8.4 (which the Docker image uses)?** Debian's
own repositories only ship MariaDB - real Oracle MySQL requires adding
Oracle's third-party apt repository, whose package names/URLs change across
releases and which needs non-interactive debconf preseeding to script
reliably. MariaDB is a maintained, wire-compatible drop-in here: the API's
`Pomelo.EntityFrameworkCore.MySql` provider supports it directly, a fresh
Debian MariaDB install defaults `root` to local `unix_socket` auth (so the
install script doesn't need to juggle a root password), and it's one `apt
install` away. If you specifically need Oracle MySQL, replace
`setup/05-install-mysql.sh` with the equivalent steps for the `mysql-apt-config`
package and re-point `ConnectionStrings__Default` accordingly - nothing else
in this toolkit assumes one or the other.

## 2. Architecture this produces

```
Internet
   |
   |  :80 / :443 (TLS via certbot)
   v
 nginx  --------------------------------------------------------------
   |  cookie "csv_slot" pins each visitor to blue or green            |
   |  (new visitors assigned by percentage - see UPDATE.md)           |
   |                                                                  |
   |-- /            -> releases/web-ui/{blue,green}/  (static files)  |
   |-- /scores/     -> releases/scoring/{blue,green}/ (static files)  |
   |-- /api/        -> 127.0.0.1:5101 (blue) or :5102 (green)         |
   ----------------------------------------------------------------- -
                              |
                     centaur-scores-api-{blue,green}.service (systemd)
                              |
                          MariaDB (127.0.0.1:3306, one shared database)
```

Both API slots talk to the **same** database - there's only one. See
"Database migrations across slots" in [UPDATE.md](UPDATE.md) for what that
means for how you ship schema changes.

### On-disk layout

```
/srv/centaur-scores/
  releases/
    api/{blue,green}       -> symlinks into api/_builds/<slot>-<timestamp>/
    web-ui/{blue,green}    -> symlinks into web-ui/_builds/<slot>-<timestamp>/
    scoring/{blue,green}   -> symlinks into scoring/_builds/<slot>-<timestamp>/
  build/                   scratch git-clone/build workspace (wiped per deploy)
  state/                   active-slot, candidate-weight, deployed-<slot>.txt
  logs/                    api-blue.log, api-green.log, nginx-access.log, nginx-error.log (+ rotated *.gz)

/etc/centaur-scores/       common.env, api-blue.env, api-green.env (secrets, root:centaur-scores 640)
/etc/nginx/sites-available/centaur-scores.conf
/etc/nginx/conf.d/centaur-scores-upstreams.conf   (blue/green routing - rewritten on every traffic change)
/etc/systemd/system/centaur-scores-api-{blue,green}.service
/etc/systemd/system/centaur-scores-logs.{service,timer}
/etc/logrotate.d/centaur-scores-{api,nginx}
```

Everything runs as a dedicated, unprivileged system user (`centaur-scores`
by default, no login shell). Its directories are `0750` (owner + group only,
nothing for "other"), and `setup/06-install-nginx-and-certbot.sh` adds
`www-data` (nginx's worker user on Debian) to that group so nginx can read
the deployed release directories without the tree being world-readable -
`state/`, `build/` and `logs/` stay private to the `centaur-scores` user.

## 3. Get the scripts onto the server

Clone the monorepo somewhere on the server just to get this toolkit (this is
separate from the per-deploy clones the update scripts make later):

```sh
sudo mkdir -p /opt/centaur-scores-admin
sudo chown "$USER" /opt/centaur-scores-admin
git clone https://github.com/pbijdens/centaur-scores-v2.git /opt/centaur-scores-admin/centaur-scores-v2
cd /opt/centaur-scores-admin/centaur-scores-v2/centaur-scores-setup-and-maintenance
```

## 4. Configure

```sh
cp config.env.example config.env
chmod 600 config.env
$EDITOR config.env
```

At minimum, set real values for `PUBLIC_HOSTNAME`, `LETSENCRYPT_EMAIL`,
`MYSQL_PASSWORD_APP`, and `JWT_SECRET` (generate one with `openssl rand -hex
32`). Every field is documented inline in `config.env.example`.

## 5. Run the setup scripts, in order

All of them must be run as root (`sudo ./setup/NN-....sh`) from inside
`centaur-scores-setup-and-maintenance/`.

```sh
cd setup
sudo ./01-install-base-packages.sh      # apt update/upgrade + build tools
sudo ./02-create-service-user-and-dirs.sh
sudo ./03-install-dotnet-sdk.sh
sudo ./04-install-nodejs.sh
sudo ./05-install-mysql.sh              # creates the app database + user
sudo ./06-install-nginx-and-certbot.sh
sudo ./07-configure-firewall.sh         # interactive confirmation before enabling ufw
sudo ./08-render-service-configs.sh     # writes systemd units, logrotate, nginx config (HTTP only so far)
sudo ./09-obtain-tls-certificate.sh     # requires DNS already pointing here; asks you to confirm
sudo ./10-initial-deploy.sh [branch]    # default branch: main. Builds + deploys all three components to 'blue'
```

Notes:

- **07** pauses for a typed `yes` before enabling `ufw`, since a firewall
  misconfiguration can lock you out over SSH. If you connect on a
  non-standard SSH port, edit that script to `ufw allow <port>/tcp` first.
- **09** will fail (loudly, safely) if DNS for `PUBLIC_HOSTNAME` doesn't yet
  resolve to this server. Fix DNS and re-run it - it's idempotent.
- **10** is just the first call to `update/deploy.sh blue <branch>` - see
  [UPDATE.md](UPDATE.md) for what it does. After it succeeds, `blue` is
  active at 100% and the site is fully live.

## 6. Verify

```sh
curl -I https://<your-hostname>/
curl -s https://<your-hostname>/api/health
curl -I https://<your-hostname>/scores/
sudo /opt/centaur-scores-admin/centaur-scores-v2/centaur-scores-setup-and-maintenance/update/status.sh
```

`status.sh` prints which slot is active, each slot's deployed version,
systemd service state, and a health check against each slot's loopback port.

## 7. Logs

Everything lands in `/srv/centaur-scores/logs/`:

- `api-blue.log`, `api-green.log` - stdout/stderr of each API instance.
- `nginx-access.log`, `nginx-error.log`.

Rotation policy (enforced by `centaur-scores-logs.timer`, which runs hourly):

- No single log file exceeds **16MB** (`logrotate size 16M`).
- Rotated files older than **90 days** are deleted (`logrotate maxage 90`).
- If the *total* size of the log folder exceeds **1GB**, the oldest
  compressed/rotated files are deleted first until it's back under budget
  (`/usr/local/bin/centaur-scores-log-prune.sh`, run right after logrotate)
  - live `*.log` files are never touched by this step.

`journalctl -u centaur-scores-api-blue.service` also works for live tailing,
independent of the file-based logs.
