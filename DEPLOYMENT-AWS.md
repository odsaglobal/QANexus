# PatternsAIAutomation — Deployment on AWS Ubuntu EC2

Assumptions: external Postgres, your own `appsettings.Production.json`, `frontend/.env` for Auth0, uploads through WinSCP or scp, and **no purchased domain** (a free `sslip.io` hostname gives you HTTPS).

> **Secrets warning:** `appsettings.Production.json` and `frontend/.env` contain credentials. Never commit them, and never commit the `publish` folder once the Production file is copied into it.

Throughout this guide, `<IP>` is your Elastic IP (for example `16.16.186.11`) and `<HOST>` is its sslip.io name (`16-16-186-11.sslip.io`, same IP with dashes).

## 1. Create the EC2 instance (AWS Console)

1. **Launch instance:**
   - AMI: **Ubuntu Server 24.04 LTS** (x86_64). **Do not use 26.04**: Playwright fails with `Playwright does not support chromium on ubuntu26.04-x64`.
   - Type: **t3.medium** (4 GB RAM) or larger. t3.small (2 GB) is workable for light use with swap. t3.micro (1 GB) is a smoke test only: Chromium plus the API will run out of memory.
   - Key pair: create a `.pem` and keep it safe.
   - Storage: 30 GB gp3.
2. **Security group, inbound:** SSH 22 from My IP, HTTP 80 and HTTPS 443 from `0.0.0.0/0`. Port 80 must stay open: Let's Encrypt validates through it.
3. **Elastic IP:** allocate one and associate it with the instance (the sslip.io name depends on a fixed IP).
4. **Postgres access:** allow the EC2 instance to reach your Postgres on port 5432 (add the app's security group as a source on the DB's security group).
5. **Connect:** `ssh -i C:\path\key.pem ubuntu@<IP>`, or EC2 Console → Connect → EC2 Instance Connect (browser terminal, also as `ubuntu`).

Optional swap for small instances (t3.micro/small):

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

## 2. Prepare the server (on EC2)

Run the install commands, then **verify each tool installed**. Missing packages caused most of the problems in the first deployment.

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y nginx curl unzip certbot python3-certbot-nginx aspnetcore-runtime-10.0
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash - && sudo apt install -y nodejs
```

Verify:

```bash
dotnet --list-runtimes     # must list Microsoft.AspNetCore.App 10.0.x and Microsoft.NETCore.App 10.0.x
node -v                    # v20.x or newer
nginx -v
certbot --version
ls /etc/nginx/sites-available
```

Ubuntu 24.04's apt feed has .NET 10 (`aspnetcore-runtime-10.0`). If it is ever missing, use Microsoft's script:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
sudo bash dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
```

Create the service user and folders:

```bash
sudo useradd -r -m -s /bin/bash patternsai
sudo mkdir -p /opt/patternsaiautomation/{api,web,mcp} /var/lib/patternsaiautomation/storage
sudo chown -R ubuntu:ubuntu /opt/patternsaiautomation          # temporary, so uploads work
sudo chown -R patternsai:patternsai /var/lib/patternsaiautomation
```

What these do:
- `useradd` creates a dedicated service account (`-r` system account, `-m` home directory, which Playwright needs for its browser cache, `-s /bin/bash` so `sudo -u patternsai bash -c ...` works).
- `/opt/patternsaiautomation/api` holds the .NET app, `/web` the built frontend, `/mcp` the Playwright MCP server.
- `/var/lib/patternsaiautomation/storage` holds uploaded files, kept outside the code folders so redeploys don't wipe data.
- The `chown` to `ubuntu` is temporary so WinSCP/scp (logged in as `ubuntu`) can write. `/home/patternsai` is private by design; you do not need to open it (WinSCP "permission denied" there is expected).

## 3. Database (on your Postgres)

```sql
CREATE ROLE atip LOGIN PASSWORD '<strong-password>';
CREATE DATABASE atip OWNER atip;
```

