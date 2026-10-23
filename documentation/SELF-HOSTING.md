# Self-hosting Centaur Scores

> 🇳🇱 **[Nederlandse versie → SELF-HOSTING.nl.md](SELF-HOSTING.nl.md)**

> **Just want to use Centaur Scores?** You don't need any of this. The software is hosted at [centaurscores.nl](https://centaurscores.nl): ask for access there by e-mail and you can create your first match straight away. This guide is for organisations that want to run their own server.

This guide covers three ways to host Centaur Scores yourself:

1. **[Docker Compose](#option-1-docker-compose)**: the quickest route on any machine with Docker.
2. **[Maintenance scripts for Debian](#option-2-debian-server-with-the-maintenance-scripts)**: a native install on a plain Debian VPS, with TLS and blue/green updates.
3. **[Manual installation](#option-3-manual-installation)**: build and wire up the components yourself.

---

## What you are installing

Centaur Scores consists of three web components behind one web address, plus a database:

| Path | Component | Purpose |
|---|---|---|
| `/` | `centaur-scores-web-ui` | Management web app for organisers, and the live display (`/narrowcast/<scope>`) |
| `/api/` | `centaur-scores-api-v2` | .NET 10 REST API, the only component that talks to the database |
| `/scores/` | `centaur-scores-mobile-web-scoring` | The score card that archers open by scanning a QR code |
| `/android/` | *(optional)* | Download location for the Android app's APK files |

- **Database:** MySQL 8.x or MariaDB. The API creates and upgrades the database schema itself when it starts.
- **Keep the score card at `/scores/`.** The QR codes printed from the management app point to `/scores` on the same address as the management app. To host the score card somewhere else, set *Scorekeeper app URL* under *Tenant settings* in the management app.
- **Use HTTPS in production.** Passwords and scores travel over the network, often over public or club Wi-Fi.

### First sign-in (all options)

On first start, the API creates a **Root Tenant** with one administrator account:

| Username | Password |
|---|---|
| `centaurscores` | `centaurscores` |

**Change this password immediately** after your first sign-in (profile menu → *Edit my profile*). Then:

1. Create an organisation (sub-tenant) for each club or region under *More → Sub-tenants*.
2. Create accounts with the right access level (*Administrator*, *Manager* or *Viewer*).
3. Set up categories (for example bow type and class) and an archer list, or skip this and let archers register as guests.
4. Create your first match, print the QR codes, and open `/narrowcast/<scope>` on a screen.

---

## Option 1: Docker Compose

Everything is built from source inside the containers: a MySQL 8.4 database, the API, and one nginx gateway that serves the two web apps and proxies the API. The gateway exposes a single **plain HTTP** port. Put a TLS-terminating reverse proxy in front of it (for example Caddy, Traefik, or an existing nginx).

**Requirements:** Docker with the Compose plugin, a few GB of disk space for the build, and a domain name pointing to the machine.

```sh
git clone https://github.com/pbijdens/centaur-scores-v2.git
cd centaur-scores-v2/centaur-scores-docker-v2
cp .env.example .env
$EDITOR .env
```

Set at least these values in `.env`:

| Variable | Meaning |
|---|---|
| `MYSQL_PASSWORD_ROOT`, `MYSQL_PASSWORD_APP` | Strong database passwords |
| `JWT_SECRET` | At least 32 random characters, for example from `openssl rand -hex 32` |
| `NGINX_PUBLIC_PORT` | The port the gateway listens on (your reverse proxy forwards to it) |
| `VOLUMES_MYSQL`, `VOLUMES_LOGS` | Host folders for database data and logs |
| `PUBLIC_API_URL` | The public address of the API, for example `https://scores.example.com/api/` |
| `PUBLIC_API_VDIR`, `PUBLIC_APP_VDIR` | Keep `/api/` and `/scores/` |

Then create the data folders and start everything:

```sh
sudo mkdir -p /srv/centaur-scores/mysql /srv/centaur-scores/logs   # match VOLUMES_* in .env
docker compose up -d --build
docker compose ps        # wait until all three services are "healthy"
curl -s http://localhost:<NGINX_PUBLIC_PORT>/api/health
```

`PUBLIC_API_URL` is compiled into the management app when it is built. If you change it later, run `docker compose up -d --build` again.

**Updating:**

```sh
git pull
docker compose up -d --build
```

More background is in [centaur-scores-docker-v2/DESIGN.md](../centaur-scores-docker-v2/DESIGN.md).

---

## Option 2: Debian server with the maintenance scripts

The [`centaur-scores-setup-and-maintenance`](../centaur-scores-setup-and-maintenance) toolkit installs everything natively (no Docker) on a Debian 12+ VPS. It sets up .NET, Node.js, MariaDB, nginx, a Let's Encrypt certificate, a firewall and systemd services. It also gives you **blue/green deployments**: a new version is built next to the running one, you can send a percentage of traffic to it, and you can promote it or roll back instantly.

**Requirements:** a Debian 12 ("bookworm") or newer server with root access (2 vCPU / 2 GB RAM is comfortable), a domain name whose DNS points to the server, and ports 80 and 443 open.

In short:

```sh
sudo mkdir -p /opt/centaur-scores-admin && sudo chown "$USER" /opt/centaur-scores-admin
git clone https://github.com/pbijdens/centaur-scores-v2.git /opt/centaur-scores-admin/centaur-scores-v2
cd /opt/centaur-scores-admin/centaur-scores-v2/centaur-scores-setup-and-maintenance
cp config.env.example config.env && chmod 600 config.env
$EDITOR config.env      # PUBLIC_HOSTNAME, LETSENCRYPT_EMAIL, MYSQL_PASSWORD_APP, JWT_SECRET
cd setup
sudo ./01-install-base-packages.sh
sudo ./02-create-service-user-and-dirs.sh
sudo ./03-install-dotnet-sdk.sh
sudo ./04-install-nodejs.sh
sudo ./05-install-mysql.sh
sudo ./06-install-nginx-and-certbot.sh
sudo ./07-configure-firewall.sh
sudo ./08-render-service-configs.sh
sudo ./09-obtain-tls-certificate.sh
sudo ./10-initial-deploy.sh
```

Optionally, run `sudo ./update/update-android-assets.sh` to publish the latest Android app at `/android/app-release.apk` and `/android/app-debug.apk`.

The full step-by-step guide, with notes on every script, is in:

- 📘 [INSTALL.md](../centaur-scores-setup-and-maintenance/INSTALL.md): requirements, configuration and first installation
- 🔄 [UPDATE.md](../centaur-scores-setup-and-maintenance/UPDATE.md): deploying new versions, canary traffic, promoting and rolling back

---

## Option 3: Manual installation

Use this if you want to fit Centaur Scores into an existing environment. You need:

- **.NET 10 SDK** (to build) or the ASP.NET Core 10 runtime (to run)
- **Node.js 24** (only to build the two web apps)
- **MySQL 8.x or MariaDB**
- **A web server / reverse proxy**, for example nginx

### 1. Database

Create an empty database and a user for the application:

```sql
CREATE DATABASE csdb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'csuser'@'localhost' IDENTIFIED BY 'a-strong-password';
GRANT ALL PRIVILEGES ON csdb.* TO 'csuser'@'localhost';
```

The API creates all tables on its first start.

### 2. API

```sh
cd centaur-scores-api-v2
dotnet publish CentaurScores.Api/CentaurScores.Api.csproj -c Release -o /srv/centaur-scores/api
```

Run it (for example as a systemd service) with these environment variables:

```sh
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080
ConnectionStrings__Default="server=localhost;port=3306;database=csdb;user=csuser;password=a-strong-password"
Jwt__Secret="at-least-32-random-characters"
dotnet /srv/centaur-scores/api/CentaurScores.Api.dll
```

Check that it works: `curl http://127.0.0.1:5080/health`.

### 3. Web apps

```sh
# Management app: the public API address is compiled in
cd centaur-scores-web-ui
npm ci
VITE_API_BASE_URL=https://scores.example.com/api/ npm run build
cp -r dist/. /srv/centaur-scores/web-ui/

# Score card: no configuration needed (it uses relative paths)
cd ../centaur-scores-mobile-web-scoring
npm ci
npm run build
cp -r dist/. /srv/centaur-scores/scores/
```

### 4. Web server

An example nginx configuration (add TLS, for example with certbot):

```nginx
server {
    listen 80;
    server_name scores.example.com;

    root /srv/centaur-scores/web-ui;
    index index.html;

    location ^~ /api/ {
        proxy_pass http://127.0.0.1:5080/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location ^~ /scores/ {
        alias /srv/centaur-scores/scores/;
        try_files $uri $uri/ /scores/index.html;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Both web apps are single-page apps: unknown paths must fall back to their `index.html`, as in the example above.

---

## Android app (optional)

Club tablets can run the Android app instead of the browser. Download `app-release.apk` (or `app-debug.apk` for older Android 6 devices) from the [latest GitHub release](https://github.com/pbijdens/centaur-scores-v2/releases/latest), or publish it on your own server at `/android/` (option 2 does this for you). The app pairs with a target by scanning the same QR code archers use, so it needs no server configuration.

## Backups

- **Database:** make regular dumps of the database (`mysqldump` / `mariadb-dump`).
- **Per organisation:** administrators can download a backup ZIP of an organisation, including its sub-organisations, and restore it from the management app. See [BACKUP-RESTORE.md](BACKUP-RESTORE.md).
