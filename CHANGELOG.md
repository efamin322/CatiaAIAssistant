# Changelog
All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com), versioning: [SemVer](https://semver.org).

## [Unreleased]

## [0.3.0] - 2026-11-08
### Added
- **Core contracts (M3):** `IOperationLog`, `ICadConnection`, `ICadDocumentService`, `ICadPartService`, `ICadExecutionEngine` in `CatiaAIAssistant.Core.Abstractions`.
- **Composition Root:** `App/Services/AppServices.cs` (`CreateDefault()`) — the only place where concrete types are constructed and wired; `Form1` takes `AppServices` via constructor injection and falls back to the default root for the designer entry point.
- `ICadDocumentService.GetOpenDocumentCount()` / `CloseAllDocuments(int)` so closing documents no longer requires COM in the UI layer.
- Executable architecture tests fail the build if a portable layer references a `*-windows` project, WinForms, `Marshal`, `dynamic`, or the CATIA infrastructure.
### Changed
- `Form1` and `Phase1SelfTestRunner` depend only on Core abstractions (`IOperationLog`/`ICad*`); zero direct COM/`dynamic` usage in the UI project (audit A-04 closed).
- `CatiaConnectionInfo` moved to `Core.Cad` as a plain DTO; `CatiaConnectionService` exposes `ProgId` instead of leaking its constant.
- `SanitizeIdentifier` call sites now use `Core.Text.TextNormalizer` directly.
### Fixed
- Nullable/using fallout from the DI rewiring; solution still builds with 0 warnings.

## [0.2.0] - 2026-10-22
### Changed
- **Modular split (M2):** solution now has `CatiaAIAssistant.Core` (net8.0), `CatiaAIAssistant.Engineering` (net8.0), `CatiaAIAssistant.Infrastructure.Catia` (net8.0-windows) and the WinForms App.
- Domain models moved to Core; Engineering no longer depends on Infrastructure (audit A-02).
- Text helpers (`NormalizeDigits`, `SanitizeIdentifier`, number/dimension parsing) centralised in `Core.Text.TextNormalizer`; old methods kept as forwarders.
### Fixed
- Crank-slider: user rod length is no longer silently overwritten; L <= r now blocks execution, short L/r is replaced only as a flagged assumption.
- Conflicting-document close failures are logged instead of swallowed (A-08); all nullable warnings resolved (0 warnings).
### Added
- `tests/CatiaAIAssistant.Tests` (xUnit, 49 tests, no CATIA needed) incl. executable architecture-contract tests that fail on forbidden layer dependencies.

## [0.1.0] - 2026-10-22
### Added
- Imported original WinForms/.NET 8 source (Phase-1 foundation).
- `ARCHITECTURE_CONTRACT.md` — binding modularity & extensibility contract.
- `ROADMAP.md` derived from `docs/MASTER_PROMPT_V3.md`.
- Solution file, `Directory.Build.props` (shared version).
- CI (Windows build/test) and tag-driven Release workflow (installer + portable ZIP + source ZIP + SHA256).
- Inno Setup installer script.
- Initial audit report `docs/reports/AUDIT_v0.1.0.md`.
### Changed
- Cleaned csproj from Visual Studio upgrade artefacts and local paths.
