---
phase: 31-module-system
plan: 01
subsystem: language-core
tags: [module-system, file-import, type-checker, evaluator, path-resolution, cycle-detection]

# Dependency graph
requires:
  - phase: 30-parser-improvements
    provides: multi-param let rec, unit param, top-level let-in (complete parser foundation)
  - phase: 26-quick-fixes
    provides: Prelude file loading pipeline (parseModuleFromString, typeCheckModuleWithPrelude, evalModuleDecls)
provides:
  - FileImportDecl AST node and OPEN STRING grammar rule
  - File-based import: open "path/to/file.fun" works in .fun files
  - Relative path resolution (relative to importing file's directory)
  - Absolute path imports
  - Cycle detection via HashSet prevents stack overflow on circular imports
  - Delegate pattern (fileImportTypeChecker / fileImportEvaluator) wires parse+TC+eval pipeline
affects:
  - 31-02-MOD-05 (record field scoping, shares same pipeline infrastructure)
  - Any future multi-file .fun program tests

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Mutable delegate pattern for late binding (TypeCheck.fileImportTypeChecker, Eval.fileImportEvaluator set by Prelude.fs at module init)
    - currentTypeCheckingFile / currentEvalFile mutables for tracking active file in import chain
    - fileLoadingStack HashSet in Prelude.fs for O(1) cycle detection

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Prelude.fs
    - src/LangThree/Format.fs
    - src/LangThree/Program.fs

key-decisions:
  - "Mutable delegate pattern: TypeCheck.fs and Eval.fs cannot call Prelude (compile order); use mutable function refs set at init time from Prelude.fs"
  - "currentTypeCheckingFile/currentEvalFile mutables: span.FileName is empty string (fsyacc StartPos not initialized); use explicit mutable file tracker instead"
  - "parseModuleFromString made non-private in Prelude.fs: accessible from loadAndTypeCheckFileImpl"
  - "fileLoadingStack HashSet in Prelude.fs: cycle detection uses mutable HashSet (save/restore around each import) rather than Set<string> parameter (would require threading through typeCheckModuleWithPrelude)"

patterns-established:
  - "Pattern: Set TypeCheck.currentTypeCheckingFile <- absFilename before calling typeCheckModuleWithPrelude when processing a named file"
  - "Pattern: Set Eval.currentEvalFile <- absFilename before calling evalModuleDecls when processing a named file"
  - "Pattern: loadAndTypeCheckFileImpl saves/restores currentTypeCheckingFile around recursive load (proper scoping)"

# Metrics
duration: 42min
completed: 2026-03-25
---

# Phase 31 Plan 01: Module System File Import Summary

**`open "path/to/file.fun"` file-based import wired through AST, parser, type checker, and evaluator using mutable delegate pattern to bridge compile-order constraints**

## Performance

- **Duration:** 42 min
- **Started:** 2026-03-25T~09:38Z
- **Completed:** 2026-03-25T~10:20Z
- **Tasks:** 2 of 2
- **Files modified:** 7

## Accomplishments

- Added `FileImportDecl of path: string * Span` to the `Decl` DU in Ast.fs with `declSpanOf` support
- Added `OPEN STRING` and `OPEN STRING Decls` grammar rules in Parser.fsy (no conflict with existing `OPEN QualifiedIdent`)
- TypeCheck.fs: `fileImportTypeChecker` delegate + `resolveImportPath` + `currentTypeCheckingFile` mutable enable file import type checking
- Eval.fs: `fileImportEvaluator` delegate + `currentEvalFile` mutable enable file import evaluation
- Prelude.fs: `loadAndTypeCheckFileImpl` + `loadAndEvalFileImpl` implement the actual file loading with cycle detection; delegates registered at module init time
- Program.fs: sets `currentTypeCheckingFile` and `currentEvalFile` before processing files for correct relative path resolution
- 209 existing tests all pass; pre-existing flaky test (deep nested constructor match, unrelated composeCounter race) confirmed pre-existing

## Task Commits

1. **Task 1: Add FileImportDecl to AST and Parser** - `a73c672` (feat)
2. **Task 2: Implement file loading in TypeCheck.fs and Eval.fs** - `23faac9` (feat)

## Files Created/Modified

- `src/LangThree/Ast.fs` - Added `FileImportDecl of path: string * Span` to Decl DU and `declSpanOf` handler
- `src/LangThree/Parser.fsy` - Added `OPEN STRING` and `OPEN STRING Decls` productions in Decls nonterminal
- `src/LangThree/TypeCheck.fs` - Added `resolveImportPath`, `fileImportTypeChecker` delegate, `currentTypeCheckingFile` mutable, `FileImportDecl` arm in typeCheckDecls fold
- `src/LangThree/Eval.fs` - Added `fileImportEvaluator` delegate, `currentEvalFile` mutable, `FileImportDecl` arm in evalModuleDecls fold
- `src/LangThree/Prelude.fs` - Made `parseModuleFromString` non-private; added `fileLoadingStack`, `loadAndTypeCheckFileImpl`, `loadAndEvalFileImpl`, delegate registration `do` block
- `src/LangThree/Format.fs` - Added `FileImportDecl` case to `formatDecl`
- `src/LangThree/Program.fs` - Sets `currentTypeCheckingFile`/`currentEvalFile` before type-checking/evaluating files

## Decisions Made

1. **Mutable delegate pattern** (not API change): TypeCheck.fs cannot reference Prelude.fs (compile order: TypeCheck=8, Prelude=13). Used mutable `fileImportTypeChecker` and `fileImportEvaluator` function refs set by Prelude.fs at module initialization time.

2. **currentTypeCheckingFile/currentEvalFile mutables** (not span.FileName): fsyacc positions use `lexbuf.StartPos` which is initialized to `pos_fname = ""` by `LexBuffer<char>.FromString`. `Lexer.setInitialPos` sets `EndPos` but not `StartPos`. So `span.FileName` is always empty for file-parsed ASTs. Used explicit mutable instead.

3. **fileLoadingStack HashSet** (not functional Set<string>): To detect cycles, a functional `Set<string>` would need to be threaded through `typeCheckModuleWithPrelude` and `typeCheckDecls` (large API change). Used a module-level `HashSet<string>` that is saved/restored around each load call (safe: single-threaded execution).

4. **`and` mutual recursion dropped**: Initial implementation used `and typeCheckModuleWithPrelude` to join with `let rec typeCheckDecls` (needed for `loadAndTypeCheckFile` calling both). With the delegate pattern, mutual recursion is no longer needed; `typeCheckModuleWithPrelude` is a standalone `let` again.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Compile order prevents TypeCheck.fs from calling Prelude.parseModuleFromString**

- **Found during:** Task 2
- **Issue:** Plan specified calling `Prelude.parseModuleFromString` from TypeCheck.fs and Eval.fs, but `Prelude.fs` is compiled at position 13 while TypeCheck.fs is at position 8, Eval.fs at position 12. Forward reference is not allowed in F#.
- **Fix:** Used mutable delegate pattern: `TypeCheck.fileImportTypeChecker` and `Eval.fileImportEvaluator` are mutable function refs initialized to error stubs, then set by Prelude.fs at module init time via a `do` block.
- **Files modified:** src/LangThree/TypeCheck.fs, src/LangThree/Eval.fs, src/LangThree/Prelude.fs
- **Verification:** Build succeeds, file imports work, tests pass
- **Committed in:** `23faac9`

**2. [Rule 3 - Blocking] span.FileName is empty string in parsed files**

- **Found during:** Task 2 (during smoke testing of relative path resolution)
- **Issue:** `Lexer.setInitialPos` sets `lexbuf.EndPos` only, not `lexbuf.StartPos`. FSharp.Text.Lexing initializes StartPos with `pos_fname = ""`. The filtered-token tokenizer doesn't update lexbuf positions. So `ruleSpan parseState 1 2` produces spans with `FileName = ""`. Relative path resolution using `span.FileName` always fell back to CWD.
- **Fix:** Added `currentTypeCheckingFile` mutable to TypeCheck.fs and `currentEvalFile` mutable to Eval.fs. These are set in Program.fs before processing each file, and saved/restored by Prelude.fs's `loadAndTypeCheckFileImpl`/`loadAndEvalFileImpl` for recursive imports.
- **Files modified:** src/LangThree/TypeCheck.fs, src/LangThree/Eval.fs, src/LangThree/Prelude.fs, src/LangThree/Program.fs
- **Verification:** Relative path `open "utils.fun"` resolves correctly to importing file's directory
- **Committed in:** `23faac9`

**3. [Rule 1 - Bug] Fixed --emit-type not setting currentTypeCheckingFile**

- **Found during:** Task 2 (noticed all Program.fs file paths need the mutable set)
- **Issue:** The `--emit-type --file` branch didn't set `currentTypeCheckingFile` before calling `typeCheckModuleWithPrelude`, so relative imports in emit-type mode would resolve from CWD.
- **Fix:** Added `TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath filename` before type-checking in the emit-type branch.
- **Files modified:** src/LangThree/Program.fs
- **Committed in:** `23faac9`

---

**Total deviations:** 3 auto-fixed (2 blocking, 1 bug)
**Impact on plan:** All auto-fixes necessary for correctness. Architecture changes are contained to the file loading layer. No scope creep.

## Issues Encountered

- Pre-existing flaky test ("deep nested constructor match" in MatchCompileTests.fs) confirmed flaky before and after changes. Root cause: `composeCounter` mutable shared across parallel test execution. Not introduced by this plan.

## Next Phase Readiness

- File-based imports working: `open "path/to/file.fun"` parses, type-checks, and evaluates correctly
- Absolute and relative paths both work
- Cycle detection prevents infinite recursion
- Ready for 31-02 if planned (MOD-05 record field scoping)
- Multi-file .fun programs can now be composed without shell concatenation hacks

---
*Phase: 31-module-system*
*Completed: 2026-03-25*
