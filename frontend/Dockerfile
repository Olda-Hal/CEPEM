# Multi-stage build for React frontend
FROM node:22.12-alpine AS build

WORKDIR /app

# Copy package files
COPY frontend/package*.json ./

# Install dependencies (use legacy peer deps to handle version conflicts)
RUN npm ci --legacy-peer-deps --no-audit --no-fund

# Copy source code
COPY frontend/ .

# Build args for API/Grafana URLs (passed from docker-compose.yml).
# Vite inlines these at build time, so they must be present before the build.
ARG VITE_API_URL
ARG VITE_GRAFANA_URL
ARG VITE_SENTRY_DSN

# Build the application
RUN npm run build

# Production stage
FROM nginxinc/nginx-unprivileged:1.27-alpine

# Copy build files to nginx
COPY --from=build /app/dist /usr/share/nginx/html

# Record the image build version (CI can pass -build-arg BUILD_NUMBER=<n>)
ARG BUILD_NUMBER=dev
RUN echo "$BUILD_NUMBER" > /usr/share/nginx/html/build_number.txt

# Copy nginx configuration
COPY frontend/nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 3000

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
