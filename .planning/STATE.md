# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-18)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v1.3 Language Completion — Planning next milestone

## Current Position

Milestone: v1.3 Language Completion
Phase: 13 (tail-call-optimization) — Not started
Plan: Not started
Status: Ready to plan
Last activity: 2026-03-18 — v1.2 milestone complete

Progress: v1.0 (7 phases, 32 plans) ✓ + v1.2 (5 phases, 12 plans) ✓

## Performance Metrics

**Velocity:**
- Total plans completed: 40
- Average duration: 4.4 min
- Total execution time: ~2.94 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 4 | 37 min | 9.3 min |
| 02 | 2 | 8 min | 4.0 min |
| 03 | 7 | 21 min | 3.0 min |
| 04 | 5 | 16 min | 3.2 min |
| 05 | 5 | 25 min | 5.0 min |
| 06 | 4 | 27 min | 6.8 min |
| 07 | 3 | 8 min | 2.7 min |
| 08 | 5 | 12 min | 2.4 min |
| 09 | 1 | 8 min | 8.0 min |
| 10 | 2 | 9 min | 4.5 min |
| 11 | 2 | 10 min | 5.0 min |
| 12 | 2 | 13 min | 6.5 min |

**Recent Trend:**
- Last 5 plans: 11-01 (5min), 11-02 (5min), 12-01 (2min), 12-02 (11min)
- Trend: Phase 12 complete. print/println/printf with 8 integration tests. 196 F# + 201 fslit tests passing.

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- F# 스타일 선택 (over OCaml) — 들여쓰기 기반이 현대적, 단순함
- GADT 포함 — bidirectional checking 활용, 표현력 있는 타입 시스템
- Functor 제외 — 복잡도 대비 실용성 낮음

**From 01-01 (Match Expression Indentation):**
- Enter match context before processing newline to enable pipe alignment validation
- Pop match contexts automatically when dedenting below their base level
- Pipes in match expressions align with 'match' keyword column, not indented from it

**From 01-02 (Multi-line Function Application):**
- canBeFunction (IDENT | RPAREN) identifies function positions, isAtom identifies argument positions
- Prevent re-entering InFunctionApp context when already in one to avoid nested INDENT tokens
- Consume newlines within function app context (emit no tokens) to allow multi-line arguments without extra INDENT/DEDENT

**From 01-04 (Module-Level Declarations):**
- Module and Decl types separate from Expr for clear file structure
- Function declarations desugar to nested lambdas (let f x y = e → let f = fun x -> fun y -> e)
- IndentFilter removes same-level NEWLINEs - rely on token boundaries in grammar

**From 01-03 (Improved Error Messages):**
- formatExpectedIndents shows all valid indent levels from stack plus "or a new indent level" hint
- validateIndentWidth enforces multiples only when StrictWidth=true (strict mode for style guides, lenient for development)
- EOF handling must emit all DEDENTs returned by processNewline in a single call (not one per loop iteration)
- Error message format: context-specific description with line number, actual column, and expected values

**From 02-04 (Exhaustiveness Checking):**
- Self-contained CasePat type decoupled from AST Pattern for independent testing
- Constructor sets passed explicitly (functional style) rather than global registry
- Maranget usefulness algorithm with complete/incomplete signature branching

**From 02-06 (Exhaustiveness Wiring):**
- Infer scrutinee type from constructor patterns rather than re-synthesizing (avoids scope issues)
- W-prefix for warning codes to distinguish from E-prefix error codes
- typeCheckModule returns Result<Diagnostic list * RecordEnv, Diagnostic> (warnings + RecordEnv on success) -- updated in 05-03

**From 03-01 (Record Foundation):**
- substTypeExprWithMap extracted as shared module-level helper for both ADT and record elaboration
- RecordEnv parallel to ConstructorEnv for record type metadata
- Record error codes use E03xx range (E0307-E0313)

**From 03-02 (Record Parser Grammar):**
- RecordExprInner with function return resolves LBRACE IDENT LALR(1) ambiguity
- Left-recursive Atom DOT IDENT enables chained field access (a.b.c)
- IndentFilter unchanged -- no bracket tracking needed for braces

**From 03-03 (Record Type Checking):**
- recEnv added as separate parameter to synth/check/inferBinaryOp (parallel to ctorEnv)
- Record type resolved from field set (globally unique field names)
- typeCheckModule returns RecordEnv alongside warnings for evaluator use