Optional connectivity check from EC2:

```bash
sudo apt install -y postgresql-client && psql "host=<postgres-host> user=atip dbname=atip"
```

## 4. Build on Windows

**a) Create `E:\PatternsAIAutomation\QANexus\frontend\.env`** (your own Auth0 values, matching the backend). Vite bakes these into `dist` at build time, so this must exist before `npm run build`. Without it the build uses the baked-in dev tenant from `frontend/src/lib/auth0.ts`, which you probably do not own (so you cannot add your callback URL to it).

```
VITE_AUTH0_DOMAIN=<your-tenant>.us.auth0.com
VITE_AUTH0_CLIENT_ID=<your-spa-client-id>
VITE_AUTH0_AUDIENCE=https://qanexus-api
```

**b) Build the backend and frontend:**

```powershell
cd E:\PatternsAIAutomation\QANexus
dotnet publish src/ATIP.Api -c Release -r linux-x64 --self-contained false -o publish

cd frontend
npm ci
npm run build        # reads .env here
cd ..
```

Framework-dependent (`--self-contained false`) is smaller and gets runtime patches via `apt upgrade`. If you cannot install .NET 10 on the VM, use `--self-contained true`, skip the `aspnetcore-runtime` install, and change `ExecStart` to `/opt/patternsaiautomation/api/ATIP.Api` (then `chmod +x` it).

**c) Create `appsettings.Production.json`** and copy it into the `publish` folder. The name is case-sensitive on Linux. Keep a master copy outside `publish`, because a clean rebuild removes it.

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=<postgres-host>;Port=5432;Database=atip;Username=atip;Password=<strong-password>;SSL Mode=Require"
  },
  "Auth0": { "Domain": "<your-tenant>.us.auth0.com", "Audience": "https://qanexus-api", "ClientId": "<your-spa-client-id>" },
  "Encryption": { "ActiveKeyId": "default", "Keys": { "default": "<base64-key>" } },
  "Llm": { "Provider": "anthropic", "Model": "<model>", "ApiKey": "<key>" },
  "Cors": { "AllowedOrigins": [ "https://<HOST>" ] },
  "PlaywrightMcp": {
    "Enabled": true,
    "Command": "node",
    "Arguments": [ "/opt/patternsaiautomation/mcp/node_modules/@playwright/mcp/cli.js", "--headless", "--isolated", "--browser", "chromium", "--no-sandbox" ]
  },
  "Storage": { "RootPath": "/var/lib/patternsaiautomation/storage" }
}
```

Notes:
- Replace `<HOST>` with your sslip.io name. The CORS origin must be the exact `https://` origin the browser uses, otherwise API calls fail after login.
- Remove `SSL Mode=Require` if your Postgres has no SSL. If you use `Require` with a certificate Npgsql cannot validate, add `Trust Server Certificate=true`.
- Back up the encryption key; without it, stored credentials cannot be decrypted.
- Arrays (`Cors:AllowedOrigins`, `PlaywrightMcp:Arguments`) replace the base values entirely, so keep the lists complete.
- Do not upload your local `appsettings.Development.json` (it has Windows paths).
- Do not reuse `dist-package`; it contains Windows binaries.
- `tools/mcp/playwright-mcp.sh` is **not needed**: the config above runs `node` with `cli.js` directly (the `.sh` is just a wrapper around that).

## 5. Upload (WinSCP or scp)

In WinSCP, log in as `ubuntu` with your key, then type the path (Ctrl+O). The folders must be owned by `ubuntu` (see the redeploy section if they were already handed to `patternsai`).

| Local | Remote |
|---|---|
| `publish\*` (including `appsettings.Production.json`) | `/opt/patternsaiautomation/api/` |
| `frontend\dist\*` | `/opt/patternsaiautomation/web/` |
| `tools\mcp\package.json` and `package-lock.json` (not `node_modules`) | `/opt/patternsaiautomation/mcp/` |

