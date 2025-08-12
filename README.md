# Centrum Preventivni medicíny System

Microservice-based medical platform built with .NET, React, and Python AI service.

## Architecture

- Backend: .NET 8 Web API microservices
- Frontend: React with TypeScript
- AI Service: Python FastAPI
- Database: MySQL with Entity Framework Core
- Deployment: Docker containers

## Quick Start

```bash
git clone <repository-url>
cd CEPEM
docker compose up --build
```

## Services

- Frontend: http://localhost:3000
- Healthcare API: http://localhost:5000
- Database API: http://localhost:5001
- AI Service: http://localhost:8000

## Project Structure

```
backend/
├── HealthcareAPI/    # Main API service
├── DatabaseAPI/      # Database operations
└── AIService/        # AI features
frontend/             # React application
```

