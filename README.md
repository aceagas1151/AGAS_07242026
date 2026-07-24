# FileProcessing.Api

A small ASP.NET Core Web API that accepts CSV file uploads over HTTP, validates and parses them, computes aggregate statistics, and tracks processing history in memory. Endpoints other than the health check are protected by a custom API-key middleware.

## Features

- CSV upload endpoint that calculates row count, total, average, minimum, maximum, and per-department totals
- Custom API-key authentication middleware for all `/api` routes
- Thread-safe in-memory tracking of processing attempts (successful and failed), with a reporting endpoint
- Centralized exception handling that returns consistent Problem Details responses without leaking internal error details
- Structured logging via `ILogger<T>`
- NUnit unit and integration test suite
- Postman collection for manual testing
- Docker multi-stage build with a non-root runtime user and a container health check

## Technology Choices

- .NET 8 / ASP.NET Core Web API, controller-based (no Swagger/OpenAPI — this collection and this README are the API documentation)
- [CsvHelper](https://joshclose.github.io/CsvHelper/) for CSV parsing
- NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing for tests
- No database — processing history is tracked in memory by design (see [Trade-offs](#trade-offs))
- Docker + Docker Compose for containerized runs

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (only needed for the containerized run)
- Optional: [Postman](https://www.postman.com/downloads/) for manual testing via the included collection

## Repository Structure

```text
AGAS_07242026/
├── src/
│   └── FileProcessing.Api/
│       ├── Controllers/       # HealthController, FilesController
│       ├── Contracts/         # API response shapes (FileProcessingResult, DepartmentTotal, ProcessingReport)
│       ├── Exceptions/        # FileValidationException, CsvProcessingException
│       ├── Middleware/        # ApiKeyMiddleware, GlobalExceptionHandler
│       ├── Models/            # ProcessingRecord, ProcessingStatus (internal tracking model)
│       ├── Options/           # FileProcessingOptions
│       ├── Services/          # CsvFileProcessor, InMemoryFileProcessingTracker
│       ├── Program.cs
│       ├── appsettings.json
│       └── FileProcessing.Api.csproj
├── tests/
│   └── FileProcessing.Api.Tests/
│       ├── Integration/       # WebApplicationFactory-based endpoint and middleware tests
│       ├── Middleware/        # GlobalExceptionHandler unit tests
│       └── Services/          # CsvFileProcessor and tracker unit tests
├── samples/                   # valid-sales.csv, invalid-sales.csv, empty-sales.csv
├── postman/                   # Postman collection and local environment
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── .gitignore
└── FileProcessing.sln
```

## Design Overview

- **`FilesController`** handles HTTP concerns only: reading the upload, returning status codes, calling into services.
- **`CsvFileProcessor`** (`IFileProcessor`) takes a `Stream` and file name — not an `IFormFile` — so it has no dependency on ASP.NET Core's HTTP types and can be unit tested directly. It owns CSV parsing, header validation, row validation, and aggregate calculation.
- **`InMemoryFileProcessingTracker`** (`IFileProcessingTracker`) is registered as a singleton and guards its state with a single `lock`. It records every processing attempt after authentication succeeds and produces the report.
- **`ApiKeyMiddleware`** protects any request under `/api`, using a constant-time comparison so response timing doesn't leak how much of the key matched.
- **`GlobalExceptionHandler`** (`IExceptionHandler`) is the single place that turns exceptions into HTTP responses: `FileValidationException` and `CsvProcessingException` map to their own safe messages and status codes; anything else becomes a generic 500 with no internal details exposed.
- Two small custom exceptions exist deliberately — `FileValidationException` (carries its own status code: 400/413/415) and `CsvProcessingException` (always 400) — rather than a larger exception hierarchy.

## Configuration

Settings live under the `FileProcessing` section in `appsettings.json` and can be overridden with environment variables using the standard ASP.NET Core `__` (double underscore) delimiter:

```json
{
  "FileProcessing": {
    "ApiKey": "",
    "MaximumFileSizeBytes": 5242880,
    "MaximumTrackedRecords": 100
  }
}
```

| Setting | Environment variable override | Purpose |
|---|---|---|
| `FileProcessing:ApiKey` | `FileProcessing__ApiKey` | Required. The API rejects startup if this is blank. |
| `FileProcessing:MaximumFileSizeBytes` | `FileProcessing__MaximumFileSizeBytes` | Upload size limit (default 5,242,880 bytes / 5 MB). |
| `FileProcessing:MaximumTrackedRecords` | `FileProcessing__MaximumTrackedRecords` | How many recent processing records the report endpoint keeps (default 100). Total/success/failure counters are unaffected by this limit — only the `recentRecords` list is trimmed. |

## API Key Setup

The API refuses to start if `FileProcessing:ApiKey` is blank — this is checked once at startup (`ValidateOnStart`), not on every request, so a misconfigured deployment fails immediately and loudly instead of silently accepting an empty key.

- **`appsettings.json`** (base, all environments) ships with `ApiKey` left blank on purpose. No real key is committed anywhere in this repository.
- **`appsettings.Development.json`** sets it to the placeholder `local-development-key`, purely so `dotnet run` works out of the box for local development. This is not a secret — it's committed and documented here as a convenience value.
- **Outside Development** (Production, Docker, tests), the key must be supplied via the `FileProcessing__ApiKey` environment variable.
- **Automated tests** never depend on a developer's local environment variable — `ApiWebApplicationFactory` in the test project injects its own fixed test key (`test-api-key-12345`) directly into configuration.

Protected requests must send the key as a header:

```text
X-API-Key: <your key>
```

## Local Build

```bash
dotnet restore
dotnet build
```

## Local Run

```bash
dotnet run --project src/FileProcessing.Api
```

By default this uses the `Development` environment (see `src/FileProcessing.Api/Properties/launchSettings.json`), so the placeholder API key is already configured and no environment variable is required. The app listens on `http://localhost:5009` and opens `/health` in your browser.

To run with an explicit key instead (for example, to simulate a non-Development environment):

Bash:
```bash
export FileProcessing__ApiKey=local-development-key
dotnet run --project src/FileProcessing.Api
```

PowerShell:
```powershell
$env:FileProcessing__ApiKey = "local-development-key"
dotnet run --project src/FileProcessing.Api
```

## Test

```bash
dotnet test
```

This runs the full NUnit suite — unit tests for CSV processing, validation, and tracking, plus integration tests (via `WebApplicationFactory`) for authentication, the upload endpoint, the report endpoint, and centralized error handling.

## Docker

```bash
docker compose up --build
```

`docker-compose.yml` requires `FILE_PROCESSING_API_KEY` to be set in your shell before running — Compose will refuse to start the container (with a clear error) if it isn't set, rather than silently starting with a blank key. This applies to `docker compose down` too, since Compose needs to parse the same file.

Bash:
```bash
export FILE_PROCESSING_API_KEY=local-development-key
docker compose up --build
```

PowerShell:
```powershell
$env:FILE_PROCESSING_API_KEY = "local-development-key"
docker compose up --build
```

The container listens on port 8080 (`http://localhost:8080`). Two more variables are supported the same way, with sensible defaults if omitted: `FILE_PROCESSING_MAX_FILE_SIZE_BYTES` and `FILE_PROCESSING_MAX_TRACKED_RECORDS`.

The image is a two-stage build (`dotnet/sdk:8.0` to restore and publish, `dotnet/aspnet:8.0` to run), runs as the non-root `app` user built into the .NET 8 runtime image, and defines a `HEALTHCHECK` that curls `/health` — `curl` is installed explicitly in the runtime stage since the base image doesn't include it.

## Postman

Import both files from `postman/`:

- `FileProcessing.Api.postman_collection.json` — 7 requests covering the health check, a valid upload, missing/invalid API key, an invalid CSV, an unsupported file type, and the report endpoint
- `Local.postman_environment.json` — defines `baseUrl` (`http://localhost:8080`, matching the Docker port) and `apiKey` (the same placeholder used elsewhere, not a real secret)

File upload requests can't carry a portable local file path across machines, so after importing, open each upload request's **Body** tab and manually select the corresponding file from `samples/` (the request description names which one). Every request includes a small test script checking the status code and key response fields.

## Endpoints

| Method | Path | Auth required | Description |
|---|---|---|---|
| GET | `/health` | No | Liveness check |
| POST | `/api/files/process` | Yes | Upload and process a CSV file |
| GET | `/api/files/report` | Yes | Processing statistics and recent records |

### Request Examples

Health check:
```bash
curl http://localhost:5009/health
```

Valid upload:
```bash
curl -H "X-API-Key: local-development-key" \
  -F "file=@samples/valid-sales.csv;type=text/csv" \
  http://localhost:5009/api/files/process
```

Upload without an API key:
```bash
curl -F "file=@samples/valid-sales.csv;type=text/csv" \
  http://localhost:5009/api/files/process
```

Report:
```bash
curl -H "X-API-Key: local-development-key" \
  http://localhost:5009/api/files/report
```

(Swap the port for `8080` when running via Docker.)

### Response Examples

`GET /health`:
```json
{ "status": "Healthy" }
```

`POST /api/files/process` (using `samples/valid-sales.csv`):
```json
{
  "fileName": "valid-sales.csv",
  "processedAtUtc": "2026-07-24T10:22:51.2164872Z",
  "rowCount": 3,
  "totalAmount": 3650.50,
  "averageAmount": 1216.83,
  "minimumAmount": 900.00,
  "maximumAmount": 1500.00,
  "departmentTotals": [
    { "department": "Engineering", "totalAmount": 2750.50 },
    { "department": "Sales", "totalAmount": 900.00 }
  ],
  "processingTimeMilliseconds": 10
}
```

`GET /api/files/report`:
```json
{
  "totalAttempts": 1,
  "successfulAttempts": 1,
  "failedAttempts": 0,
  "averageDurationMilliseconds": 19,
  "recentRecords": [
    {
      "fileName": "valid-sales.csv",
      "fileSizeBytes": 100,
      "startedAtUtc": "2026-07-24T10:22:51.199536Z",
      "completedAtUtc": "2026-07-24T10:22:51.2190568Z",
      "durationMilliseconds": 19,
      "status": "Success",
      "rowCount": 3,
      "errorMessage": null
    }
  ]
}
```

Missing API key (`401`):
```json
{ "title": "Unauthorized", "status": 401, "detail": "API key is missing.", "instance": "/api/files/process" }
```

## CSV Format

```csv
Employee,Department,Amount
Alice,Engineering,1250.50
Bob,Sales,900.00
Carol,Engineering,1500.00
```

- Required columns: `Employee`, `Department`, `Amount`
- Header matching is case-insensitive and tolerates surrounding whitespace (` employee `, `DEPARTMENT`, etc. all match)
- `Amount` is parsed as `decimal`, never `double`/`float`, to avoid floating-point rounding error on monetary values
- The displayed `averageAmount` is rounded to 2 decimal places using away-from-zero midpoint rounding (`MidpointRounding.AwayFromZero`); `totalAmount`, `minimumAmount`, `maximumAmount`, and department totals are plain decimal sums/comparisons, not independently rounded
- Department totals in the response are sorted alphabetically for deterministic output

## Validation Behavior

Processing stops at the first problem found and returns a single, safe error message — it does not attempt to collect or report every error in a file at once.

| Condition | Status |
|---|---|
| No file provided / empty file | 400 |
| File exceeds `MaximumFileSizeBytes` | 413 |
| Unsupported file extension (only `.csv`) | 415 |
| Unsupported content type | 415 |
| Missing a required header (`Employee`/`Department`/`Amount`) | 400 |
| Empty CSV or header-only CSV (no data rows) | 400 |
| Row with missing Employee or Department value | 400 |
| Row with a non-numeric or negative Amount | 400 |

## File Tracking Behavior

- Tracking is stored **in memory only**, in a singleton service. It is not persisted anywhere.
- Tracking **resets whenever the process or container restarts**.
- Uploaded files are **never written to disk** — the upload stream is read directly by the CSV processor and discarded afterward.
- **Authentication failures are not counted** as processing attempts, because the middleware rejects the request before it reaches the controller — processing never began.
- **Validation failures that occur after successful authentication** (bad file type, oversized file, malformed CSV, invalid row data) **are counted** as failed processing attempts, along with any genuinely unexpected error.
- The report's `recentRecords` list is capped at `MaximumTrackedRecords` (oldest dropped first), but the `totalAttempts`/`successfulAttempts`/`failedAttempts` counters reflect every attempt made, not just the retained records.

## Error Response Format

All error responses use `application/problem+json` with a consistent shape:

```json
{ "title": "...", "status": 000, "detail": "...", "instance": "/path" }
```

- `detail` always contains a safe, human-readable message — never a raw exception message, stack trace, internal type name, file path, or configuration value
- Expected client errors (`FileValidationException`, `CsvProcessingException`) surface their own specific message and status code (400/413/415)
- Any other, unexpected exception becomes a generic 500 with a fixed generic message; the real exception is only ever logged server-side, never returned to the caller

## Logging Behavior

Structured logging via `ILogger<T>`, for example:

```csharp
_logger.LogInformation(
    "Processed file {FileName} with {RowCount} rows in {ElapsedMilliseconds} ms",
    fileName, rowCount, elapsedMilliseconds);
```

- Processing start/completion, file name, file size, row count, and elapsed time are logged at `Information`
- Expected failures (validation, CSV errors, rejected API keys) are logged at `Warning` with just the safe message — no stack trace
- Unexpected exceptions are logged at `Error` with the full exception, but only in the server's own logs
- Never logged: the supplied API key, the configured API key, full file contents, entire uploaded records, or raw exception details
- ASP.NET Core's built-in exception-handling middleware normally logs every exception a second time at `Error` with a full stack trace, even ones already handled cleanly — `appsettings.json` sets `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` to `Critical` specifically to suppress that duplicate, noisy log line for expected errors, without affecting the app's own logging

## Security Considerations

- API key comparison uses `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes (after a length check) rather than a plain string comparison, to reduce timing-based inference of the key
- The API key is never logged and never appears in an exception message or API response
- No API key is committed to the repository; the Docker image never bakes one in either
- The container runs as a non-root user
- **HTTPS is intentionally not handled by this application** — it assumes a hosting platform, load balancer, or reverse proxy terminates TLS in front of it, which is standard for containerized services
- API-key authentication here is deliberately simple (a single static key via one middleware) because that is the scope of this exercise, not a production authentication system — see below

## Assumptions

- A single, shared API key is sufficient; there is no concept of multiple clients, key rotation, or per-client attribution
- CSV files are UTF-8 encoded
- `Amount` values use a plain decimal format compatible with `InvariantCulture` (e.g. `1250.50`, not `1.250,50`)
- The service runs as a single instance — nothing in the tracker assumes or supports multiple concurrent instances

## Trade-offs

- **In-memory tracking instead of a database** — much simpler to implement and reason about within this scope, at the cost of losing history on restart and being unable to scale beyond one instance
- **Fail-fast startup validation for the API key** — a misconfigured deployment refuses to start at all rather than accepting requests with a broken auth gate; this trades availability for correctness, which is the safer default for an authentication setting
- **One shared API key rather than per-client keys** — far simpler, but means there's no way to attribute requests to a specific caller or revoke access for just one consumer
- **Processing stops at the first invalid row** rather than collecting every error in a file — simpler and safer to reason about, but means a caller with many bad rows has to fix and resubmit repeatedly instead of seeing the full list at once

## Limitations

- Tracking is stored in memory and resets when the process or container restarts
- Uploaded files are not permanently stored — they cannot be re-fetched or re-processed after the fact
- A single static API key provides no per-client identity, rotation, or revocation
- There is no rate limiting or throttling on the upload endpoint
- There is no malware or content scanning of uploaded files beyond extension/content-type/size checks and CSV structure validation

## Potential Production Improvements

- A persistent database would be appropriate if processing history needed to survive restarts
- A distributed tracker (for example, backed by Redis) would be required if multiple application instances needed to share processing statistics
- File scanning and stronger content inspection would be needed for a production file-upload service
- Secrets (the API key, in particular) should be managed through a secret manager or the deployment platform's secret configuration, rather than a plain environment variable
- Real dependency health checks (via `Microsoft.Extensions.Diagnostics.HealthChecks`) would be worth adding once the service has actual external dependencies to verify
- Rate limiting and per-client API keys would be a reasonable next step for a public-facing deployment
