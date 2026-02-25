# Feature Landscape: LangThree Language Capabilities

**Project:** LangThree - Practical ML-style Language
**Base:** FunLang v6.0 with F# syntax
**Researched:** 2026-02-25

## Overview

This document catalogs the specific features needed for six core language capabilities in LangThree. Each capability is broken down into table stakes (must-have), differentiators (nice-to-have), and anti-features (deliberately excluded in v1).

---

## 1. Indentation-Based Syntax

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Offside rule for blocks** | Core F# syntax principle - eliminates `begin/end` | **High** | Lexer token preprocessing | Layout algorithm inserts virtual tokens |
| **Let-bindings indentation** | Most common construct | Medium | Offside rule | `let x = expr` continuation on next line |
| **Function application** | Multi-line function calls | Medium | Offside rule | Arguments aligned or indented |
| **Match expressions** | Pattern clauses alignment | Medium | Offside rule | Each `\|` clause at same level |
| **Module-level declarations** | Top-level definitions | Low | Offside rule | No mandatory indentation at file top-level |
| **Spaces-only enforcement** | Prevent tabs/spaces mixing | Low | Lexer | Reject tabs outside strings/comments |
| **4-space indentation standard** | F# convention | Low | None | Recommended, not enforced |

**Implementation Notes:**
- Requires lexer preprocessing to insert `INDENT`/`DEDENT`/`NEWLINE` tokens
- fslex/fsyacc: Use custom lexer state to track indentation stack
- Error messages must reference logical structure, not virtual tokens

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Configurable indent width | Flexibility for users | Low | Post-v1 — stick to 4-space standard initially |
| Smart error recovery | Better error messages | Medium | "Expected indentation at column N" |
| Flexible if-then-else | Single-line vs multi-line | Medium | OCaml allows both, F# prefers indentation |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Tabs support | Source of bugs, inconsistent rendering | Reject with clear error |
| Mixed indent styles | Causes subtle bugs | Enforce spaces-only |
| Optional braces syntax | Adds parser complexity | Pure indentation only |
| Significant blank lines | Python-style double-newline | Ignore blank line count |

### Feature Dependencies

```
Lexer preprocessing → Offside rule implementation → All indentation features
                   ↓
              Error messages (reference logical structure)
```

### Critical Pitfalls

**Tab vs Space Mixing:** Most common source of frustration in indentation-based languages. Python's `TabError` warns about this. **Solution:** Fail fast at lexer level with clear error.

**Invisible bugs:** One TAB press can change program semantics without syntax error (code remains grammatically correct). **Solution:** Strict spaces-only mode prevents this entirely.

**Copy-paste errors:** Copying code between editors with different tab settings. **Solution:** Documentation emphasizes spaces-only, provide linter tool.

### Constructs Requiring Indentation Rules

1. **Let bindings** - Right-hand side continuation
2. **Match expressions** - Pattern clause alignment
3. **Function definitions** - Parameter and body indentation
4. **If-then-else** - Branch alignment
5. **Type definitions** - Constructor/field alignment
6. **Module definitions** - Nested module indentation

---

## 2. Algebraic Data Types (ADT)

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Sum types (variants)** | Core ADT feature | Medium | Type checker | `type Option = Some of 'a \| None` |
| **Product types (tuples)** | Already in FunLang | Low | Existing | `(int * string)` |
| **Constructor syntax** | Pattern matching | Medium | Parser | `Some 42` creates value |
| **Pattern matching** | Already in FunLang v6.0 | Low | Existing | Extend for ADT constructors |
| **Exhaustiveness checking** | Type safety guarantee | High | Type checker | Warn on missing patterns |
| **Type parameters** | Polymorphic types | Medium | HM inference | `type 'a list = Nil \| Cons of 'a * 'a list` |
| **Recursive types** | Lists, trees | Medium | Type checker | Self-referencing definitions |
| **Mutually recursive types** | Type dependencies | Medium | Type checker | `type ... and ...` syntax |

