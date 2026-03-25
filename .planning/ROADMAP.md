# Roadmap: LangThree

## Milestones

- ✅ **v1.0 Core Language** - Phases 1-7 (shipped 2026-03-10)
- ✅ **v1.1 Testing & CLI** - Phases 8-12 (shipped 2026-03-18)
- ✅ **v1.2 Practical Features** - Phases 8-12 (shipped 2026-03-18)
- ✅ **v1.3 Tutorial Docs** - Phases 13-14 (shipped 2026-03-19)
- ✅ **v1.4 Language Completion** - Phases 15-18 (shipped 2026-03-20)
- ✅ **v1.5 User-Defined Operators** - Phases 19-22 (shipped 2026-03-20)
- ✅ **v1.6/v1.7 Offside Rule & List Syntax** - Phases 23-24 (shipped 2026-03-22)
- ✅ **v1.8 Polymorphic GADT** - Phase 25 (shipped 2026-03-23)
- ✅ **v2.0 Practical Language Completion** - Phases 26-32 (shipped 2026-03-25)
- 🚧 **v2.1 Bug Fixes & Test Hardening** - Phases 33-35 (in progress)

## Phases

<details>
<summary>✅ v1.0–v2.0 (Phases 1–32) - SHIPPED 2026-03-25</summary>

Phases 1-32 completed across v1.0–v2.0 milestones. See MILESTONES.md for full history.

- [x] Phase 1-7: Core Language (v1.0)
- [x] Phase 8-12: Practical Features (v1.1/v1.2)
- [x] Phase 13-14: Tutorial Documentation (v1.3)
- [x] Phase 15-18: Language Completion — TCO, or-patterns, type aliases, list ranges, mutual recursion (v1.4)
- [x] Phase 19-22: User-Defined Operators & Utilities (v1.5)
- [x] Phase 23-24: Offside Rule & List Syntax (v1.6/v1.7)
- [x] Phase 25: Polymorphic GADT (v1.8)
- [x] Phase 26-32: Practical Language Completion — file import, char, N-tuples, file I/O, system builtins (v2.0)

</details>

### 🚧 v2.1 Bug Fixes & Test Hardening (In Progress)

**Milestone Goal:** Fix runtime regressions from v2.0 and fill test coverage gaps for Phases 29-32.

- [x] **Phase 33: TCO Fix + Test Isolation** — Fix TCO regression and eliminate test state interference
- [x] **Phase 34: Test Coverage** — Add missing flt tests for Phases 29-32 features
- [ ] **Phase 35: Compile Warning Cleanup** — Eliminate all FS0025 and compile warnings

---

#### Phase 33: TCO Fix + Test Isolation

**Goal:** The interpreter correctly tail-call-optimizes recursive functions and the test suite runs deterministically
**Depends on:** Phase 32 (v2.0 complete)
**Requirements:** FIX-01, FIX-02, FIX-03, ISO-01, ISO-02
**Success Criteria** (what must be TRUE):
  1. `let rec loop n = if n = 0 then 0 else loop (n-1)` with 1,000,000 iterations completes without stack overflow
  2. `let rec ... and ...` mutually recursive functions tail-call-optimize correctly (LetRecDecl path)
  3. Local `let rec` inside expressions tail-call-optimizes correctly (LetRec path)
  4. All 214 F# unit tests pass on every run, including under parallel execution
  5. `typeCheckModule` calls do not interfere with each other through shared global state
**Plans:** 2 plans

Plans:
- [ ] 33-01-PLAN.md — Fix TCO in LetRec/LetRecDecl BuiltinValue wrappers (tailPos false -> true)
- [ ] 33-02-PLAN.md — Fix MatchCompile global counter: make freshTestVar local to compileMatch

---

#### Phase 34: Test Coverage

**Goal:** Every feature shipped in Phases 29-32 has flt regression tests that execute and pass
**Depends on:** Phase 33
**Requirements:** TST-01, TST-02, TST-03, TST-04, TST-05, TST-06, TST-07, TST-08, TST-09, TST-10, TST-11, TST-12, TST-13
**Success Criteria** (what must be TRUE):
  1. flt tests for char literals, char_to_int/int_to_char, and char comparison operators exist and pass
  2. flt tests for multi-param let rec, unit param shorthand, and top-level let-in expressions exist and pass
  3. flt tests for `open "lib.fun"` file import and qualified access of imported modules exist and pass
  4. flt tests for all 14 file I/O and system builtins (read_file, write_file, file_exists, append_file, read_lines, write_lines, get_args, get_env, get_cwd, eprint, path_combine, dir_files) exist and pass
  5. `dotnet test` shows the total flt test count increased from 447 with all new tests passing
**Plans:** 2 plans

Plans:
- [ ] 34-01-PLAN.md — Add flt tests for char (Phase 29) and parser improvements (Phase 30)
- [ ] 34-02-PLAN.md — Add flt tests for file import (Phase 31) and file I/O + system builtins (Phase 32)

---

#### Phase 35: Compile Warning Cleanup

**Goal:** `dotnet build` and `dotnet test` produce zero warnings
**Depends on:** Phase 34
**Requirements:** WARN-01, WARN-02, WARN-03
**Success Criteria** (what must be TRUE):
  1. IntegrationTests.fs has no FS0025 incomplete pattern match warnings
  2. RecordTests.fs has no FS0025 incomplete pattern match warnings
  3. `dotnet test` output shows 0 warnings from the F# compiler
**Plans:** 1 plan

Plans:
- [ ] 35-01-PLAN.md — Fix all FS0025 incomplete pattern match warnings in IntegrationTests.fs and RecordTests.fs

---

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1-7. Core Language | v1.0 | 32/32 | Complete | 2026-03-10 |
| 8-12. Practical Features | v1.2 | 12/12 | Complete | 2026-03-18 |
| 13-14. Tutorial Docs | v1.3 | 4/4 | Complete | 2026-03-19 |
| 15-18. Language Completion | v1.4 | 6/6 | Complete | 2026-03-20 |
| 19-22. Operators & Utilities | v1.5 | 4/4 | Complete | 2026-03-20 |
| 23-24. Offside Rule & List Syntax | v1.7 | 4/4 | Complete | 2026-03-22 |
| 25. Polymorphic GADT | v1.8 | 5/5 | Complete | 2026-03-23 |
| 26-32. Practical Language Completion | v2.0 | 14/14 | Complete | 2026-03-25 |
| 33. TCO Fix + Test Isolation | v2.1 | 2/2 | Complete | 2026-03-25 |
| 34. Test Coverage | v2.1 | 2/2 | Complete | 2026-03-25 |
| 35. Compile Warning Cleanup | v2.1 | 0/1 | Not started | - |
