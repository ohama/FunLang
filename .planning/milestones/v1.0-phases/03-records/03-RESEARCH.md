# Phase 3: Records - Research

**Researched:** 2026-03-09
**Domain:** Record types in a functional language compiler (F#-style syntax, Hindley-Milner type system)
**Confidence:** HIGH

## Summary

This research investigates how to implement record types in LangThree, a functional language with F#-style indentation syntax, Hindley-Milner type inference, and existing ADT support. Records add named fields to the type system, enabling structured data beyond tuples and ADTs.

The implementation requires changes across all compiler layers: new AST nodes for record declarations/expressions/patterns, new tokens (LBRACE, RBRACE, SEMICOLON, DOT, WITH, MUTABLE), parser grammar extensions, a record type environment parallel to the existing ConstructorEnv, type system extensions for field access and copy-and-update, evaluator support for record values, and structural equality.

The standard approach in ML-family languages treats records as nominal types (like F#, not structural like TypeScript). Each record type declaration creates a named type with known fields. Field access via dot notation is resolved by looking up the field name in the record type environment. Copy-and-update (`{ r with x = 1 }`) creates a new value with selected fields modified. Pattern matching on records uses `{ field1 = pat1; field2 = pat2 }` syntax.

**Primary recommendation:** Extend the existing ADT infrastructure pattern (TypeDecl -> elaboration -> ConstructorEnv) to build a parallel RecordEnv mapping type names to field definitions, reuse TData for the type representation, and implement records as a special form of named type with field accessors.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FsLexYacc | 11.3.0 | Parser generation | Already in use, extend grammar |
| Existing Type.fs | - | TData type constructor | Records use TData(name, args) like ADTs |
| Existing Bidir.fs | - | Bidirectional type checker | Extend synth/check for record expressions |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| N/A | - | No new dependencies needed | Pure compiler extension |

**Installation:**
No new dependencies. Extend existing F# compiler infrastructure.

## Architecture Patterns

### Recommended Project Structure Changes
```
src/LangThree/
  Ast.fs           # Add RecordDecl, RecordExpr, RecordUpdate, FieldAccess, RecordPat
  Type.fs          # Add RecordFieldInfo, RecordEnv type definitions
  Elaborate.fs     # Add elaborateRecordDecl (parallel to elaborateTypeDecl)
  Diagnostic.fs    # Add record-specific error kinds
  Lexer.fsl        # Add LBRACE, RBRACE, SEMICOLON, DOT, MUTABLE tokens
  Parser.fsy       # Add record declaration, expression, pattern grammar rules
  Infer.fs         # Extend inferPattern for RecordPat
  Bidir.fs         # Extend synth for RecordExpr, RecordUpdate, FieldAccess
  TypeCheck.fs     # Extend typeCheckModule for record declarations
  Eval.fs          # Add RecordValue, field access, copy-and-update evaluation
  Format.fs        # Add record formatting
```

### Pattern 1: Record Type Declaration (REC-01)

**What:** Declare record types with named, typed fields.
**When to use:** First step -- AST and type definitions.

F# syntax target:
```
type Point = { x: float; y: float }
type Person = { name: string; age: int; mutable score: int }
```

AST representation:
```fsharp
// In Ast.fs -- new types
type RecordFieldDecl =
    | RecordFieldDecl of name: string * fieldType: TypeExpr * isMutable: bool * Span

type RecordDecl =
    | RecordDecl of name: string * typeParams: string list * fields: RecordFieldDecl list * Span

// Extend Decl:
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | TypeDecl of TypeDecl
    | RecordTypeDecl of RecordDecl  // NEW
```

**Design decision -- separate Decl variant vs. extending TypeDecl:** Use a separate `RecordTypeDecl` variant in `Decl` rather than embedding records inside `TypeDecl`. Rationale: Records and ADTs have fundamentally different structure (named fields vs. constructor variants), and keeping them separate avoids complicating the existing ADT code path.

### Pattern 2: Record Type Environment (parallel to ConstructorEnv)

**What:** A compile-time environment mapping record type names to field information.
**When to use:** After AST extension, in Type.fs and Elaborate.fs.

```fsharp
// In Type.fs
type RecordFieldInfo = {
    Name: string
    FieldType: Type
    IsMutable: bool
    Index: int          // Field position (for efficient runtime access)
}

type RecordTypeInfo = {
    TypeParams: int list
    Fields: RecordFieldInfo list
    ResultType: Type    // Always TData(name, [TVar ...])
}

/// Record environment: type name -> record type information
type RecordEnv = Map<string, RecordTypeInfo>
```

```fsharp
// In Elaborate.fs -- new function
let elaborateRecordDecl (Ast.RecordDecl(name, typeParams, fields, _)) : string * RecordTypeInfo =
    // Map type parameter names to TVar indices (same approach as elaborateTypeDecl)
    let paramMap =
        typeParams
        |> List.mapi (fun i p ->
            let varName = if p.StartsWith("'") then p else "'" + p
            (varName, i))
        |> Map.ofList
    let typeParamVars = typeParams |> List.mapi (fun i _ -> i)
    let resultType = TData(name, List.map TVar typeParamVars)

    let fieldInfos =
        fields |> List.mapi (fun idx (Ast.RecordFieldDecl(fname, ftype, isMut, _)) ->
            { Name = fname
              FieldType = substTypeExpr paramMap ftype  // Reuse existing approach
              IsMutable = isMut
              Index = idx })

    (name, { TypeParams = typeParamVars; Fields = fieldInfos; ResultType = resultType })
```

### Pattern 3: Record Expressions and Field Access (REC-02, REC-03)

**What:** Create record values and access fields.
**When to use:** AST nodes for expressions.

```fsharp
// In Ast.fs -- extend Expr
type Expr =
    // ... existing variants ...
    | RecordExpr of typeName: string option * fields: (string * Expr) list * span: Span
        // { x = 1.0; y = 2.0 } or { Point x = 1.0; y = 2.0 }
    | FieldAccess of expr: Expr * fieldName: string * span: Span
        // point.x
    | RecordUpdate of source: Expr * fields: (string * Expr) list * span: Span
        // { point with y = 3.0 }
```

### Pattern 4: Record Value at Runtime (REC-02, REC-06)

**What:** Runtime representation of record values.
**When to use:** Evaluator.

```fsharp
// In Ast.fs -- extend Value
type Value =
    // ... existing variants ...
    | RecordValue of typeName: string * fields: Map<string, Value>
```

Using `Map<string, Value>` rather than an array for simplicity and immutable update efficiency. F# Maps are persistent data structures, so `{ r with x = 5 }` is just `Map.add "x" (IntValue 5) existingFields`.

### Pattern 5: Copy-and-Update Syntax (REC-04)

**What:** Create modified copy of a record.
**When to use:** Core feature for immutable records.

F# syntax: `{ point with y = 3.0 }`

Key implementation concern: The parser needs to distinguish between:
- Record creation: `{ x = 1; y = 2 }`
- Copy-and-update: `{ point with y = 3 }`

Both start with `LBRACE`. The parser can look ahead: if the first identifier is followed by `WITH` (or resolved as an expression followed by WITH), it is copy-and-update. Otherwise, it is creation.

Grammar approach:
```
RecordExpr:
    | LBRACE RecordFieldBindings RBRACE              // Record creation
    | LBRACE Expr WITH RecordFieldBindings RBRACE    // Copy-and-update
```

Note: `WITH` token already exists (used for `match ... with`). Reuse it.

### Pattern 6: Pattern Matching on Records (REC-05)

**What:** Destructure records in pattern matching.
**When to use:** After basic records work.

F# syntax: `match p with { x = px; y = py } -> ...`

```fsharp
// In Ast.fs -- extend Pattern
type Pattern =
    // ... existing variants ...
    | RecordPat of fields: (string * Pattern) list * span: Span
        // { x = px; y = py }  (partial patterns allowed -- not all fields required)
```

### Pattern 7: Structural Equality (REC-06)

**What:** Records of the same type with equal field values are equal.
**When to use:** Eval.fs equality operators.

```fsharp
// In Eval.fs -- extend Equal/NotEqual cases
| RecordValue (t1, f1), RecordValue (t2, f2) ->
    BoolValue (t1 = t2 && f1 = f2)  // Map equality works structurally in F#
```

### Pattern 8: Mutable Fields (REC-07)

**What:** Fields declared with `mutable` can be modified in place.
**When to use:** After immutable records work completely.

This is the most complex requirement and should be implemented last. Options:

**Option A (Recommended): Ref-based mutation** -- Store mutable field values in `ref` cells at the Value level. Semantically cleanest, doesn't require environment threading.

```fsharp
// RecordValue stores a mix of direct values and ref cells
type Value =
    | RecordValue of typeName: string * fields: Map<string, Value>
    // Mutable fields store RefValue wrapping the current value
    | RefValue of Value ref
```

**Option B: Expression-level assignment** -- Add a `SetField` expression variant (`point.score <- 42`). This requires:
- New AST node: `SetField of expr: Expr * fieldName: string * value: Expr * span: Span`
- New token: `LARROW` for `<-`
- Type checking that the field is declared mutable
- Evaluator mutation

**Recommendation:** Implement Option B with explicit assignment syntax (`<-`), which matches F# semantics. The evaluator can use F# mutable variables or ref cells internally.

**Important:** Mutable fields introduce side effects. The type checker must verify that only fields declared `mutable` can be assigned. This requires passing the RecordEnv to the type checker for field mutability lookups.

### Anti-Patterns to Avoid

- **Structural record typing:** Do NOT implement structural/anonymous records. LangThree follows F#'s nominal typing. Each record must be declared with a `type` keyword and resolved by name.
- **Overloading field names across types:** In F#, field names can be ambiguous when multiple record types share field names. For Phase 3, require that field names are globally unique (simpler) or use type annotations to disambiguate. Start with global uniqueness.
- **Mixing record and ADT in one type:** Do NOT allow `type T = { x: int } | A | B`. Records and ADTs are separate type forms.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Field name resolution | Custom lookup | RecordEnv (Map<string, RecordTypeInfo>) | Need O(1) lookup by type name, then linear scan of fields |
| Immutable update | Custom copy logic | Map.add on Map<string, Value> | F# Maps are persistent; single-field update is O(log n) |
| Record equality | Custom traversal | F# structural equality on Map<string, Value> | F# auto-derives structural equality for maps |
| Token reuse | New WITH_KW token | Existing WITH token | Already lexed for match...with; context-free grammar handles disambiguation |

**Key insight:** Records share significant infrastructure with the existing ADT system. The TData type constructor, elaboration pattern, and module-level type checking flow all apply directly. The main new machinery is field-level access and record-specific expressions.

## Common Pitfalls

### Pitfall 1: Parser Ambiguity with Braces and Semicolons
**What goes wrong:** `{ x = 1; y = 2 }` can conflict with block expressions or other brace uses.
**Why it happens:** LR parsers struggle with ambiguous lookahead when braces serve multiple purposes.
**How to avoid:** LangThree does NOT currently use braces for any purpose. LBRACE/RBRACE are new tokens exclusively for records. No ambiguity. However, semicolons need care -- use SEMICOLON as a field separator (new token, not reusing anything).
**Warning signs:** Shift/reduce conflicts in generated parser.

### Pitfall 2: Copy-and-Update vs. Record Creation Parsing
**What goes wrong:** `{ expr with field = val }` requires parsing `expr` before seeing `with`, creating potential conflicts.
**Why it happens:** The parser needs to distinguish `{ x = 1; ... }` (creation, x is a field name) from `{ x with y = 1 }` (update, x is an expression).
**How to avoid:** In the grammar, use a production like:
```
RecordExprInner:
    | Expr WITH RecordFieldBindings    // Copy-and-update (Expr resolves first)
    | RecordFieldBindings              // Record creation
```
The key insight: in record creation, field bindings are `IDENT EQUALS Expr`, which could initially look like an expression `IDENT EQUALS Expr` (variable assignment). But since `IDENT EQUALS Expr` inside braces is not a valid expression in LangThree (it would be a let without in), this is unambiguous.

**Alternative safe approach:** Require explicit type name for record creation: `{ Point x = 1.0; y = 2.0 }` or `Point { x = 1.0; y = 2.0 }`. This eliminates ambiguity entirely. However, F# does not require this when the type can be inferred from context. For Phase 3, start with type-name-optional and see if the grammar is clean. If not, fall back to requiring it.

### Pitfall 3: Field Name Disambiguation
**What goes wrong:** When two record types share a field name (e.g., both `Point` and `Color` have field `x`), field access `r.x` is ambiguous.
**Why it happens:** F# resolves this using type information, which requires bidirectional type checking.
**How to avoid:** For Phase 3 initial implementation, enforce globally unique field names. This is simpler and matches OCaml's approach. Add a check in `typeCheckModule` that no two record types share field names. Disambiguation can be added later if needed.
**Warning signs:** Test cases with overlapping field names silently resolving to wrong type.

### Pitfall 4: Dot Notation Parsing Priority
**What goes wrong:** `point.x` could be parsed as function application `point .x` if dot is not handled carefully.
**Why it happens:** The parser's AppExpr production aggressively consumes adjacent atoms.
**How to avoid:** Make DOT a postfix operator with very high precedence. Parse `FieldAccess` at the Atom level:
```
Atom:
    | Atom DOT IDENT    { FieldAccess($1, $3, ruleSpan parseState 1 3) }
```
This ensures `point.x` is parsed as field access before function application can consume it. Left-recursion in Atom handles chaining: `person.address.city`.

### Pitfall 5: Mutable Field Semantics
**What goes wrong:** Mutable field assignment changes the original record, violating value semantics.
**Why it happens:** If records are represented as immutable Maps, mutation requires a different mechanism.
**How to avoid:** For mutable fields, wrap the value in a ref cell at creation time. Assignment (`r.field <- value`) modifies the ref cell's contents. The record itself remains immutable (the ref cell identity doesn't change), but the pointed-to value does. This is exactly how F# handles mutable record fields internally.
**Warning signs:** Tests where `let r2 = r1` followed by `r1.mutable_field <- x` also changes `r2`.

### Pitfall 6: Forgetting to Update spanOf and Other Exhaustive Matches
**What goes wrong:** Adding new AST variants without updating all pattern matches in spanOf, patternSpanOf, collectMatches, formatAst, etc.
**Why it happens:** F# warns about incomplete matches, but it's easy to miss during development.
**How to avoid:** After adding AST variants, compile immediately and fix all warnings. The compiler will catch every missing case. Keep a checklist of functions to update (see Code Examples section).

### Pitfall 7: Record Type in Existing TData vs. New TRecord
**What goes wrong:** Introducing a new `TRecord` type constructor when `TData` would suffice.
**Why it happens:** Records feel different from ADTs, tempting a separate type representation.
**How to avoid:** Use `TData(name, typeArgs)` for record types, just like ADTs. The distinction between records and ADTs is in the environment (RecordEnv vs. ConstructorEnv), not in the Type representation. This means unification, substitution, free variable collection, and formatting all work without modification.
**Warning signs:** Having to duplicate all Type.fs operations for a new type constructor.

## Code Examples

### Checklist of Files to Modify (in compilation order)

```fsharp
// 1. Ast.fs -- Add these types and extend existing ones:
//    - RecordFieldDecl type
//    - RecordDecl type
//    - Extend Expr with: RecordExpr, FieldAccess, RecordUpdate (and optionally SetField for REC-07)
//    - Extend Pattern with: RecordPat
//    - Extend Value with: RecordValue
//    - Extend Decl with: RecordTypeDecl
//    - Update spanOf, patternSpanOf, declSpanOf

// 2. Type.fs -- Add:
//    - RecordFieldInfo record type
//    - RecordTypeInfo record type
//    - RecordEnv type alias
//    - Update formatType if needed (TData already handles named types)

// 3. Elaborate.fs -- Add:
//    - elaborateRecordDecl function

// 4. Diagnostic.fs -- Add error kinds:
//    - UnboundField of recordType: string * fieldName: string
//    - DuplicateFieldName of fieldName: string
//    - MissingFields of recordType: string * missing: string list
//    - ImmutableFieldAssignment of recordType: string * fieldName: string
//    - DuplicateRecordField of fieldName: string * type1: string * type2: string

// 5. Lexer.fsl -- Add tokens:
//    | '{'  -> LBRACE
//    | '}'  -> RBRACE
//    | ';'  -> SEMICOLON
//    | '.'  -> DOT
//    | "mutable" -> MUTABLE
//    | "<-" -> LARROW  (for mutable field assignment, REC-07)

// 6. Parser.fsy -- Add:
//    - Token declarations: LBRACE RBRACE SEMICOLON DOT MUTABLE LARROW
//    - RecordDeclaration grammar rule (in Decls)
//    - RecordExpr grammar rule (in Atom)
//    - FieldAccess grammar rule (in Atom, left-recursive)
//    - RecordUpdate grammar rule (in Atom)
//    - RecordPat grammar rule (in Pattern)
//    - SetField grammar rule (for REC-07, in Expr)

// 7. Infer.fs -- Extend:
//    - inferPattern: add RecordPat case

// 8. Bidir.fs -- Extend:
//    - synth: add RecordExpr, FieldAccess, RecordUpdate, SetField cases
//    - Signature change: synth needs RecordEnv parameter (or combined env)

// 9. TypeCheck.fs -- Extend:
//    - typeCheckModule: build RecordEnv from RecordTypeDecl declarations
//    - Pass RecordEnv to synth calls
//    - Validate field name uniqueness

// 10. Eval.fs -- Extend:
//    - eval: add RecordExpr, FieldAccess, RecordUpdate, SetField cases
//    - matchPattern: add RecordPat case
//    - formatValue: add RecordValue case

// 11. Format.fs -- Extend:
//    - formatAst: add new Expr variants
//    - formatPattern: add RecordPat
//    - formatToken: add new tokens
```

### Record Declaration Parsing Example
```fsharp
// Parser.fsy grammar addition
RecordDeclaration:
    | TYPE IDENT TypeParams EQUALS LBRACE RecordFields RBRACE
        { Ast.RecordDecl($2, $3, $6, ruleSpan parseState 1 7) }
    | TYPE IDENT TypeParams EQUALS INDENT LBRACE RecordFields RBRACE DEDENT
        { Ast.RecordDecl($2, $3, $7, ruleSpan parseState 1 9) }

RecordFields:
    | RecordField                              { [$1] }
    | RecordField SEMICOLON RecordFields       { $1 :: $3 }
    | RecordField SEMICOLON                    { [$1] }  // trailing semicolon

RecordField:
    | IDENT COLON TypeExpr
        { Ast.RecordFieldDecl($1, $3, false, ruleSpan parseState 1 3) }
    | MUTABLE IDENT COLON TypeExpr
        { Ast.RecordFieldDecl($2, $4, true, ruleSpan parseState 1 4) }
```

### Field Access Type Checking Example
```fsharp
// In Bidir.fs synth function
| FieldAccess (expr, fieldName, span) ->
    let s1, exprTy = synth ctorEnv recEnv ctx env expr
    let resolvedTy = apply s1 exprTy
    match resolvedTy with
    | TData (typeName, typeArgs) ->
        match Map.tryFind typeName recEnv with
        | Some recInfo ->
            match recInfo.Fields |> List.tryFind (fun f -> f.Name = fieldName) with
            | Some fieldInfo ->
                // Instantiate field type with the record's actual type args
                let subst = List.zip recInfo.TypeParams typeArgs |> Map.ofList
                let fieldTy = apply subst fieldInfo.FieldType
                (s1, fieldTy)
            | None ->
                raise (TypeException { Kind = UnboundField (typeName, fieldName); ... })
        | None ->
            raise (TypeException { Kind = NotARecord typeName; ... })
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

### Record Evaluation Example
```fsharp
// In Eval.fs
| RecordExpr (_, fieldExprs, _) ->
    let fieldValues =
        fieldExprs
        |> List.map (fun (name, expr) -> (name, eval env expr))
        |> Map.ofList
    RecordValue ("Point", fieldValues)  // typeName resolved during type checking

| FieldAccess (expr, fieldName, _) ->
    match eval env expr with
    | RecordValue (_, fields) ->
        match Map.tryFind fieldName fields with
        | Some value -> value
        | None -> failwithf "Field not found: %s" fieldName
    | _ -> failwith "Field access on non-record value"

| RecordUpdate (source, updates, _) ->
    match eval env source with
    | RecordValue (typeName, fields) ->
        let updatedFields =
            updates
            |> List.fold (fun acc (name, expr) ->
                Map.add name (eval env expr) acc) fields
        RecordValue (typeName, updatedFields)
    | _ -> failwith "Copy-and-update on non-record value"
```

### Passing RecordEnv Through the System

**Critical design question:** How to pass RecordEnv to the type checker alongside ConstructorEnv?

**Option A (Recommended): Combined environment parameter**
```fsharp
// Create a combined type context
type TypeContext = {
    CtorEnv: ConstructorEnv
    RecordEnv: RecordEnv
}

// Update Bidir.synth signature:
let rec synth (tctx: TypeContext) (ctx: InferContext list) (env: TypeEnv) (expr: Expr): Subst * Type
```

**Option B: Add RecordEnv as separate parameter**
```fsharp
let rec synth (ctorEnv: ConstructorEnv) (recEnv: RecordEnv) (ctx: ...) (env: ...) (expr: ...) = ...
```

**Recommendation:** Option A (TypeContext record) is cleaner because it avoids changing the parameter count of every function in the chain. It also makes it easy to add more environments later (e.g., module env, interface env).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate TRecord type | Use TData for records | Standard ML practice | No Type.fs changes needed |
| Global field lookup | Type-directed field resolution | F# 4.0+ | Enables same field names across types (future) |

**Deprecated/outdated:**
- Anonymous/structural records: Not applicable to this language's nominal type system.

## Open Questions

1. **Field name uniqueness scope**
   - What we know: F# allows overlapping field names between record types and uses type inference to disambiguate.
   - What's unclear: Whether LangThree should support this in Phase 3 or enforce global uniqueness.
   - Recommendation: Start with global uniqueness (simpler). Add type-directed disambiguation in a future phase if needed.

2. **Record type inference for creation expressions**
   - What we know: `{ x = 1; y = 2 }` needs to resolve to a specific record type. With globally unique field names, the first field name uniquely identifies the type.
   - What's unclear: If we drop global uniqueness, how to infer the type when multiple records share field names.
   - Recommendation: Resolve by field set -- the combination of all field names in the expression must match exactly one declared record type.

3. **Indentation handling for multi-line records**
   - What we know: The IndentFilter handles INDENT/DEDENT for existing constructs.
   - What's unclear: Whether `{ ... }` braces should suppress indentation sensitivity (like F# does inside braces).
   - Recommendation: Braces are explicit delimiters, so indentation inside them should be ignored. The existing IndentFilter may need adjustment to suppress indent/dedent tracking inside brace pairs. However, since braces provide explicit scoping, the parser should work without indentation tokens inside them. Test this during implementation.

4. **Interaction between records and ADTs**
   - What we know: F# allows ADT constructors to carry record types: `type Shape = Circle of { radius: float } | Rect of { w: float; h: float }`.
   - What's unclear: Whether Phase 3 should support inline record types in ADT constructors.
   - Recommendation: Do NOT support this in Phase 3. Records and ADTs are declared separately. ADT constructors can reference record types by name: `Circle of CircleData`.

5. **RecordEnv passing -- TypeContext vs. separate params**
   - What we know: The current synth function takes `ctorEnv` as a parameter.
   - What's unclear: Whether to refactor to a TypeContext record now or add another parameter.
   - Recommendation: Use TypeContext record. It's a breaking change to synth's signature but avoids parameter proliferation. Do the refactor in the first task of this phase.

## Implementation Order

Recommended task ordering based on dependencies:

1. **Tokens and AST** (Lexer.fsl, Ast.fs) -- Foundation, no functional changes
2. **Type environment** (Type.fs, Elaborate.fs) -- RecordEnv parallel to ConstructorEnv
3. **Parser** (Parser.fsy) -- Record declarations, expressions, field access
4. **Type checker** (Bidir.fs, TypeCheck.fs, Infer.fs) -- Type checking records
5. **Evaluator** (Eval.fs) -- Runtime record support
6. **Copy-and-update** (Parser, Bidir, Eval) -- `{ r with x = 1 }` syntax
7. **Pattern matching** (Parser, Infer, Eval) -- Record patterns
8. **Structural equality** (Eval.fs) -- Equality on record values
9. **Mutable fields** (all layers) -- Last, most complex requirement
10. **Format and diagnostics** (Format.fs, Diagnostic.fs) -- Polish

## Sources

### Primary (HIGH confidence)
- Existing LangThree codebase (Ast.fs, Type.fs, Elaborate.fs, Bidir.fs, TypeCheck.fs, Eval.fs, Parser.fsy, Lexer.fsl) -- all patterns directly observed
- Phase 2 ADT implementation -- proven pattern for type declaration -> elaboration -> environment flow
- F# language specification for record syntax and semantics

### Secondary (MEDIUM confidence)
- ML-family language design conventions for nominal record types
- F# compiler approach to field disambiguation (applies if global uniqueness is dropped)

### Tertiary (LOW confidence)
- Mutable field implementation details -- ref cell approach is standard but interaction with the existing pure evaluator needs validation during implementation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- No new dependencies, all patterns proven by ADT phase
- Architecture: HIGH -- Direct extension of existing patterns observed in codebase
- Pitfalls: HIGH -- Identified from direct codebase analysis and parser grammar inspection
- Mutable fields: MEDIUM -- Conceptually clear but implementation complexity in the pure evaluator is uncertain

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable domain, no external dependency changes expected)
