# CEPEM Medical Platform - AI Agent Instructions

This is a microservice-based medical platform with .NET APIs, React frontend, and Python AI service. The project uses clean infrastructure setup ready for entity implementation.

## Architecture Overview

**Three-tier microservice design:**
- **HealthcareAPI** (port 5000): Main business logic, JWT auth, doctor management
- **DatabaseAPI** (port 5001): Centralized data access layer (currently empty - clean slate)
- **AIService** (port 8000): Python FastAPI for medical AI features
- **Frontend** (port 3000): React 18 + TypeScript with i18n support
- **MySQL** (port 3306): Shared database for all services

**Key architectural decision:** DatabaseAPI serves as single point of database access. All data operations should go through this service, not directly to MySQL from other APIs.

## Development Workflow

**Start everything:** Use `docker compose up --build`

**Database changes:**
1. Add entities to `DatabaseAPI/Models/`
2. Update `DatabaseContext.cs` with DbSet properties
3. Run migrations via DatabaseAPI endpoints: `/api/migration/apply`
4. No manual SQL scripts - use Entity Framework Code First

**New API endpoints:**
- HealthcareAPI: Business logic, authentication
- DatabaseAPI: Pure CRUD operations, follow pattern in existing controllers
- Both use `/api/[controller]` routing

## Critical Patterns

**Frontend API calls:** Use `apiClient` from `utils/api.ts` - handles auth headers automatically
```typescript
const response = await apiClient.get<Doctor[]>('/api/doctors');
```

**Authentication flow:** JWT tokens stored in localStorage, automatically included in requests

**CORS setup:** All APIs configured for `http://localhost:3000` and `http://frontend:3000`

**Docker networking:** Services communicate via container names (e.g., `mysql`, `healthcare-api`)

## Entity Framework Conventions

**DatabaseAPI is intentionally empty** - clean infrastructure waiting for your entities:
- Add models to `Models/` folder
- Configure in `DatabaseContext.OnModelCreating()`
- Use PascalCase for C# entities (follows EF conventions)
- All database access should go through DatabaseAPI, not direct EF calls from other services

## Frontend Structure

**TypeScript interfaces:** Define in `types/index.ts`, follow existing `Doctor` pattern
**i18n system:** Active with Czech/English support, use `t()` function for all text
**State management:** React Context for auth (`AuthContext.tsx`), hooks for other state

## Service Communication

**HealthcareAPI → DatabaseAPI:** HTTP calls for data operations
**Frontend → HealthcareAPI:** Authentication, business logic
**Frontend → DatabaseAPI:** Direct data access (if needed)
**AIService:** Independent, called via HTTP from frontend/other services

## Environment Configuration

Critical environment variables in `docker-compose.yml`:
- JWT settings in HealthcareAPI
- Database connection strings (note: different passwords for different services)
- CORS origins for all APIs

## Critical Rules
- Always write everything in English.
- Do not add useless comments. most of the code is self-explanatory.
- NEVER add any methods or functions that were not explicitly requested.
- Follow the established code style and conventions.
- Never add useless readme files or documentation. Use existing files. Always write only the essential information.
- Never use emojis in code comments or documentation.
- In APIs with ASP.NET always use the Controller, Service, Repository pattern.
- Use the existing API endpoints for database operations, do not create new ones unless necessary.