**Implementation Notes:**
- Extends existing FunLang pattern matching
- Constructor arity checking at compile time
- Pattern exhaustiveness: Use decision tree algorithm
- ML-style syntax preferred over F# verbose syntax

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Redundancy checking | Warn about unreachable patterns | Medium | Dead code detection |
| Named constructors | Better documentation | Low | `type Color = Red \| Green \| Blue` |
| Constraint syntax | Refined types | High | Defer to GADT discussion |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Anonymous sum types | No established syntax in ML family | Use named types |
| Extensible variants | Complex, OCaml-specific | Closed world only |
| Default constructors | Ambiguous semantics | All cases explicit |
| Structural typing for ADT | F# uses nominal | Nominal only |

### Feature Dependencies

```
Type inference (HM) → Type parameters → Polymorphic ADT
                   ↓
Pattern matching → Exhaustiveness checking
                ↓
             Type checker error reporting
```

### Minimal Complete Feature Set

For MVP ADT:
1. Sum types with multiple constructors
2. Product types (tuples) - already have
3. Pattern matching - extend existing
4. Exhaustiveness warnings
5. Type parameters
6. Recursive type definitions

Defer to post-MVP:
- Redundancy checking (nice warning, not critical)
- Complex constraint systems (covered by GADT)

---

## 3. Generalized Algebraic Data Types (GADT)

### What GADTs Add Beyond Basic ADT

**Core difference:** GADT constructors can return **different type instantiations** of the same type constructor, enabling type refinement during pattern matching.

Example:
```fsharp
(* Basic ADT - all constructors return 'a expr *)
type 'a expr =
  | Int of int
  | Bool of bool

(* GADT - constructors return specific instantiations *)
type _ expr =
  | Int : int -> int expr          (* returns int expr *)
  | Bool : bool -> bool expr       (* returns bool expr *)
  | Add : int expr * int expr -> int expr
  | If : bool expr * 'a expr * 'a expr -> 'a expr
```

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Explicit constructor return types** | Core GADT feature | High | Type checker | `Int : int -> int expr` |
| **Type refinement in pattern matching** | Type-safe evaluation | High | Bidirectional checking | Compiler learns type info when descending into match |
| **Indexed type families** | Typed DSLs | High | Type checker | `'a expr` where `'a` determined by constructor |
| **Existential types in constructors** | Hide internal types | Medium | Type checker | `MkBox : 'a -> box` hides `'a` |
| **Local constraints** | Context-specific equalities | High | Constraint solver | Type equations within scope |

**Implementation Notes:**
- FunLang v6.0 has bidirectional type checking — good foundation
- Requires extending unification with local type equations
- Type refinement applied lazily during pattern matching
- Need rigid/wobbly flag for type variables

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Type equality witnesses | Advanced type proofs | Very High | Research feature |
| GADT-based DSL syntax sugar | Ergonomics | Medium | Post-v1 |
| Phantom types | Zero-cost abstractions | Low | Falls out naturally |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Unrestricted existentials | Type inference undecidable | Require explicit annotations |
| Full dependent types | Out of scope | Indexed types only |
| Type-level computation | Too complex for v1 | Fixed indices only |
| Automatic GADT inference | Undecidable in general | Require GADT syntax markers |

### Feature Dependencies

```
Bidirectional type checking (✓ FunLang v6.0 has this)
         ↓
Type refinement during pattern match
         ↓
Explicit constructor signatures → GADT syntax → Type-indexed evaluation
         ↓
Local constraint solving
```

### Minimal Complete Feature Set

For MVP GADT:
1. Explicit constructor type signatures
2. Type refinement in pattern matching (extend existing bidirectional checking)
3. Basic existential quantification
4. Simple indexed types (no computation)

Defer to post-MVP:
- Complex constraint solving
- Type equality witnesses
- Advanced phantom type patterns

### Critical Design Choice

**F# vs OCaml vs Haskell GADT syntax:**

```fsharp
(* OCaml 4.00+ style - RECOMMENDED *)
type _ expr =
  | Int : int -> int expr
  | Add : int expr * int expr -> int expr

(* Haskell style - more verbose *)
data Expr a where
  Int :: Int -> Expr Int
  Add :: Expr Int -> Expr Int -> Expr Int

(* F# - no native GADT support, uses witness types *)
```

