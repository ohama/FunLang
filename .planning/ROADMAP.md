# Roadmap: LangThree

## Milestones

- [x] **v1.0 Core Language** - Phases 1-7 (shipped 2026-03-10)
- [x] **v1.2 Practical Features** - Phases 8-12 (shipped 2026-03-18)
- [x] **v1.3 Tutorial** - Phases 13-14 (shipped 2026-03-19)
- [x] **v1.4 Language Completion** - Phases 15-18 (shipped 2026-03-20)
- [x] **v1.5 User-Defined Operators** - Phases 19-22 (shipped 2026-03-20)
- [x] **v1.7 Offside Rule & List Syntax** - Phases 23-24 (shipped 2026-03-22)
- [x] **v1.8 Polymorphic GADT** - Phase 25 (shipped 2026-03-23)
- [ ] **v2.0 Practical Language Completion** - Phases 26-32 (in progress)

## Phases

<details>
<summary>v1.0-v1.8 (Phases 1-25) - SHIPPED 2026-03-23</summary>

25 phases, 68 plans across 7 milestones. See MILESTONES.md for details.

</details>

### v2.0 Practical Language Completion (In Progress)

**Milestone Goal:** FunLexYacc 프로젝트에서 발견된 34개 제약사항 전면 해결. cat/sed 해킹, 접두사 규칙, 26개 등호 체인 등 workaround 제거.

- [x] **Phase 26: Quick Fixes & Small Additions** - Crash fix, Prelude path, failwith, option alias (shipped 2026-03-24)
- [ ] **Phase 27: List Syntax Completion** - Multi-line lists, trailing semicolons, list patterns
- [ ] **Phase 28: N-Tuples** - 3+ element tuples with let-destructuring
- [ ] **Phase 29: Char Type & Comparisons** - Char literals, conversion functions, ordering operators
- [ ] **Phase 30: Parser Improvements** - Local let rec, unit args, else+keyword, deep nesting, top-level let-in
- [ ] **Phase 31: Module System** - Import mechanism, multiple modules, module scoping
- [ ] **Phase 32: File I/O & System Builtins** - read/write files, stdin, args, env, stderr, path utils

## Phase Details

### Phase 26: Quick Fixes & Small Additions
**Goal**: Eliminate trivial workarounds and unblock downstream work
**Depends on**: Nothing (independent fixes)
**Requirements**: MOD-03, MOD-04, STD-01, TYPE-03
**Success Criteria** (what must be TRUE):
  1. Running an empty .fun file produces no crash and returns unit
  2. Running `dotnet run -- somefile.fun` from any directory loads Prelude functions (Option, list utilities, fst, snd, etc.)
  3. `failwith "error message"` raises an exception with the given message string
  4. `option` is accepted as a type name interchangeably with `Option` in type annotations (e.g., `fun (x : int option) -> x`)
**Plans**: 2 plans

Plans:
- [x] 26-01-PLAN.md — failwith builtin (STD-01) + option/result type alias (TYPE-03)
- [x] 26-02-PLAN.md — Prelude path resolution (MOD-04) + empty file guard (MOD-03)

### Phase 27: List Syntax Completion
**Goal**: Lists work naturally with multi-line formatting and pattern matching
**Depends on**: Nothing (independent syntax work)
**Requirements**: SYN-02, SYN-03, SYN-04
**Success Criteria** (what must be TRUE):
  1. A list literal spanning multiple lines parses correctly (e.g., `[1;\n  2;\n  3]`)
  2. A trailing semicolon in a list literal is accepted without error (`[1; 2; 3;]`)
  3. Pattern matching on list literal patterns works: `match xs with | [x] -> ...`, `| [x; y] -> ...`, `| [x; y; z] -> ...`
**Plans**: TBD

Plans:
- [ ] 27-01: TBD

### Phase 28: N-Tuples
**Goal**: Users can work with tuples of any size, not just pairs
**Depends on**: Nothing (independent type system work)
**Requirements**: TYPE-01, TYPE-02
**Success Criteria** (what must be TRUE):
  1. Creating 3-tuples and larger works: `let t = (1, "hello", true)` type-checks and evaluates
  2. Let-destructuring works for N-tuples: `let (a, b, c) = t` binds all elements
  3. N-tuples work in function parameters: `fun (a, b, c) -> a + b + c`
  4. Existing 2-tuple functionality (fst, snd, tuple patterns) remains intact
