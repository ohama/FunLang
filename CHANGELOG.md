# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

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