Upload the **contents** of `publish` and `dist`, not the folders themselves. `ATIP.Api.dll` must end up directly in `/opt/patternsaiautomation/api/`, and `index.html` directly in `/opt/patternsaiautomation/web/`.

Or from PowerShell:

```powershell
$KEY = "C:\path\key.pem"; $HOST_ = "ubuntu@<IP>"
scp -i $KEY -r publish/*       "${HOST_}:/opt/patternsaiautomation/api/"
scp -i $KEY -r frontend/dist/* "${HOST_}:/opt/patternsaiautomation/web/"
scp -i $KEY tools/mcp/package.json tools/mcp/package-lock.json "${HOST_}:/opt/patternsaiautomation/mcp/"
```

## 6. Permissions and browsers (on EC2)

```bash
sudo chown -R patternsai:patternsai /opt/patternsaiautomation
sudo chmod 600 /opt/patternsaiautomation/api/appsettings.Production.json
sudo chmod -R o+rX /opt/patternsaiautomation/web          # so nginx can read the site
sudo chmod o+x /opt/patternsaiautomation

sudo -u patternsai bash -c 'cd /opt/patternsaiautomation/mcp && npm ci'

sudo npx --yes playwright install-deps chromium           # system libraries, once, as root
sudo -u patternsai bash -c 'cd /opt/patternsaiautomation/mcp && npx playwright install chromium'
sudo -u patternsai node /opt/patternsaiautomation/api/.playwright/package/cli.js install chromium
```

After this, `ubuntu` can no longer write to `/opt/patternsaiautomation` (WinSCP uploads fail until you run the first command in the redeploy section).

## 7. Database migrations

Check `src/ATIP.Api/Program.cs` around line 181. The README says migrations auto-apply only in Development. If Production does not apply them, run them from Windows against your DB. Without them the API starts but data calls fail with `relation ... does not exist`.

Direct connection (if your PC can reach the DB):

```powershell
cd E:\PatternsAIAutomation\QANexus
dotnet ef database update --project src/ATIP.Infrastructure --startup-project src/ATIP.Api `
  --connection "Host=<postgres-host>;Database=atip;Username=atip;Password=<strong-password>;SSL Mode=Require"
```

Via an SSH tunnel (if the DB is private):

```powershell
# terminal 1
ssh -i C:\path\key.pem -L 5433:<postgres-host>:5432 ubuntu@<IP>
# terminal 2: same dotnet ef command with Host=localhost;Port=5433
```

## 8. systemd service (on EC2)

`sudo nano /etc/systemd/system/patternsaiautomation.service`:

```ini
[Unit]
Description=PatternsAIAutomation API
After=network.target

[Service]
WorkingDirectory=/opt/patternsaiautomation/api
ExecStart=/usr/bin/dotnet /opt/patternsaiautomation/api/ATIP.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5125
Restart=always
User=patternsai

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now patternsaiautomation
systemctl status patternsaiautomation --no-pager
journalctl -u patternsaiautomation -n 50 --no-pager
curl -i http://127.0.0.1:5125/api/v1/projects     # expect 401, not 500 or connection refused
```

If systemd gives up after repeated failures ("Start request repeated too quickly"), reset it after fixing the cause:

```bash
sudo systemctl reset-failed patternsaiautomation && sudo systemctl start patternsaiautomation
```

## 9. nginx and HTTPS (on EC2)

**Why HTTPS is mandatory:** `auth0-spa-js` refuses to run on plain `http://<IP>` (console error `auth0-spa-js must run on a secure origin`), which shows up as a blank page. Auth0 also rejects non-HTTPS callback URLs except for localhost. The free `sslip.io` name resolves to your IP and works with Let's Encrypt.

`sudo nano /etc/nginx/sites-available/patternsaiautomation` (this creates the file; `sites-available` exists only if nginx is installed):

