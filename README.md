# Hacker News Crawler

A Stack Builders coding challenge built with ASP.NET Core and Angular.

## Current status

The initial workspace is ready: an API health endpoint, an Angular landing screen, and one automated test for each application. Scraping, filters, and usage storage are not implemented yet. The frontend does not call the API at this stage.

## Challenge requirements

- Read the first 30 displayed entries from https://news.ycombinator.com/, preserving rank, title, points, and comment count.
- Filter A: titles with more than five words, ordered by comments.
- Filter B: titles with five words or fewer, ordered by points.
- Split words on whitespace, ignore standalone symbols, and count hyphenated expressions as one word. `This is - a self-explained example` contains five words.
- Persist the request timestamp and the applied filter identifier.

Planned ordering is descending, with ascending rank as the tie-breaker. Both filters will use the same collection of entries.

## Technologies and prerequisites

- .NET SDK 10.0.402, pinned in `global.json` with patch roll-forward.
- Node.js 22.14.0 (the version used for validation).
- Angular 21, TypeScript, and npm 11.6.2 for dependency installation.
- xUnit and ASP.NET Core's in-memory test host for the API; Vitest for Angular.

HtmlAgilityPack and SQLite are planned for later stages and are not dependencies yet.

## Install and build

From the repository root:

```powershell
dotnet restore HackerNewsCrawler.slnx
dotnet build HackerNewsCrawler.slnx --no-restore
```

In the frontend directory:

```powershell
cd src/hacker-news-web
npx --yes npm@11.6.2 ci --no-fund --no-audit
npm run build
```

The versioned npm command does not replace the globally installed npm. npm 10.9.2 and 10.9.9 failed during dependency resolution on the development machine; npm 11.6.2 completed successfully. `package-lock.json` records the resolved frontend dependencies.

On Windows, use `npm.cmd` and `npx.cmd` if PowerShell blocks their script wrappers.

## Run locally

From the repository root, start the API:

```powershell
dotnet run --project src/HackerNewsCrawler.Api --no-build --launch-profile http
```

`GET http://localhost:5218/health` returns HTTP 200 with `{"status":"ok"}`. This checks that the server is running; it does not check Hacker News or a database.

In another terminal, start Angular:

```powershell
cd src/hacker-news-web
npm start -- --host 127.0.0.1 --port 4200
```

Open http://127.0.0.1:4200/. Stop each server with Ctrl+C. These HTTP settings are for local development.

## Tests

From the repository root, after building:

```powershell
dotnet test HackerNewsCrawler.slnx --no-build
```

From `src/hacker-news-web`:

```powershell
npm test -- --watch=false
```

The API test verifies the real route, HTTP status, content type, and JSON response using an in-memory server. The Angular test renders the root component and checks its initial content. Neither test contacts Hacker News.

## Structure and decisions

- `src/HackerNewsCrawler.Api`: ASP.NET Core host and HTTP endpoints.
- `src/hacker-news-web`: standalone Angular application.
- `tests/HackerNewsCrawler.Tests`: backend tests.

The API provides an interface that other clients can consume; Angular provides a visual interface for reading results. This adds setup compared with a console application, but serves the intended browser experience. Minimal APIs keep the initial HTTP setup small. Business logic will be separated from endpoint handlers when it is introduced.

The UI currently uses plain CSS, with no component library, routing, or server-side rendering. The API and frontend run independently; their integration is a later step.

## Filters and usage storage

Filter execution is not available yet. No usage information is stored. The planned implementation uses SQLite with UTC timestamps, a filter identifier, and a result count. Storage will remain separate from filtering logic.

## Assumptions, limitations, and performance

The current stage validates the toolchain and application startup only. It does not validate HTML parsing, network error handling, word counting, sorting, or persistence.

The crawler will fetch the homepage once per operation and reuse the entries when both filters are requested. Network, parsing, filtering, and storage time will be considered separately. No crawler performance measurements are available yet.
