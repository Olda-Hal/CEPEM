# Build number increment stage
FROM alpine as build-counter
COPY BUILD_NUMBER /tmp/current_build
RUN CURRENT=$(cat /tmp/current_build) && \
    NEW_BUILD=$((CURRENT + 1)) && \
    echo $NEW_BUILD > /tmp/new_build_number.txt

# Multi-stage build for React frontend
FROM node:18-alpine as build

WORKDIR /app

# Copy package files
COPY frontend/package*.json ./

# Install dependencies (use legacy peer deps to handle version conflicts)
RUN npm install --legacy-peer-deps

# Copy source code
COPY frontend/ .

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine

# Copy build files to nginx
COPY --from=build /app/build /usr/share/nginx/html

# Copy incremented build number as static file for frontend to fetch
COPY --from=build-counter /tmp/new_build_number.txt /usr/share/nginx/html/build_number.txt

# Copy nginx configuration
COPY frontend/nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 3000

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