**Recommendation:** OCaml-style syntax — cleaner, well-established, easier to parse.

---

## 4. Records

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Named field syntax** | Record definition | Low | Parser | `type Point = { x: float; y: float }` |
| **Record expressions** | Value creation | Low | Type inference | `{ x = 1.0; y = 2.0 }` |
| **Field access (dot notation)** | Standard syntax | Low | Type checker | `point.x` |
| **Copy-and-update syntax** | Immutable updates | Medium | Type checker | `{ point with y = 3.0 }` |
| **Structural equality** | F# semantics | Medium | Codegen | Auto-generated `=` operator |
| **Nominal typing** | Type safety | Low | Type checker | Same structure ≠ same type |
| **Pattern matching** | Deconstruction | Medium | Existing PM | `match p with { x = 0; y = _ } -> ...` |
| **Type inference from labels** | Ergonomics | Medium | Type checker | Infer type from field names |

**Implementation Notes:**
- Records are **nominal** (named types), not structural
- Fields auto-exposed as accessors
- Immutable by default, mutable fields optional with `mutable` keyword
- Copy-and-update creates new record, doesn't mutate

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Mutable fields | Limited mutability | Low | `mutable odometer: int` |
| Anonymous records | Lightweight data | Medium | F# has `{| x = 1 |}` — defer to post-v1 |
| Record members | Methods on records | Medium | F# allows this — defer to post-v1 |
| Nested update syntax | Deep updates | Medium | Ergonomic but complex |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Structural record typing | Contradicts ML semantics | Use nominal only |
| DefaultValue attribute | Error-prone | Explicit default values |
| Optional fields | Complicates type system | Use Option type |
| Record inheritance | Not in F# or OCaml | Use composition |

### Feature Dependencies

```
Type inference → Label-based type resolution
              ↓
         Record expressions → Copy-and-update syntax
                           ↓
                    Pattern matching on records
```

### Structural vs Nominal Typing

**LangThree choice: NOMINAL**

```fsharp
(* These are DIFFERENT types even with same structure *)
type Point = { x: float; y: float }
type Vector = { x: float; y: float }

let p: Point = { x = 1.0; y = 2.0 }
let v: Vector = p  (* TYPE ERROR - incompatible types *)
```

**Why nominal:**
- Type safety: prevents accidental mixing
- F# semantics (our syntax model)
- Better error messages
- Aligns with ML family

**Equality:** Structural equality (values compared), but types must match first.

### Minimal Complete Feature Set

For MVP Records:
1. Type declarations with named fields
2. Record expressions `{ label = value }`
3. Field access `record.field`
4. Copy-and-update `{ record with field = value }`
5. Pattern matching on records
6. Structural equality (within same type)

Defer to post-MVP:
- Mutable fields (add when needed)
- Record members (OOP features later)
- Anonymous records (syntactic sugar)

---

## 5. F# Style Modules

### What "F# Style" Means

- **Namespace declarations** - logical organization, no code
- **Module declarations** - contain values, types, functions
- **No functors** - unlike OCaml, F# avoids parameterized modules
- **Simple scoping** - modules are static, not first-class

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Top-level module declaration** | File organization | Low | Parser | `module MyModule` |
| **Namespace declarations** | Hierarchical naming | Low | Parser | `namespace Company.Project` |
| **Module = static class** | Simple semantics | Low | Codegen | No runtime overhead |
| **Nested modules** | Sub-organization | Medium | Parser | Indentation-based |
| **`open` keyword** | Import declarations | Low | Scope resolution | Unqualified names |
| **Qualified names** | Explicit references | Low | Name resolution | `Module.function` |
| **Module-level let bindings** | Top-level definitions | Low | Existing | Values in module scope |
| **Implicit module from filename** | Convenience | Low | Parser | `program.fs` → `module Program` |
| **Recursive modules** | Mutual recursion | Medium | Type checker | `module rec M = ...` |