**Plans**: TBD

Plans:
- [ ] 28-01: TBD

### Phase 29: Char Type & Comparisons
**Goal**: Character processing works without 26-equality-chain workarounds
**Depends on**: Nothing (independent type system work)
**Requirements**: TYPE-04, TYPE-05, TYPE-06
**Success Criteria** (what must be TRUE):
  1. Char literals parse and type-check: `let c = 'A'` has type `char`
  2. `char_to_int 'A'` returns 65, `int_to_char 65` returns `'A'`
  3. String/char comparison operators work: `"abc" < "def"` returns true, `'A' < 'Z'` returns true
  4. Comparison operators work with `<=`, `>=`, `>` for both string and char types
**Plans**: TBD

Plans:
- [ ] 29-01: TBD

### Phase 30: Parser Improvements
**Goal**: Common syntax patterns that currently fail now work naturally
**Depends on**: Nothing (independent parser/indent work)
**Requirements**: SYN-01, SYN-05, SYN-06, SYN-07, SYN-08
**Success Criteria** (what must be TRUE):
  1. `let rec helper x = ... in helper 0` works inside function bodies (local recursive functions)
  2. `f ()` passes unit as an argument; `fun () -> expr` accepts unit parameter
  3. `else` followed by expression keywords (`match`, `if`, `let`, `try`, `fun`) parses correctly — IndentFilter suppresses INDENT after ELSE
  4. Deeply nested function bodies (4+ levels of let/match/if) parse without errors
  5. Top-level `let x = ... in expr` works after module-level bindings in concatenated files
**Plans**: TBD

Plans:
- [ ] 30-01: TBD

### Phase 31: Module System
**Goal**: Multiple .fun files compose without cat/sed hacks
**Depends on**: Phase 26 (Prelude path fix)
**Requirements**: MOD-01, MOD-02, MOD-05
**Success Criteria** (what must be TRUE):
  1. `open "path/to/module.fun"` (or equivalent) loads and makes that module's bindings available
  2. A file with multiple `module X` declarations parses and evaluates correctly (each module scoped)
  3. Two modules defining the same type name (e.g., `Token`) can coexist with qualified access (`Parser.Token` vs `Lexer.Token`)
  4. Record field name collisions across modules are resolved via module scoping (no more prefix hacks)
**Plans**: TBD

Plans:
- [ ] 31-01: TBD

### Phase 32: File I/O & System Builtins
**Goal**: Programs read/write files, access stdin/args/env, and output to stderr — no shell hacks needed
**Depends on**: Nothing (independent builtin work)
**Requirements**: STD-02, STD-03, STD-04, STD-05, STD-06, STD-07, STD-08, STD-09, STD-10, STD-11, STD-12, STD-13, STD-14, STD-15
**Success Criteria** (what must be TRUE):
  1. `read_file "path.txt"` returns file contents as string; nonexistent file raises exception
  2. `stdin_read_all ()` reads all stdin; `stdin_read_line ()` reads one line
  3. `write_file "path" "content"` creates/overwrites file; `append_file "path" "content"` appends
  4. `file_exists "path"` returns bool
  5. `read_lines "path"` returns `string list`; `write_lines "path" lines` writes list
  6. `get_args ()` returns command-line arguments as `string list`
  7. `get_env "VAR"` returns environment variable value; `get_cwd ()` returns current directory
  8. `path_combine "dir" "file"` returns combined path; `dir_files "path"` returns file list
  9. `eprint` / `eprintln` output to stderr (not stdout)
**Plans**: TBD

Plans:
- [ ] 32-01: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 26 -> 27 -> 28 -> 29 -> 30 -> 31 -> 32

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 26. Quick Fixes | v2.0 | 2/2 | ✓ Complete | 2026-03-24 |
| 27. List Syntax | v2.0 | 0/TBD | Not started | - |
| 28. N-Tuples | v2.0 | 0/TBD | Not started | - |
| 29. Char Type | v2.0 | 0/TBD | Not started | - |
| 30. Parser Improvements | v2.0 | 0/TBD | Not started | - |
| 31. Module System | v2.0 | 0/TBD | Not started | - |
| 32. File I/O | v2.0 | 0/TBD | Not started | - |
