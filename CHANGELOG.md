# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [0.1.8] - 2026-04-14

### Changed
- **Breaking (pre-1.0):** `Hashtable.tryGetValue` (및 `hashtable_trygetvalue_str`)의 반환 타입을 `(bool * 'v)` tuple에서 `'v option`으로 변경 (Issue #28). FunLangCompiler 런타임과 일치. 마이그레이션: `let (found, v) = ...` / `snd (...)` → `match ... with | Some v -> ... | None -> ...`.

## [0.1.7] - 2026-04-14

### Fixed
- `typeCheckFile` AnnotationMap이 **모든** import된 파일의 span을 보존하도록 수정 (Issue #27). 근본 원인은 `typeCheckModuleWithPrelude`가 재귀 호출마다 `Bidir.annotationMap`을 reset하여 N개 import 시 main + 마지막 import만 남던 것. `loadAndTypeCheckFileImpl`에서 save/merge 패턴 + `tcAnnotCache`로 cache hit 경로도 처리.

## [0.1.6] - 2026-04-14

### Fixed
- `typeCheckFile`이 import된 파일의 AST span을 `AnnotationMap`에 포함하도록 수정 (Issue #26). 근본 원인은 `Prelude.parseModuleFromString`가 position-tracking 없는 tokenizer를 사용하여 import된 파일의 모든 span이 초기 위치(1:0)에 머물고 annotationMap 엔트리가 서로 overwrite되던 것. `Program.parseModuleFromString` 패턴으로 `PositionedToken` 사용하도록 통일.

## [0.1.5] - 2026-04-14

### Fixed
- RecordExpr이 동일 필드 집합의 여러 record 타입에서 ambiguous할 때 outer type annotation으로 구분 (Issue #25). `let p : Ps = { file; line }` 같은 패턴이 false `DuplicateFieldName` 에러 없이 동작. `check` fall-through에서 `InCheckMode`를 push하여 함수 인자 등 모든 check 경로에서 expected type이 synth로 전달됨.

## [0.1.4] - 2026-04-13

### Changed
- **Breaking (pre-1.0):** `s.[i]`가 `int`가 아닌 `char`를 반환하도록 변경 (Issue #23, Issue #15 결정 반전). char 리터럴(`' '`, `'\t'`)과 타입이 일치하여 `if c = ' '` 같은 자연스러운 비교가 가능해짐. ASCII code가 필요하면 `char_to_int s.[i]` 사용.

## [0.1.3] - 2026-04-13

### Fixed
- `TEName` annotation이 fresh TVar 대신 alias/ADT/record로 올바르게 resolve (Issue #22). `let f (p : SrcLoc) = p.field` 같은 annotated parameter + field access 패턴이 정상 동작.

### Added
- 타입 alias 실제 구현: `AliasInfo`/`AliasEnv` 타입, `Elaborate.currentAliasEnv` state, first-pass에서 `TypeAliasDecl` 등록. 지금까지 완전 no-op였던 `type Name = string` 등의 alias가 실제로 확장됨 (parameterized alias 포함).

## [0.1.2] - 2026-04-13

### Removed
- `DuplicateRecordField(E0311)` 체크 제거 — 서로 다른 record 타입에서 동일 필드명 허용 (Issue #21). ML 계열(OCaml, F#)의 정상 패턴이며, 이 체크가 Issue #20의 FieldAccess TData annotation을 무효화하고 있었음.

### Fixed
- 동일 필드명을 가진 여러 record 타입의 FieldAccess가 정상 동작 (예: `type Foo = { start: int }` + `type Bar = { start: int; name: string }` → `f.start`, `b.start` 각각 올바른 값 반환)

## [0.1.1] - 2026-04-10

### Fixed
- FieldAccess가 accessExpr span에 resolved record 타입(TData)을 annotationMap에 기록하도록 수정 (Issue #20)

## [0.1.0] - 2026-04-10

### Added
- Type class system (typeclass/instance, constraint inference, dictionary elaboration) — v10.0
- Show/Eq built-in instances (int/bool/string/char) — v10.0
- Type class error reporting (E0701-E0706 with source spans) — v10.1
- Module system type class integration (ClassEnv/InstanceEnv export) — v10.1
- Per-expression type annotation map (ConcurrentDictionary<Span, Type>) — v11.0
- Top-level binding type environment export (BindingEnv) — v11.0
- `--emit-typed-ast` CLI flag for JSON typed AST output — v11.0
- String-key hashtable builtins (7 functions) — v11.1
- `dbg` builtin ('a -> 'a, stderr + identity) — v11.1
- `#[left N]`/`#[right N]` attribute parsing for operator fixity — v12.0
- FixityEnv with Pratt precedence climbing post-parse rewrite — v12.0
- `|>`/`>>`/`<<` operators moved to Prelude/Core.fun — v12.0
- List module extensions (init, find, findIndex, partition, groupBy, scan, replicate, collect, pairwise, sumBy, sum, minBy, maxBy, contains, unzip) — v13.0
- String module extensions (split, indexOf, replace, toUpper, toLower, substring, join) — v13.0
- Prelude function type annotations + `fun x ->` to direct argument conversion — v14.0
- Collection type annotations (Array/Hashtable/HashSet/Queue/MutableList/StringBuilder) — v14.0
- THashSet/TQueue/TMutableList/TStringBuilder type system registration — v14.0
- `import` keyword for file imports — v14.0
- String.toInt, String.ofInt — v14.0

### Fixed
- Per-parameter unique spans for nested LambdaAnnot (Issue #18) — v15.0 Phase 102
- Bidir.fs annotationMap population with per-param LambdaAnnot spans (Issue #19) — v15.0 Phase 103
- LetRec/LetRecDecl extended to 6-tuple with Span option for annotation tracking — v15.0 Phase 103
- FunLangCompiler annotationMap reliability (Issues #11, #14, #16) — v15.0
- String indexing `s.[i]` in type checker and eval (Issue #15) — v15.0
- OccursCheck error messages with formatTypeNormalized — v14.0
- if-then-else/if-then-without-else greedy continuation consumption — v14.0

### Changed
- PipeRight/ComposeRight/ComposeLeft AST nodes removed (operators now in Prelude) — v12.0
- Operator/instance method type annotation parser support (OpName/InstanceMethod MixedParamList) — v14.0
