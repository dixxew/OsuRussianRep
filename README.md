# 🎮 OsuRussianRep

**OsuRussianRep** is an ASP.NET Core Web API for collecting and analyzing **osu! IRC/chat activity**, with a focus on **message, word, and user statistics** for the Russian-speaking community.

The project integrates with the **osu! API**, stores data in a database, and exposes a REST API for statistics and analytics.

---

## Features

- Message collection and aggregation  
- Word statistics (top words, frequency, per-user stats)  
- User activity analytics  
- Authentication via osu! OAuth  
- Database storage via Entity Framework Core  
- REST API with Swagger (OpenAPI)  
- Docker-ready setup

---

## Tech Stack

- **.NET / ASP.NET Core**
- **Entity Framework Core**
- **PostgreSQL / SQL (via EF Core)**
- **OsuSharp** (osu! API client)
- **Swagger / OpenAPI**
- **Docker & Docker Compose**

---

## Getting Started

### 1. Clone and configure

```bash
git clone https://github.com/dixxew/OsuRussianRep.git
cd OsuRussianRep
cp .env.example .env
```

Fill in the required environment variables in `.env` (osu! OAuth, database, etc.).

---

### 2. Run locally

```bash
dotnet restore
dotnet run --project OsuRussianRep
```

Default API URL:

```
http://localhost:5000
```

Swagger UI:

```
/swagger
```

---

### 3. Run with Docker

```bash
docker compose up --build
```

---

## API Overview

- **Auth**
  - osu! OAuth authorization
  - Token exchange and refresh

- **Users**
  - User list and activity stats
  - Pagination and filtering

- **Messages**
  - Message statistics
  - Date range filtering

- **Word Stats**
  - Global and per-user word frequency
  - Top words

- **Stats**
  - Aggregated analytics endpoints

- **Status**
  - Service health check

---

## Project Structure

```
OsuRussianRep/
├── Controllers/        # REST API controllers
├── Services/           # Business logic
├── Context/            # EF Core DbContext
├── Mapping/            # DTOs and mappings
├── Options/            # Configuration options
├── Interfaces/         # Service contracts
├── Helpers/            # Utility classes
├── external/
│   └── OsuSharp/       # osu! API client
```

---

## Configuration

All configuration is done via **environment variables**.

See `.env.example` for the full list of required settings, including:
- osu! OAuth credentials
- Database connection string
- Internal service options

---

## Database

If migrations are used:

```bash
dotnet ef database update
```

---

## Contributing

The project is **actively maintained**.  
Any contribution is welcome and will be reviewed.

Contribution flow:
1. Fork the repository
2. Create a feature or fix branch
3. Open a Pull Request

---

## Use Cases

- Analyzing osu! IRC activity
- Community statistics and dashboards
- Backend for bots or external services

---

## License

MIT
