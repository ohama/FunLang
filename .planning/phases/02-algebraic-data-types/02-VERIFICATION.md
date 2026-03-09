---
phase: 02-algebraic-data-types
verified: 2026-03-09T12:00:00Z
status: gaps_found
score: 3/5 must-haves verified
gaps:
  - truth: "User writes incomplete pattern match and receives exhaustiveness warning with missing cases"
    status: failed
    reason: "Exhaustive.fs algorithm exists and passes unit tests, but is NOT wired into the type checking pipeline"
    artifacts:
      - path: "src/LangThree/Exhaustive.fs"
        issue: "Module is standalone; never called from TypeCheck.fs or Bidir.fs"
      - path: "src/LangThree/Exhaustive.fs:227-228"
        issue: "getConstructors has failwith 'Not implemented'"
      - path: "src/LangThree/TypeCheck.fs"
        issue: "typeCheckModule does not call checkExhaustive on match expressions"
    missing:
      - "AST Pattern -> CasePat conversion function (no astToCasePat or equivalent exists)"
      - "Call to Exhaustive.checkExhaustive from TypeCheck.typeCheckModule or Bidir.synth Match case"
      - "Constructor set lookup from ConstructorEnv for the scrutinee type (getConstructors is unimplemented)"
      - "Warning/diagnostic emission for non-exhaustive patterns (no warning Diagnostic type exists)"
  - truth: "User writes unreachable pattern and receives redundancy warning"
    status: failed
    reason: "Exhaustive.checkRedundant exists and passes unit tests, but is NOT wired into the type checking pipeline"
    artifacts:
      - path: "src/LangThree/Exhaustive.fs"
        issue: "checkRedundant never called from any source file"
      - path: "src/LangThree/TypeCheck.fs"
        issue: "No redundancy checking in typeCheckModule"
    missing:
      - "Call to Exhaustive.checkRedundant from the type checking pipeline"
      - "Warning/diagnostic emission for redundant patterns"
      - "Same AST Pattern -> CasePat conversion needed by exhaustiveness"
---

# Phase 2: Algebraic Data Types Verification Report

**Phase Goal:** Users can define and use sum types with exhaustive pattern matching
**Verified:** 2026-03-09
**Status:** gaps_found
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can declare sum types with multiple constructors carrying data | VERIFIED | Parser accepts `type Option 'a = None \| Some of 'a`; tests testParseSimpleADT, testParseADTWithData, testParseADTWithTypeParam pass; Elaborate.elaborateTypeDecl creates ConstructorEnv correctly |
| 2 | User can pattern match on ADT constructors and access carried data | VERIFIED | Eval.matchPattern handles ConstructorPat against DataValue; tests testADTPatternMatchingWithData, testADTRecursiveTreeEval pass; Bidir.synth Constructor + Match cases type-check correctly |
| 3 | User writes incomplete pattern match and receives exhaustiveness warning with missing cases | FAILED | Exhaustive.fs implements Maranget algorithm (checkExhaustive, buildMissingWitness, formatPattern) and 23 unit tests pass, BUT the module is never called from TypeCheck or Bidir -- user receives NO warning |
| 4 | User writes unreachable pattern and receives redundancy warning | FAILED | Exhaustive.checkRedundant exists with working tests, BUT is never called from the type checking pipeline -- user receives NO warning |
| 5 | User can define recursive types | VERIFIED | testParseRecursiveADT passes; testADTRecursiveTreeConstruction and testADTRecursiveTreeEval demonstrate `type Tree = Leaf \| Node of Tree * int * Tree` works end-to-end including recursive let rec evaluation summing to 30 |

