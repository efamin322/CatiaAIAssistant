# Changelog
All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com), versioning: [SemVer](https://semver.org).

## [Unreleased]

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
