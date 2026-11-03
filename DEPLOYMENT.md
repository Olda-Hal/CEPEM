# Production Deployment Guide

## Prerequisites

- Server with Ubuntu/Debian Linux
- Domain name pointing to your server IP
- Docker and Docker Compose installed
- Nginx installed on host server

## Step 1: Setup Domain DNS

Point your domain A record to your server IP address:
```
A    @              YOUR_SERVER_IP
A    www            YOUR_SERVER_IP
```

## Step 2: Install Required Software

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Install Docker Compose
sudo apt install docker-compose-plugin -y

# Install Nginx
sudo apt install nginx -y

# Install Certbot for SSL
sudo apt install certbot python3-certbot-nginx -y
```

## Step 3: Configure Environment Variables

```bash
cd /home/olda/programovani/CEPEM
cp .env.example .env  # then fill in real values
nano .env
```

Update all values in `.env`:
- `DOMAIN_NAME` - your actual domain
- All passwords with strong random values
- JWT secret (use: `openssl rand -base64 64`)
- Encryption keys (use: `openssl rand -hex 32` and `openssl rand -hex 16`)

## Step 4: Setup Nginx Reverse Proxy

```bash
# Copy nginx config to sites-available
sudo cp nginx-reverse-proxy.conf /etc/nginx/sites-available/cepem

# Replace YOUR_DOMAIN.com with your actual domain
sudo sed -i 's/YOUR_DOMAIN.com/your-actual-domain.com/g' /etc/nginx/sites-available/cepem

# Enable the site
sudo ln -s /etc/nginx/sites-available/cepem /etc/nginx/sites-enabled/

# Remove default site
sudo rm /etc/nginx/sites-enabled/default

# Test nginx configuration
sudo nginx -t

# Reload nginx
sudo systemctl reload nginx
```

## Step 5: Obtain SSL Certificate

```bash
# Create directory for certbot challenges
sudo mkdir -p /var/www/certbot

# Get certificate
sudo certbot certonly --webroot \
  -w /var/www/certbot \
  -d your-domain.com \
  -d www.your-domain.com \
  --email your-email@example.com \
  --agree-tos \
  --non-interactive

# Reload nginx with SSL
sudo systemctl reload nginx

# Setup auto-renewal
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer
```

## Step 6: Deploy Application

```bash
# Navigate to project directory
cd /home/olda/programovani/CEPEM

# Pull latest changes
git pull

# Build and start services
docker compose -f docker-compose.prod.yml up -d --build

# Check logs
docker compose -f docker-compose.prod.yml logs -f
```

Note: Container ports are exposed only to localhost (127.0.0.1), not publicly. Nginx reverse proxy handles all external traffic.

## Verification

1. Visit `https://your-domain.com` - should load frontend with valid SSL
2. Check SSL rating: `https://www.ssllabs.com/ssltest/`
3. Test API endpoints through the frontend

## SMTP / password-reset e-mail

`docker-compose.prod.yml` forwards these values to `database-api`:

```dotenv
FRONTEND_PUBLIC_URL=https://your-domain.com   # base of the reset link
SMTP_HOST=smtp.your-provider.com
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_USER=your-smtp-user
SMTP_PASSWORD=your-smtp-password
SMTP_FROM=noreply@your-domain.com
SMTP_FROM_NAME=CEPEM

# Pre-visit intake form reminder
INTAKE_REMINDER_ENABLED=true
INTAKE_REMINDER_LEAD_DAYS=3
INTAKE_REMINDER_CHECK_INTERVAL_MINUTES=15
```

Then recreate just the API container:

```bash
docker compose -f docker-compose.prod.yml up -d database-api
docker compose -f docker-compose.prod.yml logs --tail=100 database-api | grep -i smtp
```

Notes:
- `.NET System.Net.Mail` supports **STARTTLS on 587**; it does **not** support implicit SSL on 465.
- With `SMTP_HOST` empty the reset link is only written to the container log.
- The sending address must be allowed by the provider (SPF/DKIM helps deliverability).
- Test egress from the host: `openssl s_client -starttls smtp -connect smtp.your-provider.com:587 -crlf`.
- Reset links use `FRONTEND_PUBLIC_URL`; if it is `localhost`, e-mails point to localhost.
- The intake reminder is sent `INTAKE_REMINDER_LEAD_DAYS` before a **confirmed** reservation (only slots with status `Reserved`), to the e-mail on the booking. The reminder scan runs every `INTAKE_REMINDER_CHECK_INTERVAL_MINUTES`; links stay editable until the visit starts.
- Appointment SMS reminders run `SMS_REMINDER_LEAD_HOURS` before a **confirmed** reservation. The default `SMS_PROVIDER=log` writes each message to **Admin → Komunikační šablony → SMS log** without contacting an external service; set `SMS_PROVIDER=http` and `SMS_ENDPOINT` to POST `{ to, text, sender }` to a gateway (optional `SMS_API_KEY` becomes a Bearer token). The `appointment_reminder` wording is editable in the same admin page.
- E-mail and SMS templates resolve **Center → Country → Global**, with language fallback to English and then Czech. New databases are seeded with global Czech/English/Dutch wording; admins can add country- or center-specific overrides without changing code.
- Both APIs read the caller IP from `X-Forwarded-For`/`X-Forwarded-Proto` and trust loopback plus private Docker ranges by default (`ForwardedHeaders__ForwardLimit`, `ForwardedHeaders__KnownNetworks`, `ForwardedHeaders__KnownProxies`). Keep nginx as the outermost proxy so rate limiting and audit use the real client IP.
- Every send attempt (password reset and intake reminder) is recorded in **Admin → E-maily** with recipient, subject, type, status and the SMTP error for failed attempts. Message bodies and links are never stored in that log. Rows are pruned after `ActivityLogRetention:EmailLogRetentionDays` (default 365).

