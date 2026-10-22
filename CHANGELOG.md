# Changelog
All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com), versioning: [SemVer](https://semver.org).

## [Unreleased]

## [0.4.0] - 2026-10-22
### Added
- **M8 — Units & dimensions (Spec §11):** `Dimension` (L, M, T, Θ, angle + absolute-temperature marker), `Unit`, built-in catalog for every quantity in §11 (length, area, volume, mass, force, torque, pressure/stress, strain, linear/angular velocity, acceleration, power, energy, temperature, temperature difference, angle, stiffness), `UnitRegistry` with English and Persian aliases, `Quantity` with stored input unit, explicit conversion, dimension-checked `+ − < >`, point/interval temperature rules (T−T=ΔT, T+ΔT=T, T+T forbidden), exact rpm ↔ rad/s, explicit `DropAngle()` for P=T·ω, display precision separated from computation, `QuantityParser` (FA/EN).
- **M9 — Parameter extraction & ambiguity (Spec §06–§08):** `ParameterVocabulary` (extensible FA/EN parameter names with dimension and default unit), `ParameterExtractor` (prefix/postfix binding, units, qualifiers at-least/at-most/about, standards such as ISO/DIN, traceable source text, confidence; issues for default units, unit mismatch, conflicting values, invalid values, unassigned numbers), `ClarificationPlanner` (required vs. assumable vs. optional, priority order, no re-asking, design effect, how-to-measure hints, safety-critical assumptions need approval), problem templates (shaft, spur gear, gear pair, slider-crank, bearing, compression spring, block), `RequirementSpecification` with `REQ-nnn` IDs and pluggable consistency rules (rod > crank, bore < root diameter, centre distance, stroke = 2r, P = T·ω).
### Changed
- Intent engine prefers named, unit-aware values over positional numbers for gears and slider-crank; blocking extraction issues stop execution.
### Tests
- 177 automated tests (was 88), all passing without CATIA.

## [0.3.0] - 2026-10-22
### Added
- **M3 — Interfaces & Composition Root:** `IOperationLogger`, `ICadConnectionService`, `ICadDocumentService`, `IBasicPartService`,
  `IDesignExecutionEngine`, `IEngineeringIntentAnalyzer` in `Core.Abstractions`; `CatiaServices` module factory;
  `AppComposition` is the only place that wires concrete types. `Form1` receives services by constructor and no longer touches COM.
- **M5 — Project model & workflow:** `EngineeringProject` (requirements, assumptions with approval, files, history, version) and a
  rule-based `ProjectWorkflow` state machine `Draft → Requirements → Analysis → Design → Validation → Execution → Review → Completed`
  with pluggable `ITransitionRule` guards (no skipping; no execution without specification, plan, confirmed assumptions and user approval).
- **M6 — Commands & allowlist:** `CadOperationDescriptor` (inputs, risk, undo, failure & verification metadata), `ICadOperationHandler`,
  `CadOperationRegistry` (case-sensitive allowlist) and `CadPlanExecutor` (preflight validation, unique command IDs, stop-on-failure,
  remaining steps marked Skipped). All 15 CATIA operations registered in `CatiaOperationHandlers`.
- **M7 — Errors & lifecycle:** `ErrorTranslator` (COM HRESULT / IO / network / resource classification → user + technical message),
  cooperative cancellation between steps, `ComObjectTracker` for deterministic RCW release, global unhandled-exception handling,
  CAD execution on an STA worker thread (UI stays responsive) and close-while-running protection.
- Contract tests: only the Composition Root may reference Infrastructure; no `dynamic`/COM in UI; no service construction outside the root.
### Changed
- `CatiaMechanicalEngine.ExecuteDesignSpecification` no longer contains an operation `switch`; it delegates to `CadPlanExecutor` (contract §4).
- `OperationLogger` accepts an explicit workspace root (testability).
### Tests
- 88 automated tests (was 49), all passing without CATIA.

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
