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
- **API**: http://localhost:5000
- **MySQL Database**: localhost:3306

## Stop Services

```bash
docker-compose down
```
