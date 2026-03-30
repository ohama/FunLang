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
- Newline implicit sequencing (IndentFilter SEMICOLON injection for same-indent lines) — v6.0
- For-in collection loops (`for x in collection do body` for lists and arrays) — v6.0
- Option/Result prelude utilities (optionIter/Filter/DefaultValue/IsSome/IsNone + resultIter/ToOption/DefaultValue) — v6.0
- Practical programming tutorial (22-practical-programming.md) + 9 flt tests — v6.0
- string_endswith, string_startswith, string_trim builtins + String module extensions — v7.1
- hashtable_trygetvalue, hashtable_count builtins + Hashtable module extensions — v7.1
- StringBuilder.append → StringBuilder.add rename (List.append conflict fix) — v7.1
- ForInExpr tuple destructuring (`for (k, v) in ht do ...`) + hashtable tuple iteration — v7.1
- 39 stale flt binary paths fixed + 25 dot-notation tests converted to module API — v7.1
- Value-type FieldAccess dispatch removed from Eval.fs + Bidir.fs (순수 함수형 API) — v7.1
- Angle bracket generic type syntax (`Result<'a>`, `Map<string, int>`, `Either<'a, 'b>`) — v8.0
- Module-level parameter type annotations (`let f (x : int) y (z : bool) = ...`) — v8.0
- Module-level return type annotations (`let f x : int = ...`) — v8.0
- MixedParamList parser rule (plain/annotated param mixing) — v8.0
- `let rec ... and ...` mutual recursion with type annotations — v8.0

### Active

- (None — all active requirements shipped in v8.0)

### Future

- Expression-level mutual recursion (`let rec f x = ... and g y = ... in expr`) — 현재 module-level만 지원
- Type classes / interfaces
- Computation expressions
- do binding
- Seq expressions
- Interpreter performance optimization (JIT/bytecode)

### Out of Scope

- OCaml 스타일 functor — F# 스타일 모듈만
- .NET interop 유지 — 네이티브 구현으로 대체
- LangBackend 컴파일러 구현 — 인터프리터 구현 완료 후 별도 마일스톤
- IDE 통합 / LSP — 언어 기능 완성 후
- Dot notation / OOP 스타일 dispatch — v7.1에서 제거, 순수 함수형 API만 유지
- Unicode 지원 — ASCII로 충분

## Previous Milestone: v8.0 Declaration Type Annotations (2026-03-30)

**Delivered:** 앵글 브래킷 제네릭 + module-level 타입 어노테이션 (파라미터 + 반환)
- 26 files changed, +2,193 LOC in v8.0
- ~650 flt tests, 224 unit tests
- 64 phases, 138 plans executed across v1.0-v8.0

## Previous Milestone: v7.1 Remove Dot Notation (2026-03-29)

**Delivered:** OOP 스타일 dot dispatch 제거, 순수 함수형 module function API로 통일
- 69 files changed, +3,234 LOC in v7.1
- 637 flt tests, 224 unit tests
- 62 phases, 133 plans executed across v1.0-v7.1
- 22 tutorial chapters

**기반 코드**: ../LangTutorial의 FunLang v6.0
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플

**기술 스택**: F# / .NET 10 / fslex / fsyacc

**v7.0 동기**: FunLexYacc가 .NET interop으로 사용하는 컬렉션(Dictionary, HashSet, Queue, List, StringBuilder)을 네이티브로 구현하여 LangBackend 컴파일러에서도 동작하게 함. langbackend-feature-requests.md에 51개 기능 갭 문서화됨.

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
| SEMICOLON injection gated on InExprBlock context | isAtSameLevel branch with isContinuationStart + isStructuralTerminator guards | ✓ Good |
| ForInExpr loop var as Scheme([], elemTy) without mutableVars | Reuses E0320 immutability — zero new error infrastructure | ✓ Good |
| Option/Result utilities purely additive .fun files | Zero interpreter changes — alphabetical load order provides constructors | ✓ Good |

| .NET interop 대신 네이티브 구현 | LangBackend 컴파일러에서 .NET 사용 불가 | — Pending |
| 인터프리터 먼저 구현 | 빠른 개발/테스트 사이클, API 설계 검증 | — Pending |
| FunLexYacc 호환 API | FunLexYacc 코드 최소 변경으로 네이티브 타입 사용 | — Pending |
| Dot notation 제거 | OOP 스타일 제거, 순수 함수형 API 통일 — FunLexYacc는 별도 마일스톤에서 수정 | ✓ Good |
| LT/GT 토큰 재활용 for angle brackets | 새 토큰 불필요, LALR(1) 상태로 구별 | ✓ Good |
| MixedParamList가 ParamList 대체 | reduce/reduce 충돌 회피, 하위 호환 유지 | ✓ Good |
| 반환 타입을 Annot 노드로 래핑 | 런타임 소거, AST 변경 최소화 | ✓ Good |

---
*Last updated: 2026-03-30 after v8.0 milestone complete*