**Score:** 3/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Ast.fs` | TypeDecl, ConstructorDecl, ConstructorPat, DataValue, Constructor, TEName AST nodes | VERIFIED | All present: TypeDecl (line 134), ConstructorDecl (line 138), ConstructorPat (line 107), DataValue (line 150), Constructor (line 89), TEName (line 129). 212 lines, substantive. |
| `src/LangThree/Type.fs` | TData, ConstructorInfo, ConstructorEnv types | VERIFIED | TData (line 13), ConstructorInfo (line 24-28), ConstructorEnv (line 31). 149 lines, substantive. |
| `src/LangThree/Parser.fsy` | TypeDeclaration, Constructor, Constructors grammar rules; uppercase IDENT as constructors | VERIFIED | Lines 257-290 (TypeDeclaration with leading pipe, indented, mutual recursion); lines 131-133, 151-156 (uppercase IDENT -> Constructor in AppExpr and Atom); lines 187-199 (constructor patterns). 327 lines. |
| `src/LangThree/Lexer.fsl` | TYPE, OF, AND_KW tokens | VERIFIED | Lines 52-54: `"type"`, `"of"`, `"and"` keywords. |
| `src/LangThree/Elaborate.fs` | elaborateTypeDecl converting TypeDecl to ConstructorEnv | VERIFIED | Lines 71-110. Handles type params, recursive type references (TEName -> TData). 121 lines. |
| `src/LangThree/Bidir.fs` | synth accepts ConstructorEnv, handles Constructor expr and ConstructorPat | VERIFIED | synth signature (line 25) takes `ctorEnv: ConstructorEnv`; Constructor case (lines 46-87); Match case (lines 259-275) calls `inferPattern ctorEnv`. 343 lines. |
| `src/LangThree/Infer.fs` | inferPattern handles ConstructorPat with ConstructorEnv lookup | VERIFIED | Lines 87-132. Instantiates type params, checks arity, unifies arg types. |
| `src/LangThree/TypeCheck.fs` | typeCheckModule builds ConstructorEnv and passes to Bidir.synth | VERIFIED | Lines 74-104. Collects TypeDecl from decls, calls elaborateTypeDecl, folds into ctorEnv, passes to Bidir.synth. |
| `src/LangThree/Eval.fs` | Constructor -> DataValue evaluation; ConstructorPat matching | VERIFIED | Constructor (lines 235-237); ConstructorPat in matchPattern (lines 60-67). 278 lines. |
| `src/LangThree/Exhaustive.fs` | Maranget usefulness, checkExhaustive, checkRedundant, formatPattern | VERIFIED (standalone) / ORPHANED (not wired) | Algorithm is correct and tested (23 tests). But Exhaustive module is never called from any source file in src/. getConstructors (line 227) has `failwith "Not implemented"`. |
| `src/LangThree/Diagnostic.fs` | UnboundConstructor, ArityMismatch error kinds | VERIFIED | Lines 22-23. Used by Infer.fs and Bidir.fs for constructor type errors. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Parser.fsy | Ast.fs | TypeDeclaration rules construct TypeDecl nodes | WIRED | Lines 257-269 create Ast.TypeDecl; lines 131-133 create Constructor; lines 187-199 create ConstructorPat |
| Lexer.fsl | Parser.fsy | TYPE, OF, AND_KW tokens | WIRED | Token declarations (lines 46-47 parser) match lexer keywords (lines 52-54 lexer) |
| Elaborate.fs | Type.fs | elaborateTypeDecl creates TData and ConstructorInfo | WIRED | Line 81: `TData(name, ...)`, Lines 104-108: ConstructorInfo creation |
| TypeCheck.fs | Elaborate.fs | typeCheckModule calls elaborateTypeDecl | WIRED | Lines 83-87: `List.map elaborateTypeDecl` |
| TypeCheck.fs | Bidir.fs | Passes ctorEnv to Bidir.synth | WIRED | Line 96: `Bidir.synth ctorEnv [] env body` |
| Bidir.fs | Infer.fs | synth Match case calls inferPattern with ctorEnv | WIRED | Line 263: `inferPattern ctorEnv pat` |
| Infer.fs | Type.fs | inferPattern looks up ConstructorEnv | WIRED | Line 88: `Map.tryFind name ctorEnv` |
| Eval.fs | Ast.fs | Constructor -> DataValue, ConstructorPat -> DataValue matching | WIRED | Lines 235-237 (eval), Lines 60-67 (matchPattern) |
| TypeCheck/Bidir | Exhaustive.fs | Match should trigger exhaustiveness/redundancy checking | NOT WIRED | No import of Exhaustive module, no call to checkExhaustive or checkRedundant |
| Exhaustive.fs | ConstructorEnv | getConstructors should look up constructors for a type | NOT WIRED | Line 227-228: `failwith "Not implemented"` |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| ADT-01: Sum types with constructors | SATISFIED | -- |
| ADT-02: Constructor syntax and pattern matching | SATISFIED | -- |
| ADT-03: Exhaustiveness checking | BLOCKED | Exhaustive module not wired into TypeCheck pipeline |
| ADT-04: Redundancy checking | BLOCKED | Exhaustive module not wired into TypeCheck pipeline |
| ADT-05: Type parameters | SATISFIED | `type Option 'a = None \| Some of 'a` works |
| ADT-06: Recursive type definitions | SATISFIED | `type Tree = Leaf \| Node of Tree * int * Tree` works end-to-end |
| ADT-07: Mutually recursive types | SATISFIED | `type Expr = ... and ArithExpr = ...` parses correctly (testParseMutuallyRecursiveADT) |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Exhaustive.fs | 227-228 | `failwith "Not implemented: getConstructors"` | BLOCKER | Prevents integration of exhaustiveness checking |
| Elaborate.fs | 55-59 | TEName treated as fresh TVar (placeholder) in elaborateTypeExpr | WARNING | Named type annotations won't resolve to TData; only affects standalone type annotations, not type declarations |
| Exhaustive.fs | 27 | `OrPat of CasePat list  // Future: or-patterns` | INFO | Future feature noted, not blocking |
| Exhaustive.fs | 88-90 | `OrPat _ -> None` in specializeRow | INFO | Or-patterns return None (future) |