**From 03-04 (Record Evaluation):**
- recEnv added as first param to eval/evalMatchClauses, threaded through all recursive calls
- resolveRecordTypeName: field-set lookup against RecordEnv for type name resolution
- Program.fs --file migrated to parseModule+typeCheckModule pipeline
- --expr and REPL pass Map.empty for recEnv (no module-level record type declarations)

**From 03-05 (Record Integration Tests):**
- Equality operator = ambiguous with let binding = in contexts like `let result = a = b`; use if-then-else wrapper
- All 21 record tests passed on first run -- no bugs found in plans 01-04 implementation

**From 03-06 (Mutable Field Syntax):**
- Token declarations must be in Parser.fsy before lexer can reference them (lexer imports from Parser module)
- SetField at Expr level for low-precedence assignment semantics

**From 03-07 (Mutable Field Semantics):**
- TTuple [] as unit type representation (no dedicated TUnit in Type system)
- RecordValue uses Map<string, Value ref> for mutable field support
- Module-level let for sequencing mutations in tests (no in keyword at module level)

**From 04-01 (GADT Foundation):**
- GadtConstructorDecl AST node with argTypes list and explicit returnType
- TEData TypeExpr variant for parameterized named types (int expr) separate from TEName
- ConstructorInfo extended with IsGadt bool and ExistentialVars int list
- GADT constructor parsed via splitGadt: single grammar rule to avoid LALR conflicts
- Existential vars = arg type vars minus result type vars
- GADT error codes use E04xx range (E0401-E0403)

**From 04-02 (GADT Elaboration):**
- Constructor-local type vars get fresh indices via freshTypeVarIndex, extending paramMap per-constructor
- IsGadt sweep: if any constructor uses GADT syntax, ALL constructors marked IsGadt=true
- inferTypeFromPatterns builds generic type from TData name for GADT constructors (exhaustiveness)

**From 04-03 (GADT Type Refinement):**
- Local constraints from unifying scrutinee with constructor return type stay branch-local (not composed into global subst)
- Each branch gets independent local constraints -- no cross-branch leakage
- Body substitution propagates (may resolve globally-scoped type variables)
- isGadtMatch guard dispatches GADT vs regular ADT match handling

**From 04-04 (GADT Exhaustiveness Filtering):**
- Two-phase type inference: generic type for constructor lookup, specific type for GADT filtering
- filterPossibleConstructors: structural type arg comparison, conservative when type variables present
- inferSpecificScrutineeType: raw ResultType from first GADT constructor pattern

**From 04-05 (GADT Integration Tests):**
- GADT match tests use (match ... : ResultType) annotation to enter check mode
- Scrutinee annotation alone is insufficient -- the match expression itself must be in check mode
- All 17 GADT tests passed; zero bugs found in plans 01-04 implementation

**From 05-01 (Module System Foundation):**
- OpenDecl path is string list (not single string) to support qualified opens like open A.B.C
- QualifiedIdent rule added in Plan 01 to avoid concurrent Parser.fsy edits in Plan 02
- E05xx error code range for module system errors
- Module system keywords (module, namespace, open) lexed before general identifier rule

**From 05-02 (Module Parser Grammar):**
- Top-level module/namespace rules placed before Decls EOF for LALR priority
- Nested module uses EQUALS INDENT Decls DEDENT (top-level has no =)
- Open directive and nested module follow singleton/continuation pattern in Decls

**From 05-04 (Module-Aware Evaluation):**
- ModuleValueEnv has CtorEnv for constructor qualified access separate from Values
- FieldAccess dispatches on module name via Var pattern match before falling through to record field
- evalModuleDecls is standalone let rec, not part of eval/evalMatchClauses and-group
- Constructor names collected from both ConstructorDecl and GadtConstructorDecl in TypeDecl

**From 05-05 (Module Integration Tests):**
- AST rewriting approach for qualified module access (avoids threading modules through 47 synth/check call sites)
- Uppercase idents parsed as Constructor nodes (not Var) -- both type checker and evaluator need to handle this for module dispatch
- TypeDecl in evalModuleDecls must register constructors as FunctionValue/DataValue for qualified constructor access
- rewriteModuleAccess converts FieldAccess(Constructor(modName), member) to Var/Constructor before synth
- App(Module.Ctor, arg) rewrites to Constructor(name, Some arg) not App(Constructor(name, None), arg)