## Grafana

Grafana is bound to `127.0.0.1:3001` and published by nginx at
`https://${DOMAIN_NAME}/grafana/`; Prometheus stays on loopback and is only
used as a datasource. The repo's `nginx-reverse-proxy.conf` contains the
`/grafana/` location (including WebSocket upgrade and same-origin framing).

- `DOMAIN_NAME` must be set in the production `.env`; compose derives
  `GRAFANA_ROOT_URL=https://${DOMAIN_NAME}/grafana/` and enables
  `GRAFANA_SERVE_FROM_SUB_PATH` plus secure cookies from it.
- The frontend link is built by Vite. `VITE_GRAFANA_URL` is passed as a Docker
  **build arg**; when it is empty the SPA uses the same-origin `/grafana`
  path, so the link works on any hostname. After changing it, rebuild the
  frontend (a runtime env change is not enough):
  `docker compose -f docker-compose.prod.yml up -d --build frontend`.
- For an SSH tunnel instead of public hosting, set
  `GRAFANA_ROOT_URL=http://localhost:3001`,
  `GRAFANA_SERVE_FROM_SUB_PATH=false`, `GRAFANA_COOKIE_SECURE=false`, and
  build the frontend with `VITE_GRAFANA_URL=http://localhost:3001`:
  ```bash
  ssh -L 3001:127.0.0.1:3001 -L 9090:127.0.0.1:9090 user@your-server
  # then open http://localhost:3001
  ```
- Prometheus must find the provisioned datasource at `http://prometheus:9090` (not `localhost`). Check `deploy/grafana/provisioning/datasources/prometheus.yml` on the server; if it still says localhost, update it and recreate Grafana.
- Check scrape targets and raw metrics:
  ```bash
  curl -s http://127.0.0.1:9090/api/v1/targets | jq '.data.activeTargets[] | {scrapeUrl, health, lastError}'
  curl -s http://127.0.0.1:5000/metrics | head
  curl -s http://127.0.0.1:5001/metrics | head
  docker compose -f docker-compose.prod.yml logs --tail=100 prometheus grafana
  ```
- In Grafana open **Connections → Data sources → Prometheus → Test**; a query `up` should return both targets as `1`.
- The dashboard also has a **Requests by country** Geomap. It reads `cepem_http_requests_by_country_total{country,method,status_class}`. Country comes from the `X-Country-Code` request header, which nginx must set from GeoIP2 (or Cloudflare's `CF-IPCountry`). Without that header the panel shows no data because requests are counted as `unknown`.
  - Follow `deploy/nginx/geoip-country.conf.example` (install `libnginx-mod-http-geoip2` + `GeoLite2-Country.mmdb`, add the `map`/`geoip2` block to `http {}`, and `proxy_set_header X-Country-Code $cepem_country_code;` inside `location /api/`).
  - Reload nginx and recreate `healthcare-api`: `sudo nginx -t && sudo systemctl reload nginx && docker compose -f docker-compose.prod.yml up -d healthcare-api`.
  - Verify the label: `curl -s http://127.0.0.1:5000/metrics | grep cepem_http_requests_by_country_total`.
  - The dashboard is re-provisioned every 30 s; if the panel is still missing, run `docker compose -f docker-compose.prod.yml up -d --force-recreate grafana`. The map only fills from requests made after the updated HealthcareAPI/nginx are running.

## Maintenance

### View Logs
```bash
docker compose -f docker-compose.prod.yml logs -f [service-name]
```

### Update Application
```bash
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

### Backup Database
```bash
# Encrypted + verified backup (recommended). Configure BACKUP_ENCRYPTION_PASSPHRASE
# (or BACKUP_GPG_RECIPIENT) and optionally BACKUP_REMOTE in .env first.
scripts/backup_daily.sh

# Manual one-off:
docker exec cepem_mysql mysqldump -u root -p cepem_healthcare > backup_$(date +%Y%m%d).sql
```

The script writes the MySQL dump plus `data/` into `backups/<timestamp>/`,
encrypts both when a passphrase/key is configured, optionally copies them
offsite with rclone, verifies archive integrity, and (with
`BACKUP_RESTORE_TEST=true`) restores the dump into a temporary database.
Keep backups off the application disk and test a restore regularly.

### Renew SSL (automatic via certbot timer)
```bash
sudo certbot renew --dry-run  # test renewal
```

## Firewall Configuration

```bash
# Allow only HTTP, HTTPS, and SSH
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

## Security Notes

- Never commit `.env` file with production credentials
- Use strong passwords (minimum 32 characters)
- Regularly update Docker images
- Monitor logs for suspicious activity
- Keep SSL certificates renewed
- Enable fail2ban for SSH protection
