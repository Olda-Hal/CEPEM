# CEPEM Healthcare System - Quick Start

## How to Run

### 1. Using VS Code Tasks
1. Open VS Code
2. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
3. Type "Tasks: Run Task"
4. Select "Build and Run CEPEM Platform"

### 2. Manual Docker Run
```bash
cd /home/olda/programovani/CEPEM

# First time only: create your .env from the template and fill in real values
cp .env.example .env

docker compose up --build
```

### 3. Background Run
```bash
docker compose up --build -d
```

## Application Access

After containers start:

- **Frontend (React)**: http://localhost:3000
- **API**: http://localhost:5000
- **Database API**: http://localhost:5001 (service-key protected, docker network only)
- **MySQL Database**: localhost:3306 (bound to 127.0.0.1)

## Running Tests

Tests run in CI (`.github/workflows/ci.yml`) and must pass for container
builds. Locally:

```bash
# Backend
dotnet test backend/DatabaseAPI.Tests/DatabaseAPI.Tests.csproj
dotnet test backend/HealthcareAPI.Tests/HealthcareAPI.Tests.csproj

# Frontend (vitest)
cd frontend && npm run test
```

## Stop Services

```bash
docker compose down
```
