# Multi-stage build for React frontend
FROM node:20-alpine AS build

WORKDIR /app

# Copy package files
COPY frontend/package*.json ./

# Install dependencies (use legacy peer deps to handle version conflicts)
RUN npm ci --legacy-peer-deps --no-audit --no-fund

# Copy source code
COPY frontend/ .

# Build args for API URLs (passed from docker-compose.yml)
ARG VITE_API_URL

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine

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
