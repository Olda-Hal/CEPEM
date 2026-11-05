# Multi-stage build for React frontend
FROM node:26.13-alpine AS build

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

# Record the image build version in the static bundle (the unprivileged
# runtime image cannot write into /usr/share/nginx/html).
ARG BUILD_NUMBER=dev
RUN echo "$BUILD_NUMBER" > dist/build_number.txt

# Production stage
FROM nginxinc/nginx-unprivileged:1.27-alpine

# Copy build files to nginx
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY frontend/nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 3000

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
