# Changelog

All notable changes to Akari Tool are documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/),
and the project aims to follow semantic versioning.

## [Unreleased]

### Notes
- Audited nav ↔ `PageMap` ↔ DI wiring (`MainWindow.xaml`, `MainWindow.xaml.cs`,
  `App.xaml.cs`). No missing wiring found — see the report below.

## [2.0.2]
- Current release (commit `c037fa1`).

## [2.0.1]
- Version bump (commit `4416e4d`).
- Removed `CLAUDE.md` and `MIGRATION_PROMPT.md`.

## [2.0.0]
- WinUI 3 framework rebuild (Phase A): Mica-backdrop shell, custom title bar,
  `NavigationView` rail, `Frame`-hosted pages, docked live log console, status bar
  with theme toggle and build stamp.
- Vector (Path-based) title-bar logo — theme-aware, resolution-independent.
- Global search across all tweak tabs via `TweakRegistry`.
- Startup drift scanner with in-shell "Settings reverted" banner.
