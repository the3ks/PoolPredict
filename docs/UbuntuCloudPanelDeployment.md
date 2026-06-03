# Ubuntu + CloudPanel Deployment Guide

This guide deploys PoolPredict to an Ubuntu VM with CloudPanel, MariaDB, and nginx.

The recommended production layout uses CloudPanel as the public site manager:

- CloudPanel `Node.js Site`: `https://poolpredict.example.com`
- CloudPanel app port: `3000`, forwarded to the Next.js app
- CloudPanel database: local MariaDB database and user attached to the site
- Custom CloudPanel vhost rule: `/api/` proxies to the .NET API on `127.0.0.1:5080`
- systemd service: keeps only the .NET API running

This keeps CloudPanel responsible for domain ownership, the site user, TLS, nginx, Node version selection, database management, logs, and site-level settings.

Using a same-origin `/api` route is intentional. The API currently allows only `http://localhost:3000` in `apps/api/Program.cs`; same-origin proxying avoids production browser CORS issues without exposing the API on a second public domain.

## 1. Create The CloudPanel Site

In CloudPanel:

1. Go to **Sites**.
2. Click **Add Site**.
3. Select **Node.js Site**.
4. Set:

```text
Domain Name: poolpredict.example.com
Node.js Version: 22
App Port: 3000
Site User: poolpredict
```

CloudPanel creates the site, the site user, the nginx vhost, and the reverse proxy from the public domain to app port `3000`.

Alternative CLI command as root:

```bash
clpctl site:add:nodejs \
  --domainName=poolpredict.example.com \
  --nodejsVersion=22 \
  --appPort=3000 \
  --siteUser=poolpredict \
  --siteUserPassword='REPLACE_WITH_STRONG_PASSWORD'
```

After creation, use **Sites > poolpredict.example.com > Settings** to manage:

- Node.js version
- App port
- Site user's SSH keys/password
- Root directory
- PageSpeed
- Delete-site lifecycle

Use CloudPanel to issue the Let's Encrypt certificate for the site after DNS points to the VM.

## 2. Create The MariaDB Database In CloudPanel

In CloudPanel:

1. Open **Sites > poolpredict.example.com**.
2. Go to **Databases**.
3. Create a database and user.

Suggested values:

```text
Database: poolpredict
User: poolpredict
Password: REPLACE_WITH_STRONG_PASSWORD
Host: 127.0.0.1
Port: 3306
```

Alternative CLI command as root:

```bash
clpctl db:add \
  --domainName=poolpredict.example.com \
  --databaseName=poolpredict \
  --databaseUserName=poolpredict \
  --databaseUserPassword='REPLACE_WITH_STRONG_PASSWORD'
```

Production connection string:

```text
Server=127.0.0.1;Port=3306;Database=poolpredict;User=poolpredict;Password=REPLACE_WITH_STRONG_PASSWORD;
```

## 3. Install Server Dependencies

CloudPanel provides nginx, MariaDB management, and Node.js site support. Install only the extra build/runtime tools needed by this repo:

```bash
sudo apt update
sudo apt install -y git curl build-essential mariadb-client
```

Install the .NET SDK/runtime required by the API. The API currently targets `net10.0`, so install the matching .NET SDK from Microsoft when .NET 10 is available for your Ubuntu version. If hosting before .NET 10 GA, use the Microsoft preview feed or retarget the app deliberately.

Verify:

```bash
dotnet --info
```

## 4. Clone The App As The CloudPanel Site User

SSH as the CloudPanel site user, or switch to it:

```bash
sudo -iu poolpredict
```

CloudPanel stores site files under the site user's home. Use the site directory CloudPanel created for the domain:

```bash
cd /home/poolpredict/htdocs
git clone https://github.com/YOUR_ORG/YOUR_REPO.git poolpredict.example.com
cd /home/poolpredict/htdocs/poolpredict.example.com
```

If CloudPanel already created a non-empty domain directory, clone into a temporary folder and move the repo contents into the domain directory.

## 5. Configure Production Settings

Create the API production settings file:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com
nano apps/api/appsettings.Production.json
```

Use:

```json
{
  "ConnectionStrings": {
    "MariaDb": "Server=127.0.0.1;Port=3306;Database=poolpredict;User=poolpredict;Password=REPLACE_WITH_STRONG_PASSWORD;"
  },
  "WebApp": {
    "BaseUrl": "https://poolpredict.example.com"
  },
  "EventProvider": {
    "Provider": "Mock",
    "FootballData": {
      "BaseUrl": "https://api.football-data.org/v4",
      "ApiToken": "",
      "CompetitionCode": "WC",
      "Season": 2026,
      "TournamentName": "FIFA World Cup 2026",
      "Sport": "Football",
      "StartsOn": "2026-06-11",
      "EndsOn": "2026-07-19"
    },
    "VirtualProvider": {
      "BaseUrl": "http://localhost:5090"
    }
  },
  "Markets": {
    "HandicapOpenWindowHours": 24
  }
}
```

For a real football-data.org provider, set:

```json
"Provider": "FootballData"
```

and configure `EventProvider:FootballData:ApiToken`.

Create the web production environment file:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com/apps/web
nano .env.production
```

Use:

```text
NEXT_PUBLIC_API_BASE_URL=https://poolpredict.example.com/api
```

