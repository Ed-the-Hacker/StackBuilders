# Hacker News Explorer

An ASP.NET Core API and Angular interface for exploring the first 30 stories on the Hacker News homepage.

## Technologies and prerequisites

- .NET 10 SDK (10.0.402, pinned in `global.json`).
- Node.js 22.14.0 and npm. Installation uses npm 11.6.2 through `npx`.
- Angular 21, HtmlAgilityPack, and SQLite through Microsoft.Data.Sqlite.
- xUnit for the backend and Vitest for Angular.

No database server or credentials are required.

## Run locally

From the repository root:

```powershell
dotnet restore HackerNewsCrawler.slnx
dotnet build HackerNewsCrawler.slnx --no-restore
dotnet run --project src/HackerNewsCrawler.Api --no-build --launch-profile http
```

The API runs at http://localhost:5218. In a second terminal:

```powershell
cd src/hacker-news-web
npx --yes npm@11.6.2 ci --no-fund --no-audit
npm start -- --host 127.0.0.1 --port 4200
```

Open http://127.0.0.1:4200/. Stop each server with Ctrl+C.

The Angular development proxy forwards `/api` requests to the backend. These instructions run both applications locally; deployment requires hosting the frontend and routing `/api` to ASP.NET Core.

On Windows, use `npm.cmd` and `npx.cmd` if PowerShell blocks their script wrappers. The versioned installation command avoids a dependency-resolution error observed with npm 10 and does not replace the global npm installation.

## Use the application

- **All stories:** original homepage order, with rank, title, points, and comments.
- **Filter A:** titles with more than five words, most comments first.
- **Filter B:** titles with five words or fewer, most points first.
- **Refresh:** fetch a new homepage snapshot.

Story titles open their HTTP/HTTPS destination in a new tab; relative links are resolved against Hacker News.

Both filters break ties by ascending rank. The browser requests all entries and both filtered results together. Switching views reuses those results without another download or usage record. Initial loading and each refresh apply and record both filters.

The API also supports individual selections:

- `GET /api/entries` or `?filter=all`: original entries, no filter usage.
- `GET /api/entries?filter=a`: original entries and Filter A.
- `GET /api/entries?filter=b`: original entries and Filter B.
- `GET /api/entries?filter=both`: original entries and both filters.

Responses contain `entries`, `filterA`, and `filterB`; unrequested filters are null. Every request downloads the homepage once. `GET /health` checks application startup only.

## Tests and frontend build

From the repository root:

```powershell
dotnet test HackerNewsCrawler.slnx --no-build
```

From `src/hacker-news-web`:

```powershell
npm test -- --watch=false
npm run build
```

Backend tests cover word counting, filtering, HTML parsing, HTTP failures, cancellation, API responses, and persistent usage. They use local HTML fixtures, simulated outbound HTTP, and temporary SQLite files; they do not depend on the live website. Tests have `Unit` or `Integration` categories. Frontend tests cover loading, view selection, empty results, errors, retry, and cancellation.

## Architecture and decisions

The backend owns downloading, parsing, filtering, and storage. Angular only presents API results. Small classes separate these responsibilities without additional application layers or an ORM.

A typed HttpClient has a 15-second timeout and no automatic retries. The parser reads the first 30 displayed entries, or fewer when available, preserves their ranks, decodes HTML entities, and normalizes whitespace. Both filters operate on the original collection without modifying it.

SQLite creates `src/HackerNewsCrawler.Api/Data/usage.db` on the first filtered request. The `FilterUsage` table stores an ID, UTC request timestamp, filter identifier, and result count, including zero results. Both filter records commit in one transaction. Failed requests are not recorded as successful usage. Database files are excluded from Git.

## Assumptions and error handling

Words are whitespace-separated tokens containing a letter or digit. Standalone symbols are ignored; hyphenated expressions remain one word. `This is - a self-explained example` counts as five. This is simple character-based counting, not linguistic tokenization; supplementary Unicode letters are outside the counter's supported cases.

Missing points or comment links become zero; `discuss` also means zero comments. Missing required fields, malformed numbers, or an unrecognized HTML structure fail explicitly rather than returning partial or invented data. Website changes may require selector updates.

The API returns Problem Details: 400 for an invalid filter, 502 for upstream or parsing failures, 504 for timeout, and 503 for storage failure. Client cancellation stops the pending download. The UI reports errors and allows retry.

Filtering scans titles and sorts the matches. With at most 30 entries, straightforward LINQ and short SQLite transactions are sufficient. The parser builds a DOM for the whole downloaded page; the extraction limit does not limit download size.