**Implementation Notes:**
- Module = namespace + static container
- No runtime module values (not first-class)
- Simple name resolution: qualified or open
- Indentation determines nesting

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `open type` for static members | F# 5.0+ feature | Medium | Sugar for member access |
| Module aliases | Convenience | Low | `module M = Long.Path.Module` |
| Module signatures | Interface specs | High | Separate `.fsi` files — defer |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Functors (parameterized modules) | OCaml complexity | Simple modules only |
| First-class modules | Runtime overhead | Static modules |
| Include directive | Increases complexity | Use `open` |
| Module shadowing | Confusing | Disallow redefinition |

### Feature Dependencies

```
Namespace declarations
        ↓
Top-level modules → Nested modules → Module nesting via indentation
                 ↓
            open declarations → Name resolution
                             ↓
                  Qualified names (Module.value)
```

### Namespace vs Module

| Aspect | Namespace | Module |
|--------|-----------|--------|
| Contains | Modules, types | Values, functions, types |
| Code | No | Yes |
| Nesting | Dot notation | Indentation |
| Syntax | `namespace X.Y` | `module M = ...` |

**Key difference:** Namespaces cannot directly contain values/functions — must wrap in module.

### Module Scoping Rules

1. **Top-level module:** No indentation required for declarations
2. **Nested module:** Must indent declarations under module
3. **Sibling modules:** Same indentation level
4. **Open scope:** From `open` to end of file/module
5. **Shadowing:** Later `open` can shadow earlier names (warn on conflict)

### Minimal Complete Feature Set

For MVP Modules:
1. Top-level module declarations
2. Namespace declarations
3. Nested modules (indentation-based)
4. `open` keyword
5. Qualified name access
6. Module-level let bindings
7. Implicit module from filename

Defer to post-MVP:
- Module signatures (`.fsi` files)
- `open type` syntax
- Module aliases (convenient but not critical)
- Recursive modules (add when needed)

---

## 6. Exceptions

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| **Exception declaration** | Custom exception types | Low | Type system | `exception MyError of string` |
| **`raise` function** | Throw exceptions | Low | Runtime | `raise (MyError "fail")` |
| **`try...with` expression** | Catch exceptions | Medium | Pattern matching | Pattern match on exception type |
| **Pattern matching in `with`** | Multiple handlers | Medium | Existing PM | `with \| Error1 -> ... \| Error2 -> ...` |
| **`when` guards** | Conditional catch | Low | Existing guards | `with \| e when condition -> ...` |
| **.NET exception interop** | F# on .NET | Medium | Runtime | Match on `System.Exception` |
| **Exception as values** | First-class exceptions | Low | Type system | Exception constructors are values |

**Implementation Notes:**
- Exceptions are special variant types
- `raise` has type `'a -> 'b` (never returns)
- `try...with` is an expression (has type)
- Pattern matching uses existing PM infrastructure
- `:?` operator for .NET exception type tests (if targeting .NET)

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `try...finally` | Resource cleanup | Medium | Defer to v2 — use explicit cleanup |
| `failwith` / `failwithf` | Convenience functions | Low | Sugar over `raise` |
| Exception payload | Structured error info | Low | Already supported via `of` |
| Reraising exceptions | Preserve stack trace | Medium | `reraise()` function |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Checked exceptions | Java-style, rejected by ML community | Dynamic exceptions |
| Exception specifications | Not in F#/OCaml | Document in comments |
| Automatic exception conversion | Error-prone | Explicit handling |
| Exception hierarchies | Complexity | Flat exception types |

### Feature Dependencies

```
ADT system → Exception declarations
          ↓
Pattern matching → try...with handlers
                ↓
       Type system (exn type)
```

### Exception Declaration Syntax

```fsharp
(* Basic exception *)
exception NotFound

(* Exception with payload *)
exception InvalidInput of string

(* Exception with multiple fields *)
exception ParseError of line: int * column: int * message: string
```

### Try-With-Expression Semantics

```fsharp
(* Expression form - has a type *)
let result =
  try
    riskyOperation()
  with
  | NotFound -> 0
  | InvalidInput msg ->
      printfn "Error: %s" msg
      -1

(* Pattern matching on exception type *)
try
  ...
with
| :? System.ArgumentException as e -> handleArg e
| :? System.InvalidOperationException -> handleInvalid()
| e -> reraise()  (* catch-all with reraise *)
```

