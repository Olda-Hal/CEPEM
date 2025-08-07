<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

# CEPEM E-commerce Platform - Copilot Instructions

Tento projekt je komplexní e-commerce platforma postavená na mikroservice architektuře. Při práci s tímto projektem mějte na paměti následující:

## Architektura Projektu

### Frontend (React + TypeScript)
- Používáme React s TypeScript pro type safety
- Styling pomocí CSS3 s moderními funkcemi (Grid, Flexbox, CSS variables)
- Komunikace s backendem přes REST API
- Responzivní design s mobile-first přístupem

### Backend (.NET Mikroservices)
- Každá mikroservice je nezávislý .NET 8 Web API projekt
- Entity Framework Core pro práci s databází
- RESTful API design principy
- CORS konfigurace pro frontend komunikaci
- Swagger dokumentace pro každou službu

### Databáze (MySQL)
- Sdílená MySQL databáze pro všechny mikroservices
- Entity Framework Code First approach
- Automatické seeding dat při inicializaci

### Docker & DevOps
- Každá služba má vlastní Dockerfile
- Docker Compose pro orchestraci všech služeb
- Multi-stage builds pro optimalizaci
- Možnost horizontálního škálování

## Coding Standards

### C# (.NET Backend)
- Používej async/await pro všechny databázové operace
- Implement proper error handling a logging
- Dodržuj RESTful konvence (GET, POST, PUT, DELETE)
- Používej dependency injection
- Entity Framework konvence pro naming

### TypeScript/React Frontend
- Používej TypeScript interfaces pro type definitions
- Functional components s hooks
- Proper error handling s try-catch
- Environment variables pro konfiguraci
- Semantic HTML a accessibility

### Docker
- Multi-stage builds pro production optimalizaci
- Proper layer caching pro rychlejší builds
- Health checks pro služby
- Environment variable konfigurace

## Konvence

### API Endpointy
- `/api/[controller]` pro všechny controllers
- RESTful resource naming (plural nouns)
- Consistent response formats
- Proper HTTP status codes

### Databáze
- PascalCase pro tabulky a sloupce (C# konvence)
- Proper foreign key relationships
- Index optimalizace pro často používané queries

### Naming Conventions
- PascalCase pro C# classes, methods, properties
- camelCase pro TypeScript/JavaScript variables, functions
- kebab-case pro CSS classes
- SCREAMING_SNAKE_CASE pro environment variables

## Při přidávání nových funkcí:

1. **Nová mikroservice**: Vytvořte novou složku v `backend/`, přidejte Dockerfile, aktualizujte docker-compose.yml
2. **Nové API endpointy**: Dodržujte RESTful konvence a přidejte je do API Gateway
3. **Databázové změny**: Používejte Entity Framework migrační systém
4. **Frontend komponenty**: Vytvářejte reusable, typed components
5. **Styling**: Používejte existing CSS variables a design patterns

## Security Considerations
- Validace všech vstupů
- Proper CORS konfigurace
- Environment variables pro citlivé informace
- SQL injection protection přes Entity Framework

## Performance
- Lazy loading pro Entity Framework
- React component optimization (memo, useMemo, useCallback)
- Docker image optimization
- Database indexing

Vždy myslěte na škálovatelnost, udržovatelnost a bezpečnost při implementaci nových funkcí.
