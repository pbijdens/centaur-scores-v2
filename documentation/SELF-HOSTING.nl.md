# Centaur Scores zelf hosten

> 🇬🇧 **[English version → SELF-HOSTING.md](SELF-HOSTING.md)**

> **Wil je Centaur Scores alleen gebruiken?** Dan heb je dit allemaal niet nodig. De software draait op [centaurscores.nl](https://centaurscores.nl): vraag daar per e-mail toegang aan en je kunt direct je eerste wedstrijd aanmaken. Deze handleiding is voor organisaties die een eigen server willen draaien.

Deze handleiding beschrijft drie manieren om Centaur Scores zelf te hosten:

1. **[Docker Compose](#optie-1-docker-compose)**: de snelste route op elke machine met Docker.
2. **[Beheerscripts voor Debian](#optie-2-debian-server-met-de-beheerscripts)**: een native installatie op een kale Debian-VPS, met TLS en blue/green-updates.
3. **[Handmatige installatie](#optie-3-handmatige-installatie)**: bouw en koppel de onderdelen zelf.

---

## Wat je installeert

Centaur Scores bestaat uit drie webonderdelen achter één webadres, plus een database:

| Pad | Onderdeel | Doel |
|---|---|---|
| `/` | `centaur-scores-web-ui` | Beheer-webapp voor organisatoren, en het live-scherm (`/narrowcast/<scope>`) |
| `/api/` | `centaur-scores-api-v2` | .NET 10 REST-API, het enige onderdeel dat met de database praat |
| `/scores/` | `centaur-scores-mobile-web-scoring` | De scorekaart die schutters openen door een QR-code te scannen |
| `/android/` | *(optioneel)* | Downloadlocatie voor de APK-bestanden van de Android-app |

- **Database:** MySQL 8.x of MariaDB. De API maakt het databaseschema zelf aan en werkt het bij tijdens het opstarten.
- **Houd de scorekaart op `/scores/`.** De QR-codes die je uit de beheer-app print, wijzen naar `/scores` op hetzelfde adres als de beheer-app. Wil je de scorekaart ergens anders hosten, stel dan de *URL van de scorekeeper-app* in bij *Omgevingsinstellingen* in de beheer-app.
- **Gebruik HTTPS in productie.** Wachtwoorden en scores gaan over het netwerk, vaak via openbare wifi of de wifi van de vereniging.

### Eerste keer inloggen (alle opties)

Bij de eerste start maakt de API een **Root Tenant** aan met één beheerdersaccount:

| Gebruikersnaam | Wachtwoord |
|---|---|
| `centaurscores` | `centaurscores` |

**Wijzig dit wachtwoord direct** na de eerste keer inloggen (profielmenu → *Profiel bewerken*). Daarna:

1. Maak per vereniging of regio een omgeving aan via *Meer → Deelomgevingen*.
2. Maak accounts aan met de juiste rechten (*Beheerder*, *Manager* of *Kijker*).
3. Stel categorieën in (bijvoorbeeld boogtype en klasse) en een schutterslijst, of sla dit over en laat schutters zich als gast aanmelden.
4. Maak je eerste wedstrijd aan, print de QR-codes en open `/narrowcast/<scope>` op een scherm.

---

## Optie 1: Docker Compose

Alles wordt in de containers vanuit de broncode gebouwd: een MySQL 8.4-database, de API en één nginx-gateway die de twee webapps serveert en de API doorstuurt. De gateway biedt één poort met **gewone HTTP** aan. Zet er een reverse proxy met TLS voor (bijvoorbeeld Caddy, Traefik of een bestaande nginx).

**Vereisten:** Docker met de Compose-plugin, een paar GB schijfruimte voor het bouwen, en een domeinnaam die naar de machine wijst.

```sh
git clone https://github.com/pbijdens/centaur-scores-v2.git
cd centaur-scores-v2/centaur-scores-docker-v2
cp .env.example .env
$EDITOR .env
```

Stel in `.env` in elk geval deze waarden in:

| Variabele | Betekenis |
|---|---|
| `MYSQL_PASSWORD_ROOT`, `MYSQL_PASSWORD_APP` | Sterke databasewachtwoorden |
| `JWT_SECRET` | Minstens 32 willekeurige tekens, bijvoorbeeld uit `openssl rand -hex 32` |
| `NGINX_PUBLIC_PORT` | De poort waarop de gateway luistert (je reverse proxy stuurt daarheen door) |
| `VOLUMES_MYSQL`, `VOLUMES_LOGS` | Mappen op de host voor databasegegevens en logbestanden |
| `PUBLIC_API_URL` | Het openbare adres van de API, bijvoorbeeld `https://scores.example.com/api/` |
| `PUBLIC_API_VDIR`, `PUBLIC_APP_VDIR` | Laat op `/api/` en `/scores/` staan |

Maak daarna de datamappen aan en start alles:

```sh
sudo mkdir -p /srv/centaur-scores/mysql /srv/centaur-scores/logs   # gelijk aan VOLUMES_* in .env
docker compose up -d --build
docker compose ps        # wacht tot alle drie de services "healthy" zijn
curl -s http://localhost:<NGINX_PUBLIC_PORT>/api/health
```

`PUBLIC_API_URL` wordt bij het bouwen in de beheer-app ingebakken. Pas je die later aan, voer dan opnieuw `docker compose up -d --build` uit.

**Bijwerken:**

```sh
git pull
docker compose up -d --build
```

Meer achtergrond staat in [centaur-scores-docker-v2/DESIGN.md](../centaur-scores-docker-v2/DESIGN.md).

---

## Optie 2: Debian-server met de beheerscripts

De toolkit [`centaur-scores-setup-and-maintenance`](../centaur-scores-setup-and-maintenance) installeert alles native (zonder Docker) op een Debian 12+-VPS. Hij richt .NET, Node.js, MariaDB, nginx, een Let's Encrypt-certificaat, een firewall en systemd-services in. Je krijgt er ook **blue/green-deployments** bij: een nieuwe versie wordt naast de draaiende versie gebouwd, je kunt er een percentage van het verkeer naartoe sturen, en je kunt hem promoveren of direct terugdraaien.

**Vereisten:** een Debian 12 ("bookworm")-server of nieuwer met root-toegang (2 vCPU / 2 GB RAM is ruim voldoende), een domeinnaam waarvan de DNS naar de server wijst, en poorten 80 en 443 open.

In het kort:

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

Voer eventueel `sudo ./update/update-android-assets.sh` uit om de nieuwste Android-app te publiceren op `/android/app-release.apk` en `/android/app-debug.apk`.

De volledige stap-voor-staphandleiding, met uitleg bij elk script, staat in (Engelstalig):

- 📘 [INSTALL.md](../centaur-scores-setup-and-maintenance/INSTALL.md): vereisten, configuratie en eerste installatie
- 🔄 [UPDATE.md](../centaur-scores-setup-and-maintenance/UPDATE.md): nieuwe versies uitrollen, canary-verkeer, promoveren en terugdraaien

---

## Optie 3: Handmatige installatie

Gebruik deze route als je Centaur Scores in een bestaande omgeving wilt inpassen. Je hebt nodig:

- **.NET 10 SDK** (om te bouwen) of de ASP.NET Core 10-runtime (om te draaien)
- **Node.js 24** (alleen om de twee webapps te bouwen)
- **MySQL 8.x of MariaDB**
- **Een webserver / reverse proxy**, bijvoorbeeld nginx

### 1. Database

Maak een lege database en een gebruiker voor de applicatie aan:

```sql
CREATE DATABASE csdb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'csuser'@'localhost' IDENTIFIED BY 'een-sterk-wachtwoord';
GRANT ALL PRIVILEGES ON csdb.* TO 'csuser'@'localhost';
```

De API maakt bij de eerste start alle tabellen aan.

### 2. API

```sh
cd centaur-scores-api-v2
dotnet publish CentaurScores.Api/CentaurScores.Api.csproj -c Release -o /srv/centaur-scores/api
```

Start de API (bijvoorbeeld als systemd-service) met deze omgevingsvariabelen:

```sh
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080
ConnectionStrings__Default="server=localhost;port=3306;database=csdb;user=csuser;password=een-sterk-wachtwoord"
Jwt__Secret="minstens-32-willekeurige-tekens"
dotnet /srv/centaur-scores/api/CentaurScores.Api.dll
```

Controleer of het werkt: `curl http://127.0.0.1:5080/health`.

### 3. Webapps

```sh
# Beheer-app: het openbare API-adres wordt ingebakken
cd centaur-scores-web-ui
npm ci
VITE_API_BASE_URL=https://scores.example.com/api/ npm run build
cp -r dist/. /srv/centaur-scores/web-ui/

# Scorekaart: geen configuratie nodig (gebruikt relatieve paden)
cd ../centaur-scores-mobile-web-scoring
npm ci
npm run build
cp -r dist/. /srv/centaur-scores/scores/
```

### 4. Webserver

Een voorbeeldconfiguratie voor nginx (voeg TLS toe, bijvoorbeeld met certbot):

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

Beide webapps zijn single-page apps: onbekende paden moeten terugvallen op hun eigen `index.html`, zoals in het voorbeeld hierboven.

---

## Android-app (optioneel)

Tablets van de vereniging kunnen de Android-app gebruiken in plaats van de browser. Download `app-release.apk` (of `app-debug.apk` voor oudere Android 6-apparaten) van de [nieuwste GitHub-release](https://github.com/pbijdens/centaur-scores-v2/releases/latest), of zet hem op je eigen server onder `/android/` (optie 2 doet dat voor je). De app koppelt aan een baan door dezelfde QR-code te scannen die schutters gebruiken, dus er is geen serverconfiguratie nodig.

## Back-ups

- **Database:** maak regelmatig dumps van de database (`mysqldump` / `mariadb-dump`).
- **Per omgeving:** beheerders kunnen vanuit de beheer-app een back-up-ZIP van een omgeving downloaden, inclusief deelomgevingen, en die weer terugzetten. Zie [BACKUP-RESTORE.md](BACKUP-RESTORE.md) (Engelstalig).
