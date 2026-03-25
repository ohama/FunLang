---
phase: 29-char-type-comparisons
plan: 01
subsystem: type-system
tags: [char, TChar, lexer, parser, type-inference, builtins, comparisons, FsLex, FsYacc]

# Dependency graph
requires:
  - phase: 26-quick-fixes
    provides: builtin registration pattern (Eval.fs + TypeCheck.fs two-step)
  - phase: 28-n-tuples
    provides: LetPatDecl and general type system stability
provides:
  - char primitive type with TChar, CharValue, Char expr, CharConst, TEChar
  - char literal lexing ('A', '\n', '\t', '\\', '\'') before type_var rule
  - char_to_int : char -> int builtin
  - int_to_char : int -> char builtin (ASCII range 0-127)
  - ordered comparisons widened to int | string | char (TYPE-06)
  - char pattern matching in match expressions
affects:
  - 30-string-operations (can use char type in string manipulation)
  - any phase adding new types (follow same 10-file extension pattern)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New primitive type requires: Ast.fs (Value+Expr+Constant+TypeExpr), Type.fs (Type DU + apply + freeVars + formatType), Unify.fs (TChar,TChar case), Lexer.fsl (literal rule before type_var), Parser.fsy (token + grammar), Elaborate.fs (TEChar->TChar in 3 functions), Bidir.fs (synthesis case), Eval.fs (eval case + formatValue + valuesEqual), TypeCheck.fs (initialTypeEnv + collectMatches), Infer.fs (inferPattern + deprecated inferWithContext), Format.fs (token + expr + typeExpr + pattern), MatchCompile.fs (patternToConstructor + matchesConstructor)"
    - "Char literal lexer rules placed BEFORE type_var using longest-match: 'A' (3 chars) beats 'a type_var (2 chars)"
    - "Comparison operator widening: replace inferBinaryOp call with synth-then-unify-then-check-type pattern"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Unify.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Elaborate.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Format.fs
    - src/LangThree/MatchCompile.fs

key-decisions:
  - "Char literal rules placed before type_var in Lexer.fsl (longest-match disambiguation)"
  - "int_to_char ASCII-only (0-127) - Unicode support deferred"
  - "String comparison uses System.String.CompareOrdinal for ordinal semantics"
  - "Comparison operator widening uses synth-then-unify-then-match pattern (not inferBinaryOp)"

patterns-established:
  - "10-file extension pattern for new primitive types confirmed working"
  - "Unify.fs must have explicit TX,TX case for every new type DU variant"
  - "MatchCompile.fs needs both patternToConstructor (CharConst) and matchesConstructor (CharValue) cases"

# Metrics
duration: 20min
completed: 2026-03-24
---

# Phase 29 Plan 01: Char Type (TYPE-04/05/06) Summary

**`char` primitive type with literal parsing, type inference, pattern matching, char_to_int/int_to_char builtins, and ordered comparisons widened to int/string/char**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-03-24T~15:30Z
- **Completed:** 2026-03-24T~15:50Z
- **Tasks:** 2
- **Files modified:** 12

## Accomplishments
- Full `char` type: `'A'` parses, type-checks as `char`, evaluates to `CharValue 'A'`
- `char_to_int 'A'` returns `65`; `int_to_char 65` returns `'A'`
- Char pattern matching: `match c with | 'A' -> ...` works
- `(x : char)` type annotation accepted
- Ordered comparisons `<`, `>`, `<=`, `>=` now work on `string` and `char` (TYPE-06)
- All 199 existing tests still pass; 0 warnings in LangThree.fsproj

## Task Commits

1. **Task 1: Char AST variants, TChar type, lexer/parser support** - `bcbd0d1` (feat)
2. **Task 2: Bidir synthesis, Eval cases, Infer pattern, Format, char builtins, comparison widening** - `5140405` (feat)

**Plan metadata:** (pending docs commit)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Added TEChar, Char expr, CharConst, CharValue with equality/compare/hash
- `src/LangThree/Type.fs` - Added TChar to Type DU with formatType, apply, freeVars
- `src/LangThree/Unify.fs` - Added TChar, TChar -> empty (auto-fix)
- `src/LangThree/Lexer.fsl` - Added 5 char literal rules + TYPE_CHAR keyword
- `src/LangThree/Parser.fsy` - Added CHAR, TYPE_CHAR tokens; grammar rules for expr/pattern/type
- `src/LangThree/Elaborate.fs` - Added TEChar -> TChar in 3 functions
- `src/LangThree/Bidir.fs` - Added Char synthesis; widened comparisons to int|string|char
- `src/LangThree/Eval.fs` - Added Char eval, CharValue formatValue, CharValue valuesEqual, char_to_int/int_to_char builtins, extended comparison operators
- `src/LangThree/TypeCheck.fs` - Added char_to_int/int_to_char type schemes; added Char to collectMatches
- `src/LangThree/Infer.fs` - Added CharConst pattern inference and Char in deprecated inferWithContext
- `src/LangThree/Format.fs` - Added CHAR/TYPE_CHAR tokens, Char expr, TEChar, CharConst
- `src/LangThree/MatchCompile.fs` - Added CharConst in patternToConstructor, CharValue in matchesConstructor (auto-fix)

## Decisions Made
- Char literal rules placed BEFORE type_var in Lexer.fsl using longest-match disambiguation (3-char literal beats 2-char type var)
- int_to_char ASCII-only (0-127) per research recommendation; Unicode support deferred
- String comparisons use `System.String.CompareOrdinal` for ordinal (byte-by-byte) semantics
- Comparison operator widening uses synth-then-unify-then-match pattern instead of `inferBinaryOp` (which is hardcoded to a single type)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Missing TChar, TChar unification case in Unify.fs**
- **Found during:** Task 2 (testing char_to_int)
- **Issue:** `unifyWithContext` had no `| TChar, TChar ->` arm; `char_to_int 'A'` produced "expected char but got char" error
- **Fix:** Added `| TChar, TChar -> empty` to Unify.fs
- **Files modified:** `src/LangThree/Unify.fs`
- **Verification:** `char_to_int 'A'` returns 65
- **Committed in:** 5140405 (Task 2 commit)

**2. [Rule 2 - Missing Critical] MatchCompile.fs needs CharConst/CharValue cases**
- **Found during:** Task 2 (FS0025 warning in MatchCompile.fs)
- **Issue:** Decision tree compiler missing `CharConst` in `patternToConstructor` and `CharValue` in `matchesConstructor`; char patterns would fail at runtime
- **Fix:** Added `| ConstPat(CharConst c, _) -> Some("#char_" + string (int c), 0)` and `| CharValue c2, c when c.StartsWith("#char_") -> string (int c2) = c.Substring(6)`
- **Files modified:** `src/LangThree/MatchCompile.fs`
- **Verification:** Char pattern matching `match c with | 'A' -> true | _ -> false` works
- **Committed in:** 5140405 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 missing critical)
**Impact on plan:** Both fixes necessary for correctness. Unify.fs is a required companion file for any new type — should be in the standard 10-file pattern. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## Next Phase Readiness
- `char` type fully operational: literals, patterns, builtins, type annotations, comparisons
- TYPE-06 (ordered string/char comparisons) complete — eliminates the 26-equality-chain workaround
- Ready for any phase that needs char manipulation (lexer tools, string character classification)
- Unify.fs pattern confirmed: every new Type DU variant needs an explicit `TX, TX` case

---
*Phase: 29-char-type-comparisons*
*Completed: 2026-03-24*