```nginx
server {
  listen 80;
  server_name <HOST>;          # exactly your sslip.io name, e.g. 16-16-186-11.sslip.io
  root /opt/patternsaiautomation/web;
  index index.html;

  location / { try_files $uri /index.html; }

  location /api/ {
    proxy_pass http://127.0.0.1:5125;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Forwarded-Proto $scheme;
    client_max_body_size 50m;
  }
  location /health { proxy_pass http://127.0.0.1:5125; }

  location /hubs/ {
    proxy_pass http://127.0.0.1:5125;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_read_timeout 3600s;
  }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/patternsaiautomation /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx

sudo certbot --nginx -d <HOST>
```

- `server_name` and the `-d` value must match exactly, or certbot reports "Could not automatically find a matching server block".
- When certbot asks, enter an email, accept the terms, and choose the HTTP to HTTPS redirect.
- If certbot hits a rate limit for sslip.io, use `<IP>.nip.io` (dots, e.g. `16.16.186.11.nip.io`) in both places and everywhere `<HOST>` appears.
- If you own a real domain, use it instead of `<HOST>` throughout.
- If nginx returns 403, check `sudo chmod o+x /opt/patternsaiautomation` and `sudo chmod -R o+rX /opt/patternsaiautomation/web`, and look at `sudo tail -20 /var/log/nginx/error.log`.

## 10. Auth0

In the Auth0 Dashboard → Applications → your SPA app → Settings, add (then **Save Changes**):

- Allowed Callback URLs: `https://<HOST>/login/callback`
- Allowed Logout URLs: `https://<HOST>/login`
- Allowed Web Origins: `https://<HOST>`

The app must be the one whose Client ID is baked into the frontend (your `frontend/.env`, or the default dev client ID if you built without one). If you do not own that tenant, create your own Auth0 tenant and SPA app, set `frontend/.env`, rebuild, re-upload `dist`, and match the `Auth0` section of `appsettings.Production.json`. A "Callback URL mismatch" error on the Auth0 page means this step is missing.

## 11. Verify

```bash
curl -i https://<HOST>/api/v1/projects     # expect 401, not 500
```

Open `https://<HOST>` (not the bare IP), log in, and run an Explore. If something fails:

| Symptom | Cause / fix |
|---|---|
| Blank page, console says `must run on a secure origin` | You opened `http://<IP>`; use the `https://<HOST>` URL |
| `status=203/EXEC` in `journalctl` | `dotnet` not installed or wrong `ExecStart` path; see step 2 |
| Auth0 "Callback URL mismatch" | Step 10 not done or wrong tenant/client |
| Login works but API calls fail (CORS errors in console F12) | Fix `Cors:AllowedOrigins` in `appsettings.Production.json`, then restart the service |
| `relation ... does not exist` in API logs | Migrations not applied (step 7) |
| Explore fails | Check the MCP path and Chromium dependencies; `journalctl -u patternsaiautomation` |
| `Playwright does not support chromium on ubuntu26.04-x64` | Relaunch the instance on Ubuntu 24.04 |

## Editing config on the server

`appsettings.Production.json` is owned by `patternsai` with mode 600, so WinSCP as `ubuntu` cannot edit it. Use the EC2 Instance Connect terminal (or SSH):

```bash
sudo nano /opt/patternsaiautomation/api/appsettings.Production.json
sudo systemctl restart patternsaiautomation
```

`sudo nano` keeps the owner and permissions.

## Redeploying later

1. Rebuild (`dotnet publish`, and `npm run build` if the frontend or its `.env` changed). Copy `appsettings.Production.json` into `publish` again if the folder was recreated.
2. On EC2, give `ubuntu` write access: `sudo chown -R ubuntu:ubuntu /opt/patternsaiautomation`
3. Upload through WinSCP or scp.
4. On EC2, hand it back and restart:

```bash
sudo chown -R patternsai:patternsai /opt/patternsaiautomation
sudo chmod 600 /opt/patternsaiautomation/api/appsettings.Production.json
sudo chmod -R o+rX /opt/patternsaiautomation/web
sudo systemctl restart patternsaiautomation
```
