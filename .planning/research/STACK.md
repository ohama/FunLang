# Technology Stack: LangThree Feature Implementation

**Project:** LangThree - ML-style functional language with F# features
**Researched:** 2026-02-25
**Domain:** Programming language implementation (lexer/parser/type checker)

## Executive Summary

This research focuses on specific techniques for implementing indentation-based syntax, ADT/GADT, Records, Modules, and Exceptions in F# using fslex/fsyacc. The recommendations are prescriptive and implementation-focused, based on FunLang v6.0's existing Hindley-Milner type inference and bidirectional type checking foundation.

**Key Finding:** Indentation-based parsing is the most technically challenging feature, requiring significant lexer state management. GADTs present theoretical complexity but can leverage existing bidirectional type checking. Other features (Records, Modules, Exceptions) are relatively straightforward grammar extensions.

---

## Core Stack (Inherited from FunLang)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| F# | .NET 10 | Implementation language | Required by FunLang base, excellent for compiler work |
| fslex | FsLexYacc 12.x+ | Lexer generation | Standard F# lexer generator, OCamllex-compatible |
| fsyacc | FsLexYacc 12.x+ | Parser generation | LALR parser generator, OCamlyacc-compatible |
| FunLang v6.0 | Base | Type inference foundation | Provides Hindley-Milner + bidirectional checking |

---

## Feature 1: Indentation-Based Syntax

### Recommended Approach: Token Queue with Indentation Stack

**Confidence: MEDIUM** - Well-documented pattern but requires careful implementation

#### Technique Details

Implement a **token queue + indentation stack** hybrid lexer following Python's algorithm ([Python 3.14 Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html)):

1. **Lexer State Management**
   - Maintain mutable state outside lexer: `indentStack: int list` (initialized `[0]`)
   - Maintain token queue: `tokenQueue: Queue<token>` for buffering DEDENT tokens
   - Track whether we're at line beginning: `atLineStart: bool ref`

2. **Token Generation Algorithm**
   ```fsharp
   (* In fslex action code *)
   rule token = parse
   | '\n' { atLineStart := true; NEWLINE }
   | [' ' '\t']+ as ws when !atLineStart ->
       {
         let spaces = countSpaces ws  (* tabs = 8 spaces *)
         let topIndent = List.head !indentStack
         atLineStart := false;

         if spaces > topIndent then
           (* INDENT case *)
           indentStack := spaces :: !indentStack;
           INDENT
         elif spaces < topIndent then
           (* DEDENT case - may emit multiple tokens *)
           let rec popAndQueue acc stack =
             match stack with
             | top :: rest when top > spaces ->
                 tokenQueue.Enqueue DEDENT;
                 popAndQueue (acc + 1) rest
             | top :: _ when top = spaces ->
                 indentStack := stack;
                 if acc = 0 then DEDENT (* first one returned *)
                 else tokenQueue.Dequeue()
             | _ -> failwith "Indentation error"
           popAndQueue 0 !indentStack
         else
           (* Equal indent - consume whitespace *)
           token lexbuf
       }
   ```

3. **Wrapper Function for Multiple DEDENTs**
   ```fsharp
   let lexerWrapper lexbuf =
     if not (tokenQueue.IsEmpty) then
       tokenQueue.Dequeue()
     else
       Lexer.token lexbuf
   ```

#### Why This Works

