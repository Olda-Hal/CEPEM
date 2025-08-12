# Database API

Centrální databázový mikroservice pro CEPEM Healthcare systém. Tento service slouží jako jediný bod pro všechny databázové operace v systému.

## Funkcionalita

- **Centralizovaný přístup k databázi**: Všechny ostatní mikroservices komunikují s databází pouze přes tento service
- **Entity Framework Core**: Pro práci s MySQL databází
- **Automatické migrace**: Databáze se automaticky aktualizuje při spuštění s nejnovějším schématem
- **RESTful API**: Standardní HTTP endpointy pro CRUD operace
- **Swagger dokumentace**: Automaticky generovaná API dokumentace
- **Data seeding**: Automatické naplnění základních dat při prvním spuštění

## Podporované entity

*Žádné entity nejsou zatím nakonfigurovány - připraveno pro budoucí rozšíření*

## API Endpointy

### Health Check
- `GET /api/health` - Status kontrola service

### Migrations Management
- `GET /api/migrations/pending` - Seznam čekajících migrací
- `GET /api/migrations/applied` - Seznam aplikovaných migrací
- `POST /api/migrations/apply` - Ruční aplikace migrací
- `GET /api/migrations/connection-status` - Status databázového připojení

## Databázové migrace

Service automaticky aplikuje databázové migrace při spuštění. Schéma databáze se vytváří na základě Entity Framework modelů.

### Automatické migrace
- Migrace se spouštějí automaticky při startu aplikace
- Service kontroluje připojení k databázi před aplikací migrací
- Pokud databáze není dostupná, aplikace se spustí a bude opakovat pokusy o připojení

### Manuální správa migrací
```bash
# Kontrola stavu migrací
curl http://localhost:5001/api/migrations/pending
curl http://localhost:5001/api/migrations/applied

# Ruční aplikace migrací
curl -X POST http://localhost:5001/api/migrations/apply

# Kontrola připojení
curl http://localhost:5001/api/migrations/connection-status
```

### Vytvoření nové migrace
```bash
# V development prostředí
cd backend/DatabaseAPI
dotnet ef migrations add MigrationName
```

Service používá MySQL databázi s následujícím connection stringem:
```
Server=mysql;Database=cepem_healthcare;User=root;Password=root123;
```

## Docker

Service běží na portu 5001 a automaticky čeká na dostupnost MySQL databáze před spuštěním.

## Bezpečnost

- CORS konfigurace pro frontend komunikaci
- Soft delete pro zachování dat integrity
- Validace vstupních dat pomocí Data Annotations

## Budoucí rozšíření

- Implementace entit podle business požadavků
- JWT autentifikace pro API endpointy
- Caching mechanismus pro často používané dotazy
- Audit log pro sledování změn
- API rate limiting
- Automatické generování API endpointů pro nové entity
