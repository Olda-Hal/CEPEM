# Testing Guide

## Overview

- **HealthcareAPI** - unit tests for controllers (C# + xUnit + Moq)
- **DatabaseAPI** - controller tests against an in-memory database (C# + xUnit)
- **Frontend** - unit/component tests (React + vitest + Testing Library)

CI (`.github/workflows/ci.yml`) runs all suites plus typecheck and the
container build; a failing test fails the build.

## Running Tests

### Backend
```bash
dotnet test backend/DatabaseAPI.Tests/DatabaseAPI.Tests.csproj
dotnet test backend/HealthcareAPI.Tests/HealthcareAPI.Tests.csproj

# With coverage
dotnet test backend/DatabaseAPI.Tests/DatabaseAPI.Tests.csproj \
  --collect:"XPlat Code Coverage" --logger trx
```

### Frontend
```bash
cd frontend
npm run test          # vitest, single run
npm run test:watch    # watch mode
npm run test:coverage # with coverage report
npm run build         # typecheck (tsc --noEmit) + production build
```

## Coverage Reality

There is no enforced coverage minimum today. Coverage is low: the suites
cover controller contracts and the intake-summary parsers, while the
highest-risk paths (auth flows, permission evaluation, reservation slot
overlap, intake-form links, document access control) still lack tests.
Adding tests for those paths is tracked in `REFACTOR_PLAN.md`.
