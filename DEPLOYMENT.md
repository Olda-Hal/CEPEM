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

## Grafana

Grafana and Prometheus are bound to `127.0.0.1` only. From your workstation use an SSH tunnel:

```bash
ssh -L 3001:127.0.0.1:3001 -L 9090:127.0.0.1:9090 user@your-server
# then open http://localhost:3001
```

- `GRAFANA_ROOT_URL` must match the URL you use. For the tunnel use `http://localhost:3001`; for a public path use e.g. `https://your-domain.com/grafana/` **and** `GRAFANA_SERVE_FROM_SUB_PATH=true` plus an nginx `location /grafana/`. Without it Grafana redirects to its default `http://localhost:3000`.
- Prometheus must find the provisioned datasource at `http://prometheus:9090` (not `localhost`). Check `deploy/grafana/provisioning/datasources/prometheus.yml` on the server; if it still says localhost, update it and recreate Grafana.
- Check scrape targets and raw metrics:
  ```bash
  curl -s http://127.0.0.1:9090/api/v1/targets | jq '.data.activeTargets[] | {scrapeUrl, health, lastError}'
  curl -s http://127.0.0.1:5000/metrics | head
  curl -s http://127.0.0.1:5001/metrics | head
  docker compose -f docker-compose.prod.yml logs --tail=100 prometheus grafana
  ```
- In Grafana open **Connections → Data sources → Prometheus → Test**; a query `up` should return both targets as `1`.

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
# Prefer scripts/backup_daily.sh (retention, .env-aware). Manual one-off:
docker exec cepem_mysql mysqldump -u root -p cepem_healthcare > backup_$(date +%Y%m%d).sql
```

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
