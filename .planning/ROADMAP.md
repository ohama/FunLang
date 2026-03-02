# Roadmap: LangThree

## Overview

LangThree transforms FunLang v6.0 into a practical ML-style functional language with F# syntax and modern type system features. The roadmap progresses through six phases: first establishing indentation-based parsing (foundation for all syntax), then building out the type system with ADTs and GADTs (core expressiveness), adding records (practical data structures), implementing modules (code organization), and finally exceptions (error handling). Each phase delivers a complete, verifiable capability that builds toward the core value: a usable functional language with modern type system features and F#-style syntax.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Indentation-Based Syntax** - F# style offside rule parsing foundation
- [ ] **Phase 2: Algebraic Data Types** - Sum types with pattern matching and type parameters
- [ ] **Phase 3: Records** - Named product types with field access and copy-update syntax
- [ ] **Phase 4: Generalized Algebraic Data Types** - Type refinement and indexed type families
- [ ] **Phase 5: Module System** - F# style code organization with namespaces
- [ ] **Phase 6: Exceptions** - F# style error handling with try-with expressions

## Phase Details

### Phase 1: Indentation-Based Syntax
**Goal**: Parser accepts F# style indentation-based syntax for all language constructs
**Depends on**: Nothing (first phase)
**Requirements**: INDENT-01, INDENT-02, INDENT-03, INDENT-04, INDENT-05, INDENT-06, INDENT-07, INDENT-08
**Success Criteria** (what must be TRUE):
  1. User can write let-bindings with continuation lines and parser accepts them
  2. User can write match expressions with indented patterns and parser aligns them correctly
  3. User can write function applications across multiple lines and parser groups them properly
  4. User writes code with tabs and receives clear error message "tabs not allowed, use spaces"
  5. User makes indentation error and receives clear error message with expected vs actual indent

**Plans**: 4 plans

Plans:
- [ ] 01-01-PLAN.md — Add context-aware indentation processing for match expressions with pipe alignment
- [ ] 01-02-PLAN.md — Enable multi-line function application with indented argument grouping
- [ ] 01-03-PLAN.md — Improve indentation error messages and add configurable indent width validation
- [ ] 01-04-PLAN.md — Support module-level declarations with multiple top-level let bindings

### Phase 2: Algebraic Data Types
**Goal**: Users can define and use sum types with exhaustive pattern matching
**Depends on**: Phase 1 (indentation syntax for type definitions)
**Requirements**: ADT-01, ADT-02, ADT-03, ADT-04, ADT-05, ADT-06, ADT-07
**Success Criteria** (what must be TRUE):
  1. User can declare sum types with multiple constructors carrying data (e.g., `type Option = None | Some of 'a`)
  2. User can pattern match on ADT constructors and access carried data
  3. User writes incomplete pattern match and receives exhaustiveness warning with missing cases
  4. User writes unreachable pattern and receives redundancy warning
  5. User can define recursive types (e.g., `type Tree = Leaf | Node of Tree * int * Tree`)
**Plans**: TBD

Plans:
- TBD during planning

### Phase 3: Records
**Goal**: Users can define and use record types with field access and immutable updates
**Depends on**: Phase 1 (indentation syntax)
**Requirements**: REC-01, REC-02, REC-03, REC-04, REC-05, REC-06, REC-07
**Success Criteria** (what must be TRUE):
  1. User can declare record types with named fields (e.g., `type Point = { x: float; y: float }`)
  2. User can create record instances with field initialization syntax
  3. User can access record fields via dot notation (`point.x`)
  4. User can create modified copy of record using copy-and-update syntax (`{ point with y = 3.0 }`)
  5. User can pattern match on record fields
**Plans**: TBD

Plans:
- TBD during planning

### Phase 4: Generalized Algebraic Data Types
**Goal**: Users can define GADTs with type refinement for type-safe DSLs
**Depends on**: Phase 2 (ADT foundation)
**Requirements**: GADT-01, GADT-02, GADT-03, GADT-04
**Success Criteria** (what must be TRUE):
  1. User can declare GADT constructors with explicit return types (e.g., `Int : int -> int expr`)
  2. User can pattern match on GADT and type checker refines types in branches (e.g., `match e with Int n -> ...` knows `n : int`)
  3. User can use existential types in GADT constructors for data hiding
  4. User writes GADT pattern match without type annotation and receives clear error "GADTs require type annotations"
**Plans**: TBD

Plans:
- TBD during planning

### Phase 5: Module System
**Goal**: Users can organize code into modules with namespaces and qualified names
**Depends on**: Phase 2, 3, 4 (type system complete)
**Requirements**: MOD-01, MOD-02, MOD-03, MOD-04, MOD-05, MOD-06
**Success Criteria** (what must be TRUE):
  1. User can declare top-level module and namespace declarations
  2. User can nest modules using indentation
  3. User can import module with `open` keyword and use unqualified names
  4. User can access module members via qualified names (`Module.function`)
  5. User creates circular module dependency and receives clear error showing cycle path
**Plans**: TBD

Plans:
- TBD during planning

### Phase 6: Exceptions
**Goal**: Users can declare exceptions and handle errors with try-with expressions
**Depends on**: Phase 2 (exception constructors are ADT-like)
**Requirements**: EXC-01, EXC-02, EXC-03, EXC-04, EXC-05
**Success Criteria** (what must be TRUE):
  1. User can declare custom exceptions with data (e.g., `exception ParseError of string * int`)
  2. User can raise exceptions with `raise` function
  3. User can catch exceptions with `try...with` expressions
  4. User can pattern match on exception types in handlers
  5. User can use `when` guards in exception handlers to filter cases
**Plans**: TBD

Plans:
- TBD during planning

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Indentation-Based Syntax | 0/3 | Ready to execute | - |
| 2. Algebraic Data Types | 0/? | Not started | - |
| 3. Records | 0/? | Not started | - |
| 4. Generalized Algebraic Data Types | 0/? | Not started | - |
| 5. Module System | 0/? | Not started | - |
| 6. Exceptions | 0/? | Not started | - |

---
*Roadmap created: 2026-02-25*
*Last updated: 2026-03-02*
