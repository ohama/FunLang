# Roadmap: LangThree v9.0 Project Build System

## Overview

v9.0 delivers a Cargo-style project build system in two phases: first extending the CLI with `--check`, `--deps`, `--prelude` flags, environment variable support, and file import caching; then adding `funproj.toml` project file parsing with `build` and `test` subcommands for multi-target project management.

## Milestones

<details>
<summary>v1.0-v8.1 (Phases 1-66) -- SHIPPED 2026-03-30</summary>

142 plans across 66 phases. See milestone-archive.md for details.

</details>

### v9.0 Project Build System (In Progress)

**Milestone Goal:** funproj.toml-based Cargo-style build system for systematic multi-file project management

## Phases

- [x] **Phase 67: CLI Extensions** - --check, --deps, --prelude flags + env var + file import caching
- [x] **Phase 68: Project File** - funproj.toml parsing + build/test subcommands

## Phase Details

### Phase 67: CLI Extensions
**Goal**: Users can type-check, inspect dependencies, and configure Prelude paths without executing programs
**Depends on**: Phase 66 (v8.1 complete)
**Requirements**: CLI-01, CLI-02, CLI-03, CLI-04, CLI-05
**Success Criteria** (what must be TRUE):
  1. `langthree --check file.fun` type-checks the file and all its `open` imports without executing, reporting errors/success to stderr
  2. `langthree --deps file.fun` prints the recursive dependency tree showing all `open "file.fun"` imports with indentation
  3. `langthree --prelude /path/to/Prelude file.fun` uses the specified Prelude directory instead of auto-discovery
  4. When `--prelude` is not given, `LANGTHREE_PRELUDE` environment variable is used as Prelude path (falling back to existing auto-discovery if unset)
  5. Files imported via `open "file.fun"` are cached within a single process invocation -- importing the same file twice does not re-parse or re-typecheck it
**Plans:** 4 plans
Plans:
- [ ] 67-01-PLAN.md -- Argu flags + Prelude path resolution priority chain
- [ ] 67-02-PLAN.md -- --check and --deps mode implementation
- [ ] 67-03-PLAN.md -- File import caching
- [ ] 67-04-PLAN.md -- flt integration tests for CLI features

### Phase 68: Project File
**Goal**: Users can manage multi-target projects with a single `funproj.toml` configuration file
**Depends on**: Phase 67
**Requirements**: PROJ-01, PROJ-02, PROJ-03, PROJ-04, PROJ-05, PROJ-06, PROJ-07
**Success Criteria** (what must be TRUE):
  1. A `funproj.toml` file with `[project]`, `[[executable]]`, and `[[test]]` sections is parsed and validated by the interpreter
  2. `langthree build` in a directory with `funproj.toml` type-checks all `[[executable]]` targets; `langthree build <name>` type-checks only the named target
  3. `langthree test` in a directory with `funproj.toml` executes all `[[test]]` targets; `langthree test <name>` executes only the named target
  4. `[project].prelude` in `funproj.toml` sets the Prelude path (lower priority than `--prelude` flag and `LANGTHREE_PRELUDE` env var)
  5. flt integration tests cover CLI extensions (--check, --deps, --prelude) and project file features (build, test subcommands)
**Plans:** 3 plans
Plans:
- [ ] 68-01-PLAN.md -- Tomlyn NuGet + ProjectFile.fs TOML parsing module
- [ ] 68-02-PLAN.md -- Build/Test subcommands in Cli.fs + Prelude priority + Program.fs dispatch
- [ ] 68-03-PLAN.md -- flt integration tests for build, test, and project prelude

## Progress

**Execution Order:** Phase 67 -> Phase 68

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 67. CLI Extensions | 4/4 | ✓ Complete | 2026-03-31 |
| 68. Project File | 3/3 | ✓ Complete | 2026-03-31 |