**Key property:** `try...with` is an **expression**, not a statement. All branches must return same type.

### Minimal Complete Feature Set

For MVP Exceptions:
1. Exception declarations (`exception E of T`)
2. `raise` function
3. `try...with` expressions
4. Pattern matching on exception types
5. `when` guards in exception handlers

Defer to post-MVP:
- `try...finally` (resource management)
- `failwith` convenience functions (trivial sugar)
- Reraise functionality
- Stack trace utilities

---

## Cross-Cutting Concerns

### Feature Interaction Matrix

| Feature | Depends On | Enables | Conflicts With |
|---------|-----------|---------|----------------|
| Indentation syntax | Lexer preprocessing | All language constructs | Mixed tabs/spaces |
| ADT | Type inference, Pattern matching | GADT, Records | None |
| GADT | ADT, Bidirectional checking | Type-safe DSLs | None |
| Records | Type inference | Pattern matching | None |
| Modules | Indentation syntax | Namespace organization | None |
| Exceptions | ADT, Pattern matching | Error handling | None |

### Implementation Order Recommendation

**Phase 1: Foundation**
1. Indentation syntax (affects all parsing)
2. Basic ADT (extends FunLang pattern matching)

**Phase 2: Type System**
3. Records (uses type inference)
4. GADT (extends ADT + bidirectional checking)

**Phase 3: Organization**
5. Modules (uses indentation + name resolution)
6. Exceptions (uses ADT + pattern matching)

**Rationale:**
- Indentation affects parser for everything else
- ADT/Records build on existing type system
- GADT requires ADT foundation
- Modules independent but needs indentation rules
- Exceptions are simplest, uses existing infrastructure

### Shared Infrastructure

**Parser:**
- Indentation token preprocessing (used by: all features)
- Pattern matching syntax (used by: ADT, GADT, Records, Exceptions)

**Type Checker:**
- Hindley-Milner inference (used by: ADT, Records, Modules)
- Bidirectional checking (used by: GADT, existing)
- Exhaustiveness checker (used by: ADT, GADT, Exceptions)

**Runtime:**
- Exception mechanism (used by: Exceptions)
- Equality primitives (used by: Records, ADT)

---

## Complexity Assessment

| Capability | Overall Complexity | Reason |
|-----------|-------------------|--------|
| Indentation syntax | **High** | Requires lexer preprocessing, affects all parsing |
| ADT | **Medium** | Extends existing pattern matching, adds exhaustiveness |
| GADT | **High** | Requires bidirectional checking extensions, type refinement |
| Records | **Medium** | Type inference + copy-update, but straightforward |
| Modules | **Low-Medium** | Simple semantics (no functors), indentation nesting |
| Exceptions | **Low** | Uses existing ADT + pattern matching infrastructure |

### Risk Factors

**High Risk:**
- Indentation syntax: Many subtle edge cases, error messages
- GADT: Type system extension, potential inference issues

**Medium Risk:**
- Records: Copy-update with nested types can be tricky
- ADT: Exhaustiveness checking algorithm

**Low Risk:**
- Modules: Simple static semantics
- Exceptions: Straightforward extension

---

## Quality Gates

### Feature Completeness Checklist

**Indentation Syntax:**
- [ ] Let-binding indentation works
- [ ] Match expression alignment works
- [ ] Nested modules indent correctly
- [ ] Tab characters rejected
- [ ] Clear error messages for indentation errors

**ADT:**
- [ ] Sum types with multiple constructors
- [ ] Pattern matching on constructors
- [ ] Exhaustiveness warnings
- [ ] Type parameters work
- [ ] Recursive types allowed
- [ ] Mutually recursive types with `and`

**GADT:**
- [ ] Explicit constructor return types parsed
- [ ] Type refinement in pattern matching
- [ ] Existential types work
- [ ] Type-indexed evaluation example works

**Records:**
- [ ] Record type declarations
- [ ] Record expressions
- [ ] Field access via dot notation
- [ ] Copy-and-update syntax
- [ ] Pattern matching on records
- [ ] Structural equality within same type

