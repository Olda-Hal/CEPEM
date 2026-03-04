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
cp .env.production .env
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
docker exec cepem_mysql mysqldump -u root -p cepem_db > backup_$(date +%Y%m%d).sql
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