- **Queue enables multiple DEDENT emission**: When dedenting multiple levels (e.g., from 12 spaces to 0), generate all required DEDENT tokens at once, queue them, return first immediately ([Marcel Goh - Scanning Spaces](https://marcelgoh.ca/2019/04/14/scanning-spaces.html))
- **Stack tracks valid dedent targets**: Ensures dedent only to previous indentation levels, catches `IndentationError`
- **fslex allows mutable state**: Actions are "arbitrary F# expressions" with access to external state ([FsLex docs](https://github.com/fsprojects/FsLexYacc/blob/master/docs/content/fslex.md))

#### Grammar Impact

fsyacc grammar becomes simpler - use INDENT/DEDENT as block delimiters:

```fsharp
(* In .fsy file *)
Block:
  | INDENT StmtList DEDENT { $2 }

IfExpr:
  | IF Expr COLON NEWLINE Block { IfExpr($2, $5, None) }
  | IF Expr COLON NEWLINE Block ELSE COLON NEWLINE Block
      { IfExpr($2, $5, Some $9) }
```

#### Pitfalls

- **Tabs vs Spaces**: Must decide on tab=8 spaces (Python style) or reject tabs entirely (Python 3+ warning). **Recommendation**: Reject tabs, spaces only.
- **EOF handling**: Must emit DEDENT for all remaining stack levels at EOF
- **Blank lines**: Ignore entirely - don't change indentation state for lines with only whitespace
- **First line indentation**: Error if first line is indented (unless in REPL continuation mode)

#### Alternative Considered: Offside Rule Parser Extensions

Michael D. Adams' offside rule grammar extensions ([Layout Parsing paper](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf)) extend CFGs with layout annotations. **Why Not**: Requires modifying fsyacc itself, not just grammar. Too much infrastructure work for single language. Token-based approach is standard and proven.

---

## Feature 2: Algebraic Data Types with GADT Support

### Recommended Approach: Grammar Extension + Type Checker Enhancement

**Confidence: HIGH** - Grammar is straightforward, type checking builds on existing bidirectional foundation

#### Grammar Extension (fsyacc)

Add production rules for ADT declarations:

```fsharp
(* .fsy syntax *)
TypeDecl:
  | TYPE TypeParams ID EQUALS ConstructorList
      { TypeDecl($2, $3, $5) }

ConstructorList:
  | Constructor { [$1] }
  | Constructor BAR ConstructorList { $1 :: $3 }

Constructor:
  (* Simple ADT: Option = Some of 'a | None *)
  | ID OF Type { SimpleConstructor($1, $3) }
  | ID { NullaryConstructor($1) }

  (* GADT: return type specified *)
  | ID COLON Type { GADTConstructor($1, $3) }
```

#### Type System Extension

**Key Insight**: FunLang already has bidirectional type checking - leverage this for GADT ([How to Choose Between Hindley-Milner and Bidirectional Typing](https://thunderseethe.dev/posts/how-to-choose-between-hm-and-bidir/))

1. **Simple ADT (Standard Hindley-Milner)**
   ```fsharp
   type Option<'a> =
     | Some of 'a
     | None
   ```
   - Constructor types: `Some : 'a -> Option<'a>`, `None : Option<'a>`
   - Pattern matching introduces type equality constraint
   - Standard unification handles this

2. **GADT (Requires Bidirectional)**
   ```fsharp
   type Expr<'t> =
     | IntLit : int -> Expr<int>
     | BoolLit : bool -> Expr<bool>
     | If : Expr<bool> * Expr<'a> * Expr<'a> -> Expr<'a>
   ```
   - Return types are **specified**, not inferred
   - Pattern matching refines type parameters based on constructor
   - Need type equality propagation in environment

#### Implementation Strategy

1. **AST Representation**
   ```fsharp
   type TypeConstructor =
     | SimpleCons of name: string * argType: Type
     | GADTCons of name: string * fullType: Type

   type TypeDecl = {
     Name: string
     TypeParams: string list
     Constructors: TypeConstructor list
     IsGADT: bool  (* true if any constructor has explicit return type *)
   }
   ```

2. **Type Environment Extension**
   ```fsharp
   (* Add to type environment *)
   type TyEnv = {
     (* existing fields *)
     TypeConstructors: Map<string, TypeDecl>
     DataConstructors: Map<string, ConstructorType>
   }

   type ConstructorType =
     | SimpleADT of Type -> Type
     | GADT of Type  (* full type with equality constraints *)
   ```

3. **Pattern Matching with GADTs**

   When checking `match expr with | IntLit n -> ...`:
   - **Check mode**: `expr : Expr<'t>` for some unknown `'t`
   - **Synthesis mode**: Pattern binding introduces `'t = int` constraint
   - Propagate constraint into branch body type checking

   **Implementation**: Extend existing bidirectional checking with local type equations:
   ```fsharp
   let checkPattern env pattern expectedType =
     match pattern with
     | ConsPattern(consName, subPatterns) ->
         let consType = lookupConstructor env consName
         match consType with
         | GADT fullType ->
             (* Unify expectedType with constructor's return type *)
             let returnType = extractReturnType fullType
             let tyEqs = unify expectedType returnType
             (* Add type equations to environment for branch *)
             let env' = addTypeEquations env tyEqs
             (env', subPatterns)
   ```

#### Why This Works

- **GADT type checking is bidirectional**: "Work on GADTs uses unification to propagate equality information" ([Bidirectional Typing paper](https://arxiv.org/pdf/1908.05839))
- **FunLang already has bidirectional**: Extending it is incremental, not revolutionary
- **No HM principal types**: GADTs break HM principality anyway - bidirectional is the modern approach ([Omnidirectional Type Inference](https://inria.hal.science/hal-05438544v1/document))

#### Pitfalls

- **GADT requires annotations**: Cannot infer return types. Must require explicit `COLON Type` in grammar.
- **Type equation scope**: Constraints from pattern matching are **local** to match branch. Don't leak.
- **Recursive types**: Handle occurs-check carefully with GADTs (same as existing FunLang)

#### F# Native Workaround Note

F# itself doesn't support GADTs due to .NET CLR limitations ([F# GADT Issue #179](https://github.com/fsharp/fslang-suggestions/issues/179)). LangThree can implement them because:
1. We're implementing a language, not extending F# runtime
2. GADTs are in our AST and type checker, not in F#/.NET
3. F#'s algebraic types handle our AST representation fine

---

## Feature 3: Records

### Recommended Approach: Simple Grammar Extension

**Confidence: HIGH** - Straightforward syntax, standard type checking

#### Grammar (fsyacc)

```fsharp
(* Record type declaration *)
TypeDecl:
  | TYPE ID EQUALS RecordType { RecordTypeDecl($2, $4) }

RecordType:
  | LBRACE FieldDeclList RBRACE { RecordType($2) }

FieldDeclList:
  | FieldDecl { [$1] }
  | FieldDecl SEMICOLON FieldDeclList { $1 :: $3 }

FieldDecl:
  | ID COLON Type { FieldDecl($1, $3) }

(* Record expressions *)
Expr:
  | LBRACE FieldExprList RBRACE { RecordExpr($2) }
  | Expr DOT ID { FieldAccess($1, $3) }
  | LBRACE Expr WITH FieldExprList RBRACE { RecordUpdate($2, $4) }

FieldExprList:
  | FieldExpr { [$1] }
  | FieldExpr SEMICOLON FieldExprList { $1 :: $3 }

FieldExpr:
  | ID EQUALS Expr { ($1, $3) }
```

#### Type Checking

Records are **structural types** in F# style:

```fsharp
type RecordType = {
  Name: string
  Fields: Map<string, Type>
}

(* Type checking record construction *)
let checkRecordExpr env fields =
  (* Check all field expressions *)
  let fieldTypes =
    fields |> List.map (fun (name, expr) ->
      name, inferType env expr)
    |> Map.ofList

  (* Construct record type *)
  RecordType { Name = freshName(); Fields = fieldTypes }
```

#### Pattern Matching on Records

```fsharp
Pattern:
  | LBRACE FieldPatternList RBRACE { RecordPattern($2) }

FieldPatternList:
  | FieldPattern { [$1] }
  | FieldPattern SEMICOLON FieldPatternList { $1 :: $3 }

FieldPattern:
  | ID EQUALS Pattern { ($1, $3) }
```

#### Why This Works

- **Records are just product types**: Like tuples but with named fields
- **Structural typing**: `{ x: int; y: int }` is same type anywhere declared
- **Field access is projection**: Type checker looks up field in record type

#### Pitfalls

- **Field order**: Decide if `{ x: int; y: bool }` ≡ `{ y: bool; x: int }`. **Recommendation**: Yes (structural), but warn on different order.
- **Duplicate fields**: Parser must reject `{ x: int; x: bool }`
- **Record update syntax**: `{ r with x = 5 }` requires original record type inference

---

## Feature 4: Module System

### Recommended Approach: Namespace Resolution Layer

**Confidence: MEDIUM-HIGH** - Grammar is simple, implementation requires environment refactoring

#### Grammar (fsyacc)

```fsharp
(* Module declarations *)
Program:
  | ModuleDecl Program { ModuleDecl($1) :: $2 }
  | TopLevelDecl Program { TopLevelDecl($1) :: $2 }
  | EOF { [] }

ModuleDecl:
  | MODULE ModulePath EQUALS INDENT DeclList DEDENT
      { Module($2, $5) }

ModulePath:
  | ID { [|$1|] }
  | ID DOT ModulePath { $1 :: $3 }

(* Module expressions *)
Expr:
  | ModulePath { ModuleAccess($1) }
  | OPEN ModulePath IN Expr { OpenModule($2, $4) }
```

#### Implementation Strategy

1. **Module Environment**
   ```fsharp
   type ModulePath = string list

   type Module = {
     Path: ModulePath
     Types: Map<string, TypeDecl>
     Values: Map<string, Type>
     Submodules: Map<string, Module>
   }

   type TyEnv = {
     CurrentModule: ModulePath
     RootModule: Module
     OpenModules: ModulePath list  (* for name resolution *)
   }
   ```

2. **Name Resolution**
   ```fsharp
   let rec resolveQualifiedName env (path: string list) =
     match path with
     | [] -> None
     | [name] ->
         (* Try current module, then open modules *)
         tryResolveSimpleName env name
     | moduleName :: rest ->
         (* Resolve module, then name in that module *)
         let module' = findModule env.RootModule moduleName
         resolveInModule module' rest
   ```

3. **Module Compilation**
   ```fsharp
   let checkModule env (Module(path, decls)) =
     (* Enter module scope *)
     let env' = { env with CurrentModule = path }

     (* Check all declarations in module *)
     let checkedDecls = decls |> List.map (checkDecl env')

     (* Add module to environment *)
     addModuleToEnv env path checkedDecls
   ```

#### Why F# Style (Not OCaml Functors)

- **Simpler**: Modules are just namespaces, not first-class values
- **Practical**: PROJECT.md explicitly excludes functors for complexity reasons
- **Sufficient**: Can express most modular code without functor parameterization

Reference: [F# Modules vs Namespaces](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)

#### Pitfalls

- **Circular dependencies**: Modules can't circularly reference each other. Detect at compile time.
- **Name shadowing**: Decide priority - qualified names vs open'd modules vs local definitions
- **Module signatures**: Start without signatures (all exports public), add later if needed

---

## Feature 5: Exceptions

### Recommended Approach: Grammar + AST Extension, No Type System Changes

**Confidence: HIGH** - Exceptions don't affect type inference

#### Grammar (fsyacc)

```fsharp
(* Exception declaration *)
Decl:
  | EXCEPTION ID OF Type { ExceptionDecl($2, Some $4) }
  | EXCEPTION ID { ExceptionDecl($2, None) }

(* Exception expressions *)
Expr:
  | RAISE Expr { Raise($2) }
  | TRY Expr WITH MatchCases { TryWith($2, $4) }
  | TRY Expr FINALLY Expr { TryFinally($2, $4) }

(* Cannot combine try-with-finally in single expression - must nest *)
```

#### Type Checking

**Key Insight**: Exceptions are **untyped** in type system (like F#):

```fsharp
(* Type checking rules *)
let inferType env expr =
  match expr with
  | Raise exnExpr ->
      (* exnExpr must type as exception *)
      let exnType = inferType env exnExpr
      checkIsException env exnType;
      (* Raise has any type - represents non-returning *)
      freshTypeVar()

  | TryWith(body, cases) ->
      let bodyType = inferType env body
      (* Each case must return same type as body *)
      cases |> List.iter (fun (pat, handler) ->
        checkPattern env pat ExceptionType;
        let handlerType = inferType env handler
        unify bodyType handlerType)
      bodyType

  | TryFinally(body, finalizer) ->
      let bodyType = inferType env body
      let finalizerType = inferType env finalizer
      (* finalizer return ignored - must be unit for effects *)
      unify finalizerType UnitType;
      bodyType
```

#### Runtime Representation

Since LangThree is an interpreter:

```fsharp
(* Evaluation with exceptions *)
type EvalResult<'a> =
  | Value of 'a
  | Exception of exnValue: Value * trace: string list

let rec eval env expr =
  match expr with
  | Raise exnExpr ->
      let exnVal = eval env exnExpr
      Exception(exnVal, [currentLocation()])

  | TryWith(body, cases) ->
      match eval env body with
      | Value v -> Value v
      | Exception(exn, trace) ->
          (* Try to match exception against cases *)
          match findMatchingCase cases exn with
          | Some handler -> eval env handler
          | None -> Exception(exn, currentLocation() :: trace)
```

#### Why This Works

- **Exceptions bypass type system**: Like F#, exception raises have polymorphic type `'a` ([F# Exceptions](https://fsharpforfunandprofit.com/posts/exceptions/))
- **No effect types needed**: Not tracking exceptions in types (save for advanced future)
- **Match exhaustiveness**: Same algorithm as ADT pattern matching

#### Pitfalls

- **No exception types**: Unlike checked exceptions (Java), F# exceptions are unchecked
- **Match order matters**: First matching case handles exception, not most specific
- **Finally guarantees**: Must run finally block even if exception occurs (use F# `try...finally` semantics)

---

## Implementation Order (Dependencies)

Based on complexity and dependencies:

1. **Records** (Easiest, no dependencies)
   - Grammar extension
   - Simple structural type checking
   - Test: `let r = { x = 1; y = true } in r.x`

2. **ADT without GADT** (Builds on records)
   - Grammar for simple ADT
   - Constructor type checking
   - Pattern matching
   - Test: `type Option = Some of int | None`

3. **Exceptions** (Independent)
   - Grammar extension
   - Runtime evaluation changes
   - Test: `raise (Exception "test")`

4. **Modules** (Requires stable declarations)
   - After ADT and Records declared
   - Environment refactoring
   - Test: `module M = let x = 1` then `M.x`

5. **Indentation Syntax** (Most complex, can parallelize)
   - Independent of type system
   - Lexer changes only
   - Test all previous features with indentation

6. **GADT** (Last - requires ADT + bidirectional)
   - Extends ADT grammar
   - Extends type checker with constraints
   - Test: `type Expr = IntLit : int -> Expr<int>`

---

## Testing Strategy

For each feature:

### Unit Tests
- **Lexer**: Test token generation (especially INDENT/DEDENT)
- **Parser**: Test grammar accepts/rejects appropriate syntax
- **Type Checker**: Test type inference and unification

### Integration Tests
- **Feature combinations**: Records in modules, GADTs with exceptions, etc.
- **Error messages**: Indentation errors, type errors, module not found

### Property-Based Tests
- **Indentation invariants**: Stack never negative, EOF DEDENTs balance
- **Type soundness**: Well-typed programs don't get stuck

---

## Key References (with Confidence Levels)

### High Confidence (Authoritative Sources)
- [Python 3.14 Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html) - INDENT/DEDENT algorithm (official spec)
- [FsLexYacc GitHub](https://github.com/fsprojects/FsLexYacc) - fslex/fsyacc capabilities
- [F# Modules Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules) - Module semantics
- [F# Discriminated Unions](https://fsharpforfunandprofit.com/posts/discriminated-unions/) - ADT patterns

### Medium-High Confidence (Academic/Research)
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) - GADT type checking approach
- [Layout Parsing](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf) - Offside rule theory
- [Marcel Goh - Scanning Spaces](https://marcelgoh.ca/2019/04/14/scanning-spaces.html) - Practical lexer implementation

### Medium Confidence (Community/Blog Posts)
- [thanos.codes FsLexYacc tutorial](https://thanos.codes/blog/using-fslexyacc-the-fsharp-lexer-and-parser/) - Practical examples
- [Thunderseethe - HM vs Bidirectional](https://thunderseethe.dev/posts/how-to-choose-between-hm-and-bidir/) - Type system tradeoffs

---

## Open Questions / Research Needed During Implementation

1. **Indentation + Modules**: How do indentation rules interact with module nesting? Test Python's approach.

2. **GADT Type Inference**: FunLang's existing bidirectional checking - how much needs extension? Prototype small GADT first.

3. **Record Type Inference**: Should records be nominal (named) or structural (shape-based)? PROJECT.md doesn't specify. **Recommendation**: Start structural (simpler), can add nominal later.

4. **Exception Hierarchies**: Support exception subtyping? F# does, but adds complexity. **Recommendation**: Start flat, no hierarchy.

5. **Module Mutual Recursion**: Do we need `module rec`? PROJECT.md doesn't mention. **Recommendation**: No, too complex for first version.

---

## Alternatives Considered

| Feature | Considered | Rejected Why |
|---------|-----------|--------------|
| Indentation | Offside rule grammar extension | Requires modifying fsyacc core |
| Indentation | Preprocessor (indent to braces) | Loses position info for error messages |
| GADT | Full HM with constraint solving | Breaks principality, bidirectional is cleaner |
| GADT | Skip entirely, only simple ADT | PROJECT.md explicitly requires GADT |
| Records | Nominal typing | More complex, structural is F# style |
| Modules | OCaml functors | PROJECT.md excludes, too complex |
| Exceptions | Checked exceptions (Java style) | Not F# style, adds type system complexity |

---

## Success Criteria

Each feature complete when:

- [ ] Grammar accepts valid syntax, rejects invalid
- [ ] Type checker infers types correctly (or checks annotations)
- [ ] Interpreter evaluates semantics correctly
- [ ] Error messages are clear (especially indentation errors)
- [ ] Integration tests pass with other features
- [ ] Performance is acceptable (not 10x slower than FunLang base)

---

**Next Steps**:
1. Read this STACK.md during roadmap creation
2. Create phases in recommended order (Records → ADT → Exceptions → Modules → Indentation → GADT)
3. Use `/gsd:research-phase` for deeper investigation during each phase
4. Prototype risky parts early (indentation lexer, GADT type checking)
