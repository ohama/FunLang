# LangThree

## What This Is

FunLang v6.0을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어. F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 패턴 매칭 컴파일, 파이프/합성 연산자, 문자열 내장 함수, printf 포맷 출력, Prelude 표준 라이브러리를 갖춘 완전한 인터프리터.

## Core Value

현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어.

## Requirements

### Validated

- Indentation-based syntax (F# 스타일 들여쓰기 기반 파싱) — v1.0
- ADT (Algebraic Data Types) with GADT support — v1.0
- Records with mutable fields — v1.0
- Module system (F# 스타일, functor 없이) — v1.0
- Exceptions (F# 스타일 — exception, try...with, raise) — v1.0
- Pattern matching compilation to decision trees — v1.0
- File-based testing with fslit (63 tests) — v1.1
- CLI module-level --emit-ast and --emit-type — v1.1
- IndentFilter integration in CLI file mode — v1.1
- Pipe operator `|>` and composition `>>` `<<` — v1.2
- Unit type `()` with side-effect sequencing — v1.2
- String built-in functions (string_length, string_concat, string_sub, string_contains, to_string, string_to_int) — v1.2
- `printf` / `print` / `println` formatted output — v1.2
- Prelude/*.fun directory-based standard library loading — v1.2
- Option type in Prelude (None | Some of 'a) — v1.2
- Comprehensive tutorial documentation (13 chapters, 224 examples) — v1.3
- Tail call optimization (trampoline) — v1.4
- Or-patterns and string patterns in match — v1.4
- Type aliases (`type Name = string`) — v1.4
- List ranges (`[1..5]`, `[1..2..10]`) — v1.4
- Module-level let rec and mutual recursion (`let rec f = ... and g = ...`) — v1.4
- User-defined operators (INFIXOP0-4, `let (op)`, `(op)` function form) — v1.5
- Prelude utilities (not, min, max, abs, fst, snd, ignore) — v1.5
- Prelude operators (`++`, `<|>`, `^^`) — v1.5
- sprintf, printfn, % modulo — v1.5
- Negative integer patterns, tuple lambda (`fun (x,y) ->`) — v1.5
- F#-style offside rule for implicit `in` (CtxtLetDecl + ODECLEND) — v1.6/v1.7
- List separator semicolon `[1; 2; 3]` (F# convention) — v1.7
- Polymorphic GADT return types (`eval : 'a Expr -> 'a`, per-branch type refinement) — v1.8
- File-based module import (`open "path.fun"`) with cycle detection — v2.0
- Multiple module declarations in single file with qualified access — v2.0
- N-tuples (3+ elements) + module-level let-destructuring — v2.0
- Char type with char_to_int/int_to_char + string/char comparison operators — v2.0
- Multi-line list literals, trailing semicolons, list literal patterns — v2.0
- Local let rec (multi-param), unit param shorthand, else+keyword, deep nesting — v2.0
- File I/O (14 builtins: read_file, write_file, stdin, get_args, get_env, eprint, etc.) — v2.0
- failwith builtin, option alias, Prelude path fix, empty file guard — v2.0
- TCO regression fix (LetRec/LetRecDecl BuiltinValue tailPos=true) — v2.1
- MatchCompile test isolation (local counter per compileMatch) — v2.1
- flt test coverage for phases 29-32 (char, parser, file import, file I/O) — v2.1
- Zero FS compile warnings in test suite — v2.1
- E0313 qualified access fix (imported + Prelude modules) — v2.2
- Inline try-with without leading pipe — v2.2
- failwith, LetPatDecl, qualified access flt tests — v2.2
- Mutable Array (create/get/set/length/ofList/toList) + Prelude/Array.fun — v3.0
- Mutable Hashtable (create/get/set/containsKey/keys/remove) + Prelude/Hashtable.fun — v3.0
- Array higher-order functions (iter/map/fold/init) with callValueRef pattern — v3.0
- Mutable data structures tutorial (19-mutable-data.md) + flt test suite — v3.0
- Mutable variables (`let mut x = expr`, `x <- expr`) with RefValue, closure capture — v4.0
- Mutable variable error diagnostics (E0320 immutable assign, E0301 type mismatch) — v4.0
- Mutable variables tutorial (20-mutable-variables.md) + 30 flt tests — v4.0
- Expression sequencing (`e1; e2`) via SeqExpr nonterminal (OCaml pattern) — v5.0
- While/for loops (`while cond do body`, `for i = s to e do body`, `for i = s downto e do body`) — v5.0
- Array/hashtable indexing syntax (`arr.[i]`, `arr.[i] <- v`, `ht.[key]`, `ht.[key] <- v`, chained `matrix.[r].[c]`) — v5.0
- If-then without else (`if cond then expr` for unit-returning branches) — v5.0
- Imperative ergonomics tutorial (21-imperative-ergonomics.md) + 22 flt tests — v5.0

### Future

- Expression-level mutual recursion (`let rec f x = ... and g y = ... in expr`) — 현재 module-level만 지원
- Type classes / interfaces
- Computation expressions
- do binding
- Seq expressions
- Interpreter performance optimization (JIT/bytecode)

### Out of Scope

- OCaml 스타일 functor — F# 스타일 모듈만
- 컴파일러 (네이티브/바이트코드) — 인터프리터 유지
- IDE 통합 / LSP — 언어 기능 완성 후
- 네트워크 I/O — 파일/시스템 I/O만 이번 마일스톤

## Current Milestone: v6.0 Practical Programming

**Goal:** 뉴라인 기반 암묵적 시퀀싱, 컬렉션 for-in 루프, Option/Result 유틸리티 함수로 실용적 프로그래밍 완성

**Target features:**
- 뉴라인 암묵적 시퀀싱 — 같은 들여쓰기의 다음 줄이 자동으로 `;` 시퀀싱 (IndentFilter 변경)
- `for x in collection do body` — 리스트/배열 직접 반복
- Option/Result 유틸리티 함수 — map, bind, getOrDefault 등 Prelude 함수

## Context

**Shipped:** v5.0 Imperative Ergonomics (2026-03-28)
- 23+ source files, ~12,577 lines of F#
- 224 F# unit tests + 573 flt tests, all passing, 0 warnings
- 49 phases, 108 plans executed across v1.0-v5.0

**기반 코드**: ../LangTutorial의 FunLang v6.0
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플

**기술 스택**: F# / .NET 10 / fslex / fsyacc

## Constraints

- **Tech stack**: F# / .NET 10 — FunLang과 동일
- **Parser**: fslex / fsyacc — 기존 인프라 활용
- **Scope**: 각 기능의 기본형만. 고급 기능은 이후 마일스톤

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F# 스타일 선택 | OCaml보다 단순, 들여쓰기 기반이 현대적 | ✓ Good |
| GADT 포함 | 표현력 있는 타입 시스템, FunLang의 bidirectional checking 활용 | ✓ Good |
| Functor 제외 | 복잡도 대비 실용성 낮음 | ✓ Good |
| Jules Jacobs algorithm for pattern matching | Binary trees simpler than Maranget N-way, no clause duplication | ✓ Good |
| evalFn parameter pattern | Avoids circular F# module dependency between MatchCompile and Eval | ✓ Good |
| Record field-name encoding in constructor names | Enables partial record pattern matching in decision trees | ✓ Good |
| BuiltinValue DU for native F# functions | Clean carrier for all built-in functions, CustomEquality on Value | ✓ Good |
| Closure-based composition (composeCounter) | Unique names avoid stack overflow in chained >> | ✓ Good |
| Prelude/*.fun directory loading | Modular standard library, type-checked prelude files | ✓ Good |
| InLetDecl context stack for offside rule | Replaced fragile LetSeqDepth counter, handles nesting naturally | ✓ Good |
| SemiExprList separate from ExprList | Lists use `;`, tuples keep `,`, no grammar conflicts | ✓ Good |
| synth GADT → fresh-var check delegation | Replaces E0401 error, enables annotation-free GADT match | ✓ Good |
| isPolyExpected per-branch isolation | Each GADT branch gets independent expected type, no cross-contamination | ✓ Good |
| BuiltinValue tailPos=true for TCO | BuiltinValue wrapper must eval body with tailPos=true to propagate TailCall to App trampoline | ✓ Good |
| Local counter in compileMatch | Eliminated global mutable freshTestVar, each compileMatch call gets independent counter | ✓ Good |
| ArrayValue uses Value array (no outer ref) | In-place element mutation via arr.[i] <- v; reference equality | ✓ Good |
| HashtableValue uses Dictionary<Value, Value> | Reference equality; OOB/missing-key errors via LangThreeException | ✓ Good |
| callValueRef forward reference pattern | Builtins that invoke user closures use mutable ref wired after eval defined | ✓ Good |
| RefValue for mutable variables | Mutable vars stored as RefValue(Value ref) in env; closures share ref cells | ✓ Good |
| Module-level mutableVars Set in Bidir.fs | Avoids threading mutableVars through 67+ synth/check call sites | ✓ Good |
| Monomorphic mutable variables | No generalization for let mut — prevents unsound polymorphism | ✓ Good |
| `mut` keyword alias for `mutable` | Both `let mut x` and `let mutable x` accepted — user convenience | ✓ Good |
| SeqExpr nonterminal for `;` sequencing | OCaml parser.mly pattern — separate nonterminal avoids LALR conflict with list/record semicolons | ✓ Good |
| Parser desugar for `e1; e2` | Desugar to LetPat(WildcardPat, e1, e2) — zero changes to Eval/Bidir/Infer | ✓ Good |
| DOTLBRACKET single token for `.[` | Avoids LALR shift/reduce conflict with `.IDENT` field access | ✓ Good |
| IndexGet/IndexSet AST nodes (not desugar) | Need type-level TArray/THashtable dispatch — can't desugar without type info | ✓ Good |
| Parser desugar for if-then-without-else | `if c then e` → `If(c, e, Tuple([]))` — zero changes to 6 files | ✓ Good |
| For-loop variable immutability via mutableVars exclusion | Reuses E0320 from Phase 42 — zero new error infrastructure | ✓ Good |

---
*Last updated: 2026-03-28 after v6.0 milestone started*