**From 06-02 (Exception Type Checking):**
- Guarded patterns excluded from exhaustiveness matrix (guard may fail, so pattern doesn't guarantee coverage)
- W0003 warning for try-with without catch-all handler (open type can't be exhaustively matched)
- Exception constructors added to typeEnv as functions for higher-order use

**From 06-01 (Exception Foundation):**
- MatchClause changed from Pattern * Expr to Pattern * Expr option * Expr (when guard slot)
- TExn type for exception base type (open, no type parameters)
- Exception error codes use E06xx range (E0601-E0604), W0003 for non-exhaustive handler
- ExceptionDecl uses same singleton/continuation pattern as OpenDecl in parser
- raise uses Atom (not AppExpr) to avoid ambiguity with constructor application

**From 06-03 (Exception Runtime):**
- LangThreeException carries Value directly (DataValue at runtime), reuses matchPattern/evalMatchClauses
- When guards evaluated in extended env after pattern bindings; skip clause if not BoolValue true
- InTry indent context mirrors InMatch exactly (pipe alignment, DEDENT popping, JustSawTry flag)

**From 06-04 (Exception Integration Tests):**
- TExn unification case was missing in Unify.fs (bug fix)
- InTry context popping uses strict < (not <=) because try body DEDENTs back to try level before pipes
- Try-with re-raises exception when no handler matches (enables nested try-with)
- typeCheckDecls fold now threads ctorEnv/recEnv so open directive propagates constructors

**From 07-01 (Match Compilation Foundation):**
- MatchCompile.fs only opens Ast -- no dependency on Eval.fs (avoids circular deps)
- evalDecisionTree takes evalFn parameter for decoupling from Eval module
- Constructor names use prefix encoding: #tuple_N, #int_N, #bool_N for non-ADT patterns
- Record sub-patterns sorted alphabetically by field name for canonical ordering
- resetTestVarCounter called per compileMatch for deterministic variable numbering

**From 07-02 (Eval Integration):**
- Record constructor encoding changed from #record_N to #record:fieldA,fieldB for partial record pattern support
- Match case uses MatchCompile.compileMatch + evalDecisionTree; TryWith remains sequential
- matchPattern and evalMatchClauses preserved (used by TryWith and LetPat)

**From 07-03 (Match Compilation Tests):**
- 17 integration tests verify decision tree correctness across all pattern types
- Structural tree-walking test confirms no redundant constructor tests per decision path
- All 196 tests pass (zero regression across all phases)

**From 08-01 (Expression AST Emit Tests):**
- One .flt file per AST node type (fslit limitation: one test per file)
- 28 files created (not 16 as planned) to cover all expression node types
- Use single quotes for embedded double quotes in fslit Command lines
- ast-expr-*.flt naming convention with hyphen suffixes for variants

**From 08-02 (Type Expression Emit Tests):**
- Use single quotes for --expr args containing string literals in .flt files (avoids shell escaping issues)
- type-expr-*.flt naming convention for --emit-type expression tests

**From 08-03 (Declaration AST Emit Tests):**
- ast-decl-*.flt naming convention for declaration-level AST tests
- All declaration node types covered: LetDecl, TypeDecl (ADT/GADT/mutual), RecordDecl, ExceptionDecl, ModuleDecl, OpenDecl, NamespaceDecl

**From 08-04 (Type Declaration Emit Tests):**
- type-decl-*.flt naming convention for declaration-level type inference tests
- Builtin names (id, const) filtered from --emit-type output; use non-builtin names for polymorphic tests
- --emit-type file mode outputs bindings in alphabetical order

**From 08-05 (Pattern AST Emit Tests):**
- ast-pat-*.flt naming convention for pattern node AST tests
- String constant patterns not supported by parser (parse error) -- skipped
- When guard parsed but not shown in AST output (Format.fs binds as _guard)

**From 09-01 (Pipe & Composition Operators):**
- Unique compose variable names per closure (composeCounter) to avoid stack overflow in chained composition
- Pipe/composition precedence: PIPE_RIGHT < COMPOSE_RIGHT = COMPOSE_LEFT < OR (lowest of all operators)
- Closure-based composition: compose operators capture evaluated function values with unique names in minimal closure env
- No Prelude.fun file exists; prelude functions are type-only (in initialTypeEnv) not eval-available

**From 10-01 (Unit Type):**
- Unit representation: TTuple [] / TupleValue [] (no new TUnit/UnitValue ADT case needed — already established in 03-07)
- TYPE_UNIT lexer keyword (consistent with TYPE_INT/TYPE_BOOL pattern, not special-casing in elaborator)
- fun () -> body desugars to LambdaAnnot("__unit", TETuple [], body) — explicit type annotation ensures unit -> T type
- Var("()") sentinel replaced with Tuple([], ...) in indented-let continuation (now that () is a real expression)
- let _ = at Decl level produces LetDecl("_", body, ...) — binds "_" in env harmlessly, enables sequencing

**From 11-01 (String Operations Infrastructure):**
- BuiltinValue of fn:(Value->Value) in Value DU — native F# function carrier for all built-in functions
- valuesEqual explicit helper required: BuiltinValue fn type breaks F# auto-derived structural equality; never use = operator directly on Value
- initialBuiltinEnv : Env in Eval.fs — merge into startup env in Program.fs and Repl.fs (Plan 02)
- to_string: Scheme([0], TArrow(TVar 0, TString)) — permissively polymorphic; runtime enforces int/bool/string only
- string_sub uses start+length semantics: string_sub s start len = s.[start .. start+len-1]

**From 11-02 (String Operations Wiring + Tests):**
- Startup merge: Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv in Program.fs and Repl.fs
- and [<CustomEquality; CustomComparison>] Value requires inline attribute on the and-clause (not preceding line); implements Equals/GetHashCode/IEquatable/IComparable; BuiltinValue always returns false for equality
- fslit single-quote quoting: use single quotes for --expr args with string literals in .flt Command lines
- Pre-existing warnings fixed: Exhaustive.fs RecordPat, Format.fs MODULE/NAMESPACE/OPEN, Eval.fs FS0067

**From 12-01 (Printf Output):**
- printf typed as Scheme([0], TArrow(TString, TVar 0)) — cannot know arity at type-check time; runtime enforces specifier types
- Use stdout.Write + stdout.Flush() (not F# printfn) to avoid formatting interference and ensure ordering
- applyPrintfArgs must be let rec (recursive); parsePrintfSpecifiers/printfFormatArg/substitutePrintfArgs are non-recursive let
- All four helpers must appear before initialBuiltinEnv in Eval.fs to be in scope
- Zero-specifier printf (e.g., printf "hello") writes format string directly via applyPrintfArgs fmt [] []

**From 12-02 (Printf Integration Tests):**
- fslit substitutes %s in Command lines (like %input — replaces with test file path); tests with %s in --expr arg must use %input file-mode instead
- Program.fs file-mode result printing: use Map.tryFind lastName finalEnv (not re-eval lastBody) to avoid double side effects
- print/println in --expr mode: side-effect text appears immediately before () result on same line (no newline between)
- println in --expr mode: side-effect text on its own line, () result on the next line

### Roadmap Evolution

- Phase 7 added: Pattern Matching Compilation (decision tree per Jules Jacobs 2021 algorithm)
- Phase 8 added: Full Coverage fslit Testing (--emit-ast, --emit-type, 100% grammar coverage)

### Pending Todos

None yet.

### Blockers/Concerns

**Phase 1 dependencies:**
- Indentation lexer state management (Python algorithm well-documented, low risk)
- Spaces-only enforcement critical for correctness

**From 01-04:**
- **Nested indentation-based let:** Current implementation requires explicit `in` keywords for nested let bindings inside indented blocks. Full indentation-based `let` sequences not yet supported. Workaround: use explicit `in` keywords

**Phase 4 (GADT) -- COMPLETE:**
- All challenges resolved; 132 tests pass

**Phase 5 (Modules) known challenges:**
- Circular dependency detection: RESOLVED in 05-03 (DFS 3-color algorithm)
- Two-phase compilation design needed

**From 05-03 (Module-Scoped Type Checking):**
- ModuleExports captures only new bindings (not inherited from parent scope) for clean module isolation
- Exhaustiveness checking extracted to checkMatchWarnings, runs per-LetDecl in typeCheckDecls
- typeCheckModule now returns Result<Diagnostic list * RecordEnv * Map<string, ModuleExports>, Diagnostic>

## Session Continuity

Last session: 2026-03-18 (v1.2 milestone archived)
Stopped at: v1.2 complete — ready for v1.3
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-18 (v1.2 milestone complete, archived)*
