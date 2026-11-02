# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Phase 1 in progress: solution under `src/` (Domain, Application, Infrastructure, WinForms) + `tests/`; Krypton shell (`MainShellForm`) runs with sample data. See `docs/plans/PROJECT-PLAN.md` for status.

## Purpose

Windows Forms desktop app (**.NET Framework 4.5**, C#) that wraps the Vietnamese tax authority e-invoice portal <https://hoadondientu.gdt.gov.vn/>. The portal limits invoice lookup to one month per query; this tool lets users search/export by **month, quarter, year, or a custom date range**, by splitting the range into monthly requests internally.

Behaviour must mirror the portal ("Tra cứu" menu), plus the extensions below:

- **Search** invoices (purchase/sold) over the chosen period.
- **Export list** → spreadsheet in the portal's template layout.
- **Print** → PDF.
- **Export XML** → a ZIP containing the invoice XML files.
- **Period selection** (new): month / quarter / year / free-typed from–to dates.

## Authentication design (key architectural decision)

The portal login has a CAPTCHA, so the app does **not** automate login:

1. An embedded WebView (WinForms-hosted browser control) loads the portal's login page directly; the user logs in manually.
2. After login, the app captures the auth token (from the WebView's requests/storage/cookies) and reuses it for direct HTTP API calls to the portal. The WebView is only for login; data retrieval goes through the API client.
3. Handle token expiry by prompting the user to re-login in the WebView.

Planned layering (not yet created): UI (WinForms) → services (period splitting, search, export) → portal API client (token-bearing HttpClient) / WebView token capture. Keep period-splitting logic (quarter/year/custom → monthly windows) UI-independent so it is unit-testable.

## Testing policy: mock data only, real portal only in Release

- **Do not hit the real portal (`hoadondientu.gdt.gov.vn`) from tests, dev runs, spikes or demos.** All testing and development use mock data (`MockPortalHandler`, `MockInvoiceDataset`, development adapters). Real API calls happen only in the released product (Release build, real user, real login).
- **Mark test/mock mode visibly.** Whenever the app or a test runs on mock data it must say so: a visible `TEST`/`MOCK` label in the UI (title bar or banner), mock data clearly labelled as fake (e.g. "mẫu", "(mô phỏng)", XML comment "DỮ LIỆU GIẢ"), and automated tests tagged (`Category`, names/fixtures that make the mock explicit). Mock code stays Debug-only (`#if DEBUG`) and must never be reachable from Release.
- **Sole exception: trying out the first-time login feature** (user-driven WebView login on the real portal to verify login capture). Even then: the user enters credentials and CAPTCHA manually, nothing is automated, no invoice/search/download API is called, and no secrets are stored or logged.
- Never paste real traffic, tokens, passwords or cookies into the repo (see the API contract note below); commit only sanitized samples.

## Constraints

- Target framework is `net45`: no `async Main`, no modern C# runtime APIs; check package compatibility with .NET 4.5. TLS 1.2 must be enabled explicitly (`ServicePointManager.SecurityProtocol`) for the portal.
- .NET 4.5 WebView options: `WebBrowser` (IE, legacy), CefSharp.WinForms, or WebView2 (needs newer framework) — pick one deliberately and record it here once decided.
- Portal API contract (login JWT, RSQL search, detail, export-xml zip) is documented in `docs/api-samples/README.md`, taken from real traffic. Parts marked ⚠ there (purchase search, `state` paging) are still assumptions: verify against live traffic before relying on them. Never commit raw traffic captures (they contain passwords/tokens/cookies): name them `*.raw.*` (git-ignored) and commit only sanitized copies such as `docs/api-samples/sample-api-web.md`.
- Build requires Visual Studio / MSBuild with the .NET Framework 4.5 targeting pack (`msbuild` is not on PATH in this environment; only the `dotnet` CLI is, which cannot build net45 WinForms projects without reference assemblies).

## Commands

MSBuild/vstest are not on PATH; use the VS 2022 copies (Git Bash paths):

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
VT="/c/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TestWindow/vstest.console.exe"
"$MSB" src/HoaDonDienTu.sln -restore -nologo -v:m   # build
"$VT" tests/HoaDonDienTu.Domain.Tests/bin/Debug/net45/HoaDonDienTu.Domain.Tests.dll /TestCaseFilter:"Name~TryParse"   # tests (NUnit 3.13 + adapter 3.17)
# all: pass every tests/*/bin/Debug/net45/*.Tests.dll (Domain, Application, Infrastructure, WinForms) to vstest
```

UI: Krypton Toolkit/Ribbon **5.500.2008** (last line with a net45 build; 60+ needs net462). The WinForms project is an old-style csproj (designer-friendly); forms follow `X.cs` + `X.Designer.cs` partial pairs. In `HoaDonDienTu.WinForms`, `Application` resolves to the `HoaDonDienTu.Application` namespace, so write `System.Windows.Forms.Application`.

## Repository layout (harness scaffolding)

- `src/` — application source (solution + WinForms project go here)
- `tests/` — unit tests
- `docs/` — project context; see `docs/README.md` for the index (`requirements/` SRS + `FEATURE-LIST.md`, `detail-design/` one `DD-xx` per feature with an implement-progress table, `architecture/`, `decisions/` ADRs, `api-samples/`, `plans/PROJECT-PLAN.md` phase-level progress, `progress/` session handoff notes, `runbooks/`). Read `docs/plans/PROJECT-PLAN.md` then `docs/progress/` first when resuming work; record decisions in `docs/decisions/`. When a task changes state, update the DD's progress table and then the summary in `PROJECT-PLAN.md`.
- `.claude/agents/`, `.claude/commands/`, `.claude/skills/`, `.claude/hooks/` — Claude Code harness extensions (empty; add project subagents, slash commands, skills, hooks here)

## Working rules

- At the end of every work session/turn, propose the next task to do (from `docs/plans/PROJECT-PLAN.md`), with a one-line reason, so the user can answer "Làm tiếp".
