# CEPEM Healthcare System

A modern web application for healthcare administration built with ASP.NET Core and React.

## Tech Stack

- **Backend:** ASP.NET Core 8, Entity Framework Core, MySQL, JWT Authentication, Docker
- **Frontend:** React 19, TypeScript, CSS3

## Prerequisites

- Docker & Docker Compose
- Ports: 3000 (frontend), 5000 (API), 3306 (MySQL)

## Quick Start

1. **Clone the repository**
    ```bash
    git clone <repository-url>
    cd CEPEM
    ```
2. **Run the application**
    ```bash
    docker compose up --build
    ```
3. **Access the application**
    - Frontend: http://localhost:3000
    - API: http://localhost:5000

## Project Structure

```
CEPEM/
├── backend/           # ASP.NET Core API
├── frontend/          # React app
├── database/          # SQL init scripts
└── docker-compose.yml # Docker orchestration
```

## Demo Accounts

| Email                   | Password    | Role          |
|-------------------------|------------|---------------|
| jan.novak@cepem.cz      | password123| Doctor        |
| marie.svobodova@cepem.cz| password123| Doctor        |
| petr.dvorak@cepem.cz    | password123| Doctor        |
| admin@cepem.cz          | admin123   | Administrator |

## Features

- Secure login (JWT authentication)
- Doctor dashboard
- Profile management
- Responsive design
- Passwords hashed with BCrypt

## Configuration

- Environment variables are set in `docker-compose.yml`
    - `JWT__SecretKey`
    - `JWT__Issuer`
    - `JWT__Audience`
    - `ConnectionStrings__DefaultConnection`

## License

This project is for demonstration and internal use only.