`NEXT_PUBLIC_API_BASE_URL` is read at build time, so rebuild the web app whenever this value changes.

## 6. Apply EF Migrations

The API does not run migrations at startup. Apply migrations manually before first launch and before each release with schema changes.

As the `poolpredict` site user:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com
dotnet tool restore
dotnet ef database update \
  --project apps/api/PoolPredict.Api.csproj \
  --startup-project apps/api/PoolPredict.Api.csproj \
  --configuration Release
```

If the repo does not contain a local tool manifest for `dotnet ef`, install it for the site user:

```bash
dotnet tool install --global dotnet-ef
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
```

## 7. Publish And Run The .NET API

Publish the API into the site user's home:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com
dotnet publish apps/api/PoolPredict.Api.csproj \
  -c Release \
  -o /home/poolpredict/api-publish
```

Create the API systemd service as root:

```bash
sudo nano /etc/systemd/system/poolpredict-api.service
```

Use:

```ini
[Unit]
Description=PoolPredict API
After=network.target mariadb.service

[Service]
WorkingDirectory=/home/poolpredict/api-publish
ExecStart=/usr/bin/dotnet /home/poolpredict/api-publish/PoolPredict.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=poolpredict-api
User=poolpredict
Group=poolpredict
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now poolpredict-api
sudo systemctl status poolpredict-api --no-pager
curl http://127.0.0.1:5080/health
```

## 8. Build And Run The Next.js Site

As the `poolpredict` site user:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com/apps/web
npm ci
npm run build
```

CloudPanel's Node.js Site points nginx to port `3000`, but you still need a process manager for the Next.js process. Use PM2 under the site user:

```bash
npm install pm2 --location=global
pm2 start npm --name poolpredict-web -- run start -- --hostname 127.0.0.1 --port 3000
pm2 save
pm2 startup systemd -u poolpredict --hp /home/poolpredict
```

`pm2 startup` prints a root command. Run that printed command with `sudo`, then save again:

```bash
pm2 save
```

Verify locally:

```bash
curl -I http://127.0.0.1:3000
pm2 status
```

## 9. Add The API Proxy In CloudPanel Vhost Editor

In CloudPanel:

1. Open **Sites > poolpredict.example.com**.
2. Open **Vhost**.
3. Keep CloudPanel's generated server wrapper.
4. Add this `location` block before the generic `location /` block:

```nginx
location /api/ {
    proxy_pass http://127.0.0.1:5080/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

The trailing slash in `proxy_pass http://127.0.0.1:5080/;` is important. It strips `/api/` before forwarding, so `/api/health` reaches the API as `/health`.

Save in the Vhost Editor. CloudPanel validates nginx syntax and reverts invalid changes to avoid downtime.

Verify publicly:

```text
https://poolpredict.example.com
https://poolpredict.example.com/api/health
```

## 10. Deploy Updates

As the `poolpredict` site user:

```bash
cd /home/poolpredict/htdocs/poolpredict.example.com
git pull

dotnet ef database update \
  --project apps/api/PoolPredict.Api.csproj \
  --startup-project apps/api/PoolPredict.Api.csproj \
  --configuration Release

dotnet publish apps/api/PoolPredict.Api.csproj \
  -c Release \
  -o /home/poolpredict/api-publish

cd apps/web
npm ci
npm run build
pm2 restart poolpredict-web
```

As root:

```bash
sudo systemctl restart poolpredict-api
```

Verify:

```bash
curl https://poolpredict.example.com/api/health
sudo journalctl -u poolpredict-api -n 80 --no-pager
sudo -iu poolpredict pm2 logs poolpredict-web --lines 80
```

## 11. CloudPanel Operations Checklist

Use CloudPanel for:

- Site creation and deletion
- Node.js version and app port
- Site user SSH keys/password
- Database creation and credentials
- TLS certificates
- nginx vhost edits
- access/error logs
- optional Basic Auth during staging
- optional Cloudflare-only traffic controls

Use SSH/systemd/PM2 for:

- .NET SDK and API runtime
- EF migrations
- API process lifecycle
- Next.js build and process lifecycle

Keep only these ports public:

```text
80/tcp
443/tcp
8443/tcp only from trusted IPs if possible
```

Keep these local-only:

```text
127.0.0.1:3000  Next.js
127.0.0.1:5080  .NET API
127.0.0.1:3306  MariaDB
```

Back up the database before migrations:

```bash
clpctl db:export --databaseName=poolpredict --file=/home/poolpredict/poolpredict-backup.sql.gz
```

Important production notes:

- Keep database credentials out of Git. `appsettings.Production.json` should exist only on the server.
- The default development admin is seeded from development settings, not production. Configure production admin/identity behavior before launch.
- If you choose separate domains for API and web, update API CORS in `apps/api/Program.cs` to read allowed origins from configuration.
- Rebuild the Next.js app after changing `NEXT_PUBLIC_API_BASE_URL`.

## References

- CloudPanel Add Site: https://www.cloudpanel.io/docs/v2/frontend-area/add-site/
- CloudPanel Site Settings: https://www.cloudpanel.io/docs/v2/frontend-area/settings/
- CloudPanel Vhost Editor: https://www.cloudpanel.io/docs/v2/frontend-area/vhost/
- CloudPanel CLI root commands: https://www.cloudpanel.io/docs/v2/cloudpanel-cli/root-user-commands/

