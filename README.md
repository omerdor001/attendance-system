# Attendance System

A production-ready attendance tracking system built with Clean Architecture.

## Tech Stack

- **Backend**: ASP.NET Core 9, EF Core, SQL Server
- **Frontend**: React 19 + TypeScript + Vite + Tailwind CSS
- **Time Source**: WorldTimeAPI (Europe/Zurich) — system clock is never used
- **Auth**: JWT (HS256, 8h expiry) + BCrypt password hashing
- **Resilience**: Polly retry (3x exponential backoff) + circuit breaker (5 failures → 60s open)
- **AI Analysis**: Ollama (llama3.2) — optional; falls back to rule-based anomaly detection
- **PDF Reports**: QuestPDF (Community)
- **Containers**: Docker Compose (SQL Server + Backend)

## Quick Start

### Prerequisites
- Docker Desktop
- Node.js 18+
- .NET SDK 9
- [Ollama](https://ollama.com) with `llama3.2` pulled *(optional — anomaly detection falls back to rule-based if unavailable)*

### 1. Start backend + database

```bash
docker-compose up --build
```

Backend available at: `http://localhost:5000`  
Swagger UI: `http://localhost:5000/swagger`

### 2. Start frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend at: `http://localhost:5173`

### 3. (Optional) Enable AI anomaly detection

```bash
ollama pull llama3.2
ollama serve
```

If Ollama is not running, the system silently falls back to rule-based anomaly detection — no configuration needed.

## Default Credentials

| Role     | Username   | Password    |
|----------|------------|-------------|
| Admin    | admin      | Admin123!   |
| Employee | john.doe   | Employee1!  |
| Employee | jane.smith | Employee1!  |
| Employee | bob.wilson | Employee1!  |

> Seed data is loaded automatically on first startup.

## Architecture

```
AttendanceSystem.Core          (Domain + Business Logic — no external deps)
  ├── Domain/                  User, AttendanceEvent entities
  ├── Interfaces/              IAttendanceService, IWorldTimeApiService, etc.
  ├── Services/                AttendanceService, AuthService, AdminService
  └── Exceptions/              BusinessException, ValidationException

AttendanceSystem.Infrastructure (Data + External Services)
  ├── Data/                    AppDbContext, EF Core Migrations
  ├── Repositories/            AttendanceRepository, UserRepository
  └── ExternalServices/        WorldTimeApiService (Polly), JwtTokenService,
                               OllamaAnalysisService (AI), PdfReportService (QuestPDF)

AttendanceSystem.API           (Presentation Layer)
  ├── Controllers/             AttendanceController, AuthController, AdminController
  └── Program.cs               DI, Polly, JWT, CORS, Rate limiting
```

## Key Features

- **External time only**: All timestamps sourced from WorldTimeAPI — never system clock
- **503 on time failure**: If WorldTimeAPI is down, clock-in/out returns 503 (no silent fallback)
- **Retrospective entries**: Employees can submit past clock-ins/outs with reasons
- **Admin approval workflow**: Retrospective entries require admin approval before counting toward hours
- **AI anomaly detection**: Ollama (llama3.2) analyses patterns per employee; rule-based fallback if Ollama is unavailable
- **Heartbeat**: Frontend polls every 2 min — returns alerts, clock status, pending count
- **Rate limiting**: 10 req/min per user (sliding window)

## API Endpoints


| Method | Path | Auth |
|--------|------|------|
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/register` | Admin |
| POST | `/api/attendance/clock-in` | User |
| POST | `/api/attendance/clock-out` | User |
| POST | `/api/attendance/clock-in-retrospective` | User |
| POST | `/api/attendance/clock-out-retrospective` | User |
| GET | `/api/attendance/heartbeat` | User |
| GET | `/api/attendance/my-history` | User |
| GET | `/api/admin/reports` | Admin |
| GET | `/api/admin/pending-approvals` | Admin |
| POST | `/api/admin/approve-retrospective/{id}` | Admin |
| POST | `/api/admin/reject-retrospective/{id}` | Admin |
| GET | `/api/admin/export-employee-pdf/{id}` | Admin |
| GET | `/api/admin/export-all-employees-pdf` | Admin |

## Running Tests

```bash
# All tests
dotnet test backend/AttendanceSystem.Tests

# Unit tests only
dotnet test backend/AttendanceSystem.Tests --filter "FullyQualifiedName~Unit"

# Integration tests only (requires running SQL Server)
dotnet test backend/AttendanceSystem.Tests --filter "FullyQualifiedName~Integration"
```

**Unit tests**: AttendanceService, AuthService, OllamaAnalysisService, PdfReportService, WorldTimeApiService  
**Integration tests**: DB attendance events, DB user operations

## Known Limitations

- Frontend runs locally (no nginx container)
- Ollama must run locally on `:11434`; there is no Docker service for it — pull `llama3.2` separately
- Email notifications not implemented (badge count only)
- No refresh token rotation (8h JWT expiry)
