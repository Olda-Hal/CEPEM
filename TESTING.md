# 🧪 Testing Guide

## Přehled testů

CEPEM platformě má implementované testy pro všechny mikroservisy:

- **HealthcareAPI** - Unit testy pro controllers a services (C# + xUnit)
- **DatabaseAPI** - Unit a integration testy (C# + xUnit)  
- **AIService** - Unit testy pro FastAPI endpoints (Python + pytest)
- **Frontend** - Component a unit testy (React + Jest + Testing Library)

## 🚀 Spuštění testů

### Všechny testy najednou
```bash
# Sestavit platformu a spustit všechny testy
./build.sh

# Pouze testy (bez buildu platformy)
./build.sh --tests-only

# Sestavit bez testů
./build.sh --no-tests

# Vyčistit vše a sestavit s testy
./build.sh --clean
```

### Individuální testy

#### HealthcareAPI Tests
```bash
cd backend/HealthcareAPI.Tests
dotnet test --collect:"XPlat Code Coverage" --logger trx
```

#### DatabaseAPI Tests  
```bash
cd backend/DatabaseAPI.Tests
dotnet test --collect:"XPlat Code Coverage" --logger trx
```

#### AIService Tests
```bash
cd backend/AIService
pip install -r requirements-test.txt
pytest --cov=. --cov-report=html --cov-report=xml
```

#### Frontend Tests
```bash
cd frontend
npm test -- --coverage --watchAll=false
```

## 📊 Test Coverage

### Minimální požadavky coverage:
- **Backend (.NET)**: 80% line coverage
- **AIService (Python)**: 80% line coverage  
- **Frontend (React)**: 80% line/function/branch coverage

### Generované reporty:
- **.NET**: `TestResults/CoverageReport/index.html`
- **Python**: `htmlcov/index.html`
- **React**: `coverage/lcov-report/index.html`

## 🐳 Docker Test Commands

```bash
# Spustit test containers
docker compose -f docker-compose.test.yml up --build

# Vyčistit test volumes
docker compose -f docker-compose.test.yml down --volumes

# Zobrazit test results
cat test-results/*/coverage.xml
find test-results -name "*.trx" -exec cat {} \;
```

## 📋 Test Output

Build script automaticky zobrazuje:
- ✅ Počet úspěšných/neúspěšných testů pro každý servis
- 📊 Test coverage procenta
- ❌ Detaily chyb pro neprošlé testy
- 📈 Celkové shrnutí všech mikroservisů

### Příklad výstupu:
```
==============================================
CEPEM Platform - Test Results Summary
==============================================

📊 HealthcareAPI Results:
   Total Tests: 12
   Passed: 12
   Failed: 0
   Line Coverage: 85.4%

📊 DatabaseAPI Results:
   Total Tests: 8
   Passed: 8  
   Failed: 0
   Line Coverage: 92.1%

📊 AIService Results:
   Total Tests: 6
   Passed: 6
   Failed: 0
   Line Coverage: 78.3%

📊 Frontend Results:
   Total Tests: 15
   Passed: 15
   Failed: 0
   Line Coverage: 88.7%

==============================================
📋 Overall Test Summary
==============================================
Services with tests: 4
Successful: 4
Failed: 0
✅ All tests completed successfully!
```

## 🔧 Troubleshooting

### Častí problémy:

1. **Test selhává kvůli chybějícím dependencies**
   ```bash
   # .NET
   dotnet restore
   
   # Python  
   pip install -r requirements-test.txt
   
   # React
   npm install
   ```

2. **Docker test containers selhávají**
   ```bash
   # Vyčistit Docker cache
   docker system prune -f
   docker compose -f docker-compose.test.yml build --no-cache
   ```

3. **Nízké test coverage**
   - Přidejte více unit testů
   - Ověřte, že jsou testovány všechny kritické cesty kódu
   - Zkontrolujte exclude patterns v coverage konfiguracích

## 📝 Přidání nových testů

### .NET Tests:
```csharp
[Fact]
public async Task Method_ReturnsExpected_WhenCondition()
{
    // Arrange
    var expected = "test";
    
    // Act
    var result = await _service.Method();
    
    // Assert
    Assert.Equal(expected, result);
}
```

### Python Tests:
```python
def test_endpoint_returns_success():
    response = client.get("/api/endpoint")
    assert response.status_code == 200
    assert response.json()["status"] == "success"
```

### React Tests:  
```typescript
test('component renders correctly', () => {
    render(<Component />);
    expect(screen.getByText('Expected Text')).toBeInTheDocument();
});
```
