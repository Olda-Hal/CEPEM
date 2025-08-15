# CEPEM Healthcare System - Quick Start

## How to Run

### 1. Using VS Code Tasks
1. Open VS Code
2. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
3. Type "Tasks: Run Task"
4. Select "Build and Run CEPEM Platform"

### 2. Manual Docker Run
```bash
cd /home/olda/programovani/github/CEPEM
docker-compose up --build
```

### 3. Background Run
```bash
docker-compose up --build -d
```

## 📱 Application Access

After containers start:

- **Frontend (React)**: http://localhost:3000
- **Test Dashboard**: http://localhost:3000/tests
- **API**: http://localhost:5000
- **Database API**: http://localhost:5001 
- **MySQL Database**: localhost:3306

## 🧪 Testing System

The platform includes a comprehensive testing system that continues running even when individual tests fail:

- **Resilient Build**: Application starts regardless of test failures
- **Test Dashboard**: Monitor all test results at `/tests` route
- **Coverage Reports**: Detailed coverage analysis for all services
- **Live Status**: Real-time test execution monitoring

### Test Commands

```bash
# Build with tests (recommended)
./build.sh

# Build without tests
./build.sh --no-tests

# Run tests only
./build.sh --tests-only

# Clean build
./build.sh --clean
```

## Stop Services

```bash
docker-compose down
```