**Modules:**
- [ ] Top-level module declarations
- [ ] Namespace declarations
- [ ] Nested modules
- [ ] `open` keyword
- [ ] Qualified name access
- [ ] Implicit module from filename

**Exceptions:**
- [ ] Exception declarations
- [ ] `raise` works
- [ ] `try...with` expressions
- [ ] Pattern matching on exception types
- [ ] `when` guards in handlers

---

## Sources

### Indentation Syntax
- [F# code formatting guidelines - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [F# syntax: indentation and verbosity | F# for fun and profit](https://fsharpforfunandprofit.com/posts/fsharp-syntax/)
- [Principled Parsing for Indentation-Sensitive Languages](https://www.researchgate.net/publication/262389112_Principled_Parsing_for_Indentation-Sensitive_Languages_Revisiting_Landin's_Offside_Rule)
- [Off-side rule - Wikipedia](https://en.wikipedia.org/wiki/Off-side_rule)
- [Indentation-based syntax considered troublesome](https://yinwang0.wordpress.com/2011/05/08/layout/)
- [Python TabError: inconsistent use of tabs and spaces](https://www.geeksforgeeks.org/python/python-taberror-inconsistent-use-of-tabs-and-spaces-in-indentation/)

### Algebraic Data Types
- [Algebraic data type - Wikipedia](https://en.wikipedia.org/wiki/Algebraic_data_type)
- [CS 242: Algebraic data types - Stanford](https://stanford-cs242.github.io/f19/lectures/03-2-algebraic-data-types.html)
- [Algebraic Data Types | Scala 3 Documentation](https://docs.scala-lang.org/scala3/book/types-adts-gadts.html)
- [Exhaustiveness checking and algebraic data types](https://github.com/josefs/Gradualizer/wiki/Exhaustiveness-checking-and-algebraic-data-types)
- [Algebraic data types and pattern matching — OCaml From the Ground Up](https://ocamlbook.org/algebraic-types/)

### GADTs
- [Generalized algebraic data type - Wikipedia](https://en.wikipedia.org/wiki/Generalized_algebraic_data_type)
- [Generalised algebraic datatype - HaskellWiki](https://wiki.haskell.org/Generalised_algebraic_datatype)
- [GADTs — GHC User's Guide](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html)
- [GADTs - Real World OCaml](https://dev.realworldocaml.org/gadts.html)
- [Sound and Complete Bidirectional Typechecking for GADTs](https://www.cl.cam.ac.uk/~nk480/gadt.pdf)
- [Simple unification-based type inference for GADTs - Microsoft Research](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf)

### Records
- [Records in F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Records | F# for fun and profit](https://fsharpforfunandprofit.com/posts/records/)
- [Copy and Update Record Expressions - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/copy-and-update-record-expressions)
- [Immutable object - Wikipedia](https://en.wikipedia.org/wiki/Immutable_object)
- [Functional Optics for Modern Java - 2026](https://blog.scottlogic.com/2026/01/09/java-the-immutability-gap.html)

### Modules
- [Modules - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Namespaces in F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces)
- [open Declarations - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/import-declarations-the-open-keyword)
- [First-Class Modules - Real World OCaml](https://dev.realworldocaml.org/first-class-modules.html)
- [ML Dialects and Haskell - Hyperpolyglot](https://hyperpolyglot.org/ml)

### Exceptions
- [Exceptions: The try...with Expression - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression)
- [Exceptions | F# for fun and profit](https://fsharpforfunandprofit.com/posts/exceptions/)
- [Exception Handling - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/)
- [Exceptions: raise and reraise functions - F# | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function)

### Language Comparisons
- [How does OCaml compare to F#](https://discuss.ocaml.org/t/how-does-ocaml-compare-to-f-in-the-family-of-ml-languages/11665)
- [Comparing OCAML to F#](https://jkone27-3876.medium.com/comparing-ocaml-to-f-f75e4ab27769)
- [F# vs Haskell comparison](https://www.educba.com/f-sharp-vs-haskell/)

---

**Document Status:** Research complete, ready for requirements definition
**Confidence Level:** HIGH (based on official documentation and established patterns)
**Next Step:** Use this feature catalog to define requirements and roadmap phases