### Human Verification Required

### 1. Recursive ADT Evaluation Correctness
**Test:** Run `type Tree = Leaf | Node of Tree * int * Tree` with a 3-level tree and recursive sum function
**Expected:** Correct sum of all node values
**Why human:** Tests cover 2 levels; deeper nesting may reveal stack/closure issues in recursive evaluation

### 2. Mutually Recursive Type Evaluation
**Test:** Define `type Expr = Lit of int | Arith of ArithExpr and ArithExpr = Add of Expr * Expr`, construct values, and evaluate
**Expected:** Values construct and pattern match correctly
**Why human:** testParseMutuallyRecursiveADT only tests parsing, not evaluation of mutually recursive types

### 3. Type Error Quality for Constructor Misuse
**Test:** Use an undefined constructor in a match pattern, check error message
**Expected:** Clear "Unbound constructor" error with location
**Why human:** Error message formatting and location accuracy need visual inspection

### Gaps Summary

Two of five success criteria are not achieved. The Exhaustive module (Maranget usefulness algorithm) is fully implemented as a standalone library with 23 passing tests covering exhaustiveness, redundancy, pattern formatting, and matrix specialization. However, it is completely **orphaned** -- never called from any production source file.

The specific missing pieces are:
1. **No AST Pattern -> CasePat conversion.** The Exhaustive module uses its own `CasePat` representation but no function exists to convert `Ast.Pattern` to `Exhaustive.CasePat`.
2. **No integration call.** Neither `TypeCheck.typeCheckModule` nor `Bidir.synth` (Match case) invoke `checkExhaustive` or `checkRedundant`.
3. **No constructor set resolution.** `getConstructors` (the function that would look up all constructors for a given type from `ConstructorEnv`) has `failwith "Not implemented"`.
4. **No warning infrastructure.** The `Diagnostic` module has error types but no warning concept. Exhaustiveness and redundancy should produce warnings, not errors.

The remaining 3 truths (declare sum types, pattern match on constructors, recursive types) are fully verified with both type checking and runtime evaluation working correctly across 85 passing tests.

---

_Verified: 2026-03-09_
_Verifier: Claude (gsd-verifier)_
