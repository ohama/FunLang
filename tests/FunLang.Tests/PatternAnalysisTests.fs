module FunLang.Tests.PatternAnalysisTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Types
open FunLang.PatternAnalysis

// =============================================================================
// Phase 9: Pattern Matching Analysis Tests
// =============================================================================
//
// TDD: 테스트 먼저 작성, 구현은 Types.fs 및 PatternAnalysis.fs에
//
// =============================================================================

// =============================================================================
// Phase 9.0: TypeDefRegistry Tests
// =============================================================================

let typeDefRegistryTests = testList "TypeDefRegistry" [

    test "build registry from single type definition" {
        // type Option 'a = None | Some of 'a
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }

        let registry = TypeDefRegistryBuilder.buildTypeDefRegistry [typeDef]

        Expect.isTrue (Map.containsKey "Option" registry) "should contain Option"
        let info = Map.find "Option" registry
        Expect.equal info.Name "Option" "name should be Option"
        Expect.equal info.TypeParams ["a"] "should have type param 'a"
        Expect.equal info.Constructors [("None", 0); ("Some", 1)] "should have None(0), Some(1)"
    }

    test "build registry from multiple type definitions" {
        // type Option 'a = None | Some of 'a
        // type Bool = True | False
        let optionDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let boolDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }

        let registry = TypeDefRegistryBuilder.buildTypeDefRegistry [optionDef; boolDef]

        Expect.isTrue (Map.containsKey "Option" registry) "should contain Option"
        Expect.isTrue (Map.containsKey "Bool" registry) "should contain Bool"

        let boolInfo = Map.find "Bool" registry
        Expect.equal boolInfo.Constructors [("True", 0); ("False", 0)] "Bool should have True(0), False(0)"
    }

    test "getConstructors returns constructors for user-defined type" {
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let registry = TypeDefRegistryBuilder.buildTypeDefRegistry [typeDef]

        let optionType = TConstructor ("Option", [TInt])
        let result = TypeDefRegistryBuilder.getConstructors optionType registry

        Expect.isSome result "should find constructors"
        Expect.equal (Option.get result) [("None", 0); ("Some", 1)] "should return constructors with arity"
    }

    test "getConstructors returns None for unknown type" {
        let registry = Map.empty

        let unknownType = TConstructor ("Unknown", [])
        let result = TypeDefRegistryBuilder.getConstructors unknownType registry

        Expect.isNone result "should return None for unknown type"
    }

    test "getConstructors returns constructors for TBool" {
        let registry = Map.empty  // Built-in types don't need registry

        let result = TypeDefRegistryBuilder.getConstructors TBool registry

        Expect.isSome result "should find bool constructors"
        Expect.equal (Option.get result) [("true", 0); ("false", 0)] "should return true and false"
    }

    test "getConstructors returns constructors for TList" {
        let registry = Map.empty

        let result = TypeDefRegistryBuilder.getConstructors (TList TInt) registry

        Expect.isSome result "should find list constructors"
        Expect.equal (Option.get result) [("[]", 0); ("::", 2)] "should return [] and ::"
    }

    test "getConstructors returns None for TInt (infinite domain)" {
        let registry = Map.empty

        let result = TypeDefRegistryBuilder.getConstructors TInt registry

        Expect.isNone result "int has infinite domain, no finite constructors"
    }

    test "getConstructors returns None for TString (infinite domain)" {
        let registry = Map.empty

        let result = TypeDefRegistryBuilder.getConstructors TString registry

        Expect.isNone result "string has infinite domain, no finite constructors"
    }

    test "registry handles type with multiple parameters" {
        // type Either 'a 'b = Left of 'a | Right of 'b
        let typeDef: TypeDef = {
            Name = "Either"
            TypeParams = ["a"; "b"]
            Constructors = [("Left", Some (TEVar "a")); ("Right", Some (TEVar "b"))]
        }

        let registry = TypeDefRegistryBuilder.buildTypeDefRegistry [typeDef]

        let info = Map.find "Either" registry
        Expect.equal info.TypeParams ["a"; "b"] "should have both type params"
        Expect.equal info.Constructors [("Left", 1); ("Right", 1)] "both constructors have arity 1"
    }
]

// =============================================================================
// Phase 9.1.1: SimplePattern and Simplify Tests
// =============================================================================

/// Helper to create Located<Pattern> without position
let locP (p: Pattern) : LPattern = Located.noLoc p

let simplePatternTests = testList "SimplePattern" [

    test "simplify wildcard" {
        let p = locP PWildcard
        let result = simplify p
        Expect.equal result SPWildcard "wildcard simplifies to SPWildcard"
    }

    test "simplify variable becomes wildcard" {
        let p = locP (PVariable "x")
        let result = simplify p
        Expect.equal result SPWildcard "variable simplifies to SPWildcard"
    }

    test "simplify integer literal" {
        let p = locP (PLiteral (LInt 42))
        let result = simplify p
        Expect.equal result (SPLiteral (LInt 42)) "int literal simplifies correctly"
    }

    test "simplify bool literal true" {
        let p = locP (PLiteral (LBool true))
        let result = simplify p
        Expect.equal result (SPLiteral (LBool true)) "true simplifies correctly"
    }

    test "simplify empty tuple" {
        let p = locP (PTuple [])
        let result = simplify p
        Expect.equal result (SPConstructor ("tuple", [])) "empty tuple is constructor"
    }

    test "simplify tuple with two elements" {
        let p = locP (PTuple [locP (PVariable "x"); locP (PLiteral (LInt 1))])
        let result = simplify p
        Expect.equal result (SPConstructor ("tuple", [SPWildcard; SPLiteral (LInt 1)])) "tuple simplifies correctly"
    }

    test "simplify empty list" {
        let p = locP (PList [])
        let result = simplify p
        Expect.equal result (SPConstructor ("[]", [])) "empty list is Nil constructor"
    }

    test "simplify single element list" {
        let p = locP (PList [locP (PVariable "x")])
        let result = simplify p
        // [x] → x :: []
        let expected = SPConstructor ("::", [SPWildcard; SPConstructor ("[]", [])])
        Expect.equal result expected "single element list is Cons(x, Nil)"
    }

    test "simplify two element list" {
        let p = locP (PList [locP (PLiteral (LInt 1)); locP (PLiteral (LInt 2))])
        let result = simplify p
        // [1; 2] → 1 :: (2 :: [])
        let expected =
            SPConstructor ("::",
                [SPLiteral (LInt 1);
                 SPConstructor ("::",
                    [SPLiteral (LInt 2);
                     SPConstructor ("[]", [])])])
        Expect.equal result expected "two element list simplifies correctly"
    }

    test "simplify cons pattern" {
        let p = locP (PCons (locP (PVariable "h"), locP (PVariable "t")))
        let result = simplify p
        Expect.equal result (SPConstructor ("::", [SPWildcard; SPWildcard])) "cons simplifies to :: constructor"
    }

    test "simplify nullary constructor" {
        let p = locP (PConstructor ("None", None))
        let result = simplify p
        Expect.equal result (SPConstructor ("None", [])) "nullary constructor has no args"
    }

    test "simplify unary constructor" {
        let p = locP (PConstructor ("Some", Some (locP (PVariable "x"))))
        let result = simplify p
        Expect.equal result (SPConstructor ("Some", [SPWildcard])) "unary constructor has one arg"
    }

    test "simplify nested constructor" {
        // Some (Some x)
        let inner = locP (PConstructor ("Some", Some (locP (PVariable "x"))))
        let outer = locP (PConstructor ("Some", Some inner))
        let result = simplify outer
        let expected = SPConstructor ("Some", [SPConstructor ("Some", [SPWildcard])])
        Expect.equal result expected "nested constructors simplify correctly"
    }
]

// =============================================================================
// Pattern to String Tests
// =============================================================================

let patternToStringTests = testList "patternToString" [

    test "wildcard to string" {
        let result = patternToString SPWildcard
        Expect.equal result "_" "wildcard is _"
    }

    test "int literal to string" {
        let result = patternToString (SPLiteral (LInt 42))
        Expect.equal result "42" "int literal"
    }

    test "bool true to string" {
        let result = patternToString (SPLiteral (LBool true))
        Expect.equal result "true" "bool true"
    }

    test "bool false to string" {
        let result = patternToString (SPLiteral (LBool false))
        Expect.equal result "false" "bool false"
    }

    test "string literal to string" {
        let result = patternToString (SPLiteral (LString "hello"))
        Expect.equal result "\"hello\"" "string literal with quotes"
    }

    test "unit to string" {
        let result = patternToString (SPLiteral LUnit)
        Expect.equal result "()" "unit is ()"
    }

    test "empty list to string" {
        let result = patternToString (SPConstructor ("[]", []))
        Expect.equal result "[]" "empty list"
    }

    test "cons pattern to string" {
        let p = SPConstructor ("::", [SPWildcard; SPConstructor ("[]", [])])
        let result = patternToString p
        Expect.equal result "_ :: []" "cons pattern"
    }

    test "tuple to string" {
        let p = SPConstructor ("tuple", [SPWildcard; SPLiteral (LInt 1)])
        let result = patternToString p
        Expect.equal result "(_, 1)" "tuple pattern"
    }

    test "nullary constructor to string" {
        let result = patternToString (SPConstructor ("None", []))
        Expect.equal result "None" "nullary constructor"
    }

    test "unary constructor to string" {
        let result = patternToString (SPConstructor ("Some", [SPWildcard]))
        Expect.equal result "Some _" "unary constructor"
    }

    test "nested constructor to string" {
        let p = SPConstructor ("Some", [SPConstructor ("Some", [SPWildcard])])
        let result = patternToString p
        Expect.equal result "Some (Some _)" "nested constructor with parens"
    }
]

// =============================================================================
// Phase 9.1.2: Matrix Operations Tests
// =============================================================================

let matrixOperationsTests = testList "Matrix Operations" [

    // -------------------------------------------------------------------------
    // specialize tests
    // -------------------------------------------------------------------------

    test "specialize empty matrix returns empty" {
        let matrix: PatternMatrix = []
        let result = specialize "Some" 1 matrix
        Expect.isEmpty result "empty matrix stays empty"
    }

    test "specialize keeps matching constructor row and expands args" {
        // Row: [Some x]
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let result = specialize "Some" 1 matrix
        // After specialize: [[x]] (args expanded)
        Expect.equal result [[SPWildcard]] "constructor args expanded"
    }

    test "specialize removes non-matching constructor row" {
        // Row: [None]
        let matrix: PatternMatrix = [
            [SPConstructor ("None", [])]
        ]
        let result = specialize "Some" 1 matrix
        Expect.isEmpty result "non-matching constructor removed"
    }

    test "specialize expands wildcard to arity wildcards" {
        // Row: [_]
        let matrix: PatternMatrix = [
            [SPWildcard]
        ]
        let result = specialize "Some" 1 matrix
        // After specialize: [[_]] (one wildcard for arity 1)
        Expect.equal result [[SPWildcard]] "wildcard expanded to arity wildcards"
    }

    test "specialize with arity 2 expands correctly" {
        // Row: [_]
        let matrix: PatternMatrix = [
            [SPWildcard]
        ]
        let result = specialize "::" 2 matrix
        // After specialize: [[_, _]] (two wildcards for cons)
        Expect.equal result [[SPWildcard; SPWildcard]] "arity 2 expands to two wildcards"
    }

    test "specialize preserves remaining columns" {
        // Row: [Some x, y]
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPLiteral (LInt 1)]); SPWildcard]
        ]
        let result = specialize "Some" 1 matrix
        // After specialize: [[1, y]]
        Expect.equal result [[SPLiteral (LInt 1); SPWildcard]] "remaining columns preserved"
    }

    test "specialize with multiple rows" {
        // Row 1: [Some x] → [x]
        // Row 2: [None]   → removed
        // Row 3: [_]      → [_]
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPLiteral (LInt 1)])]
            [SPConstructor ("None", [])]
            [SPWildcard]
        ]
        let result = specialize "Some" 1 matrix
        Expect.equal result [
            [SPLiteral (LInt 1)]
            [SPWildcard]
        ] "multiple rows handled correctly"
    }

    // -------------------------------------------------------------------------
    // defaultMatrix tests
    // -------------------------------------------------------------------------

    test "defaultMatrix empty matrix returns empty" {
        let matrix: PatternMatrix = []
        let result = defaultMatrix matrix
        Expect.isEmpty result "empty matrix stays empty"
    }

    test "defaultMatrix keeps wildcard row without first column" {
        // Row: [_, y]
        let matrix: PatternMatrix = [
            [SPWildcard; SPLiteral (LInt 1)]
        ]
        let result = defaultMatrix matrix
        Expect.equal result [[SPLiteral (LInt 1)]] "wildcard row kept, first column removed"
    }

    test "defaultMatrix removes constructor row" {
        // Row: [Some x, y]
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard]); SPLiteral (LInt 1)]
        ]
        let result = defaultMatrix matrix
        Expect.isEmpty result "constructor row removed"
    }

    test "defaultMatrix removes literal row" {
        // Row: [1, y]
        let matrix: PatternMatrix = [
            [SPLiteral (LInt 1); SPWildcard]
        ]
        let result = defaultMatrix matrix
        Expect.isEmpty result "literal row removed"
    }

    test "defaultMatrix with multiple rows" {
        // Row 1: [Some x, y] → removed
        // Row 2: [_, z]      → [z]
        // Row 3: [None, w]   → removed
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard]); SPLiteral (LInt 1)]
            [SPWildcard; SPLiteral (LInt 2)]
            [SPConstructor ("None", []); SPLiteral (LInt 3)]
        ]
        let result = defaultMatrix matrix
        Expect.equal result [[SPLiteral (LInt 2)]] "only wildcard rows kept"
    }

    test "defaultMatrix with empty row returns None" {
        // Row: [] (shouldn't happen in practice)
        let matrix: PatternMatrix = [[]]
        let result = defaultMatrix matrix
        Expect.isEmpty result "empty row removed"
    }
]

// =============================================================================
// Phase 9.1.3: isUseful Algorithm Tests
// =============================================================================

// Helper: Create Option type registry
let optionRegistry =
    let typeDef: TypeDef = {
        Name = "Option"
        TypeParams = ["a"]
        Constructors = [("None", None); ("Some", Some (TEVar "a"))]
    }
    TypeDefRegistryBuilder.buildTypeDefRegistry [typeDef]

let isUsefulTests = testList "isUseful" [

    // -------------------------------------------------------------------------
    // Base cases
    // -------------------------------------------------------------------------

    test "empty matrix: any vector is useful" {
        let matrix: PatternMatrix = []
        let vector = [SPWildcard]
        let result = isUseful matrix vector Map.empty [TInt]
        Expect.isTrue result "empty matrix means vector is useful"
    }

    test "empty vector against non-empty matrix: not useful" {
        // This case represents: we've matched all columns, but there are patterns
        let matrix: PatternMatrix = [[]]  // Row with no columns (already matched)
        let vector: PatternVector = []
        let result = isUseful matrix vector Map.empty []
        Expect.isFalse result "empty vector against matched patterns is not useful"
    }

    // -------------------------------------------------------------------------
    // Wildcard vector cases
    // -------------------------------------------------------------------------

    test "wildcard useful against complete Option coverage" {
        // Matrix covers: None, Some _
        // Checking if _ is useful → No, all cases covered
        let matrix: PatternMatrix = [
            [SPConstructor ("None", [])]
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = isUseful matrix [SPWildcard] optionRegistry [optionType]
        Expect.isFalse result "wildcard not useful when all constructors covered"
    }

    test "wildcard useful against incomplete Option coverage" {
        // Matrix covers: Some _
        // Checking if _ is useful → Yes, None is not covered
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = isUseful matrix [SPWildcard] optionRegistry [optionType]
        Expect.isTrue result "wildcard useful when Some but no None"
    }

    test "wildcard useful against complete bool coverage" {
        // Matrix covers: true, false
        let matrix: PatternMatrix = [
            [SPLiteral (LBool true)]
            [SPLiteral (LBool false)]
        ]
        let result = isUseful matrix [SPWildcard] Map.empty [TBool]
        Expect.isFalse result "wildcard not useful when both bools covered"
    }

    test "wildcard useful when only true covered" {
        let matrix: PatternMatrix = [
            [SPLiteral (LBool true)]
        ]
        let result = isUseful matrix [SPWildcard] Map.empty [TBool]
        Expect.isTrue result "wildcard useful when false not covered"
    }

    // -------------------------------------------------------------------------
    // Constructor vector cases
    // -------------------------------------------------------------------------

    test "constructor useful when not in matrix" {
        // Matrix: None
        // Vector: Some _
        let matrix: PatternMatrix = [
            [SPConstructor ("None", [])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = isUseful matrix [SPConstructor ("Some", [SPWildcard])] optionRegistry [optionType]
        Expect.isTrue result "Some is useful when only None in matrix"
    }

    test "constructor not useful when already in matrix" {
        // Matrix: Some _
        // Vector: Some _
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = isUseful matrix [SPConstructor ("Some", [SPWildcard])] optionRegistry [optionType]
        Expect.isFalse result "Some not useful when Some already in matrix"
    }

    // -------------------------------------------------------------------------
    // Literal cases
    // -------------------------------------------------------------------------

    test "literal useful when not covered" {
        // Matrix: 1
        // Vector: 2
        let matrix: PatternMatrix = [
            [SPLiteral (LInt 1)]
        ]
        let result = isUseful matrix [SPLiteral (LInt 2)] Map.empty [TInt]
        Expect.isTrue result "different literal is useful"
    }

    test "literal not useful when covered by wildcard" {
        // Matrix: _
        // Vector: 42
        let matrix: PatternMatrix = [
            [SPWildcard]
        ]
        let result = isUseful matrix [SPLiteral (LInt 42)] Map.empty [TInt]
        Expect.isFalse result "literal covered by wildcard"
    }

    // -------------------------------------------------------------------------
    // List patterns
    // -------------------------------------------------------------------------

    test "cons useful when only empty list in matrix" {
        // Matrix: []
        // Vector: _ :: _
        let matrix: PatternMatrix = [
            [SPConstructor ("[]", [])]
        ]
        let result = isUseful matrix [SPConstructor ("::", [SPWildcard; SPWildcard])] Map.empty [TList TInt]
        Expect.isTrue result "cons useful when only [] in matrix"
    }

    test "empty list not useful when wildcard covers" {
        // Matrix: _
        // Vector: []
        let matrix: PatternMatrix = [
            [SPWildcard]
        ]
        let result = isUseful matrix [SPConstructor ("[]", [])] Map.empty [TList TInt]
        Expect.isFalse result "[] covered by wildcard"
    }

    // -------------------------------------------------------------------------
    // Multi-column cases
    // -------------------------------------------------------------------------

    test "multi-column: second column matters" {
        // Matrix: [Some _, true]
        // Vector: [Some _, false]
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard]); SPLiteral (LBool true)]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = isUseful matrix [SPConstructor ("Some", [SPWildcard]); SPLiteral (LBool false)] optionRegistry [optionType; TBool]
        Expect.isTrue result "false is useful when only true covered in second column"
    }
]

// =============================================================================
// Phase 9.1.4: findMissing Tests
// =============================================================================

let findMissingTests = testList "findMissing" [

    test "finds None when only Some covered" {
        // Matrix: Some _
        // Missing: None
        let matrix: PatternMatrix = [
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = findMissing matrix optionRegistry [optionType]
        Expect.isSome result "should find missing pattern"
        let missing = Option.get result
        Expect.equal missing [SPConstructor ("None", [])] "missing should be None"
    }

    test "finds Some when only None covered" {
        // Matrix: None
        // Missing: Some _
        let matrix: PatternMatrix = [
            [SPConstructor ("None", [])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = findMissing matrix optionRegistry [optionType]
        Expect.isSome result "should find missing pattern"
        let missing = Option.get result
        Expect.equal missing [SPConstructor ("Some", [SPWildcard])] "missing should be Some _"
    }

    test "no missing when Option fully covered" {
        // Matrix: None, Some _
        let matrix: PatternMatrix = [
            [SPConstructor ("None", [])]
            [SPConstructor ("Some", [SPWildcard])]
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = findMissing matrix optionRegistry [optionType]
        Expect.isNone result "should not find missing pattern"
    }

    test "finds false when only true covered" {
        let matrix: PatternMatrix = [
            [SPLiteral (LBool true)]
        ]
        let result = findMissing matrix Map.empty [TBool]
        Expect.isSome result "should find missing"
        let missing = Option.get result
        Expect.equal missing [SPLiteral (LBool false)] "missing should be false"
    }

    test "finds cons when only empty list covered" {
        let matrix: PatternMatrix = [
            [SPConstructor ("[]", [])]
        ]
        let result = findMissing matrix Map.empty [TList TInt]
        Expect.isSome result "should find missing"
        let missing = Option.get result
        Expect.equal missing [SPConstructor ("::", [SPWildcard; SPWildcard])] "missing should be _ :: _"
    }

    test "empty matrix returns wildcard" {
        let matrix: PatternMatrix = []
        let result = findMissing matrix Map.empty [TInt]
        Expect.isSome result "should find missing"
        let missing = Option.get result
        Expect.equal missing [SPWildcard] "missing should be _"
    }

    test "no missing when wildcard covers all" {
        let matrix: PatternMatrix = [
            [SPWildcard]
        ]
        let result = findMissing matrix Map.empty [TInt]
        Expect.isNone result "wildcard covers all"
    }
]

// =============================================================================
// Phase 9.2: checkRedundancy Tests
// =============================================================================

let checkRedundancyTests = testList "checkRedundancy" [

    test "no redundancy when patterns are unique" {
        // None, Some _
        let patterns = [
            locP (PConstructor ("None", None))
            locP (PConstructor ("Some", Some (locP PWildcard)))
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = checkRedundancy patterns optionRegistry optionType
        Expect.isEmpty result "no redundant patterns"
    }

    test "detects duplicate constructor" {
        // None, None
        let patterns = [
            locP (PConstructor ("None", None))
            locP (PConstructor ("None", None))
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = checkRedundancy patterns optionRegistry optionType
        Expect.equal result [1] "second None is redundant"
    }

    test "detects pattern after wildcard" {
        // _, None
        let patterns = [
            locP PWildcard
            locP (PConstructor ("None", None))
        ]
        let optionType = TConstructor ("Option", [TInt])
        let result = checkRedundancy patterns optionRegistry optionType
        Expect.equal result [1] "None after wildcard is redundant"
    }

    test "detects duplicate literal" {
        // true, true
        let patterns = [
            locP (PLiteral (LBool true))
            locP (PLiteral (LBool true))
        ]
        let result = checkRedundancy patterns Map.empty TBool
        Expect.equal result [1] "second true is redundant"
    }

    test "no redundancy for different literals" {
        // true, false
        let patterns = [
            locP (PLiteral (LBool true))
            locP (PLiteral (LBool false))
        ]
        let result = checkRedundancy patterns Map.empty TBool
        Expect.isEmpty result "true and false are not redundant"
    }
]

// =============================================================================
// Phase 9.1.5: analyzeMatch Tests
// =============================================================================

let analyzeMatchTests = testList "analyzeMatch" [

    test "analyzeMatch finds missing None" {
        let cases = [
            (locP (PConstructor ("Some", Some (locP PWildcard))), None, Located.noLoc (ELiteral (LInt 1)))
        ]
        let optionType = TConstructor ("Option", [TInt])
        let pos = { Line = 1; Column = 1; File = None }
        let warnings = analyzeMatch optionType cases optionRegistry pos
        Expect.hasLength warnings 1 "should have one warning"
        match List.head warnings with
        | NonExhaustive (missing, _) ->
            Expect.isTrue (List.contains "None" missing) "should report None missing"
        | _ -> failtest "expected NonExhaustive warning"
    }

    test "analyzeMatch finds redundant pattern" {
        let cases = [
            (locP (PConstructor ("None", None)), None, Located.noLoc (ELiteral (LInt 0)))
            (locP (PConstructor ("Some", Some (locP PWildcard))), None, Located.noLoc (ELiteral (LInt 1)))
            (locP (PConstructor ("None", None)), None, Located.noLoc (ELiteral (LInt 2)))  // Redundant
        ]
        let optionType = TConstructor ("Option", [TInt])
        let pos = { Line = 1; Column = 1; File = None }
        let warnings = analyzeMatch optionType cases optionRegistry pos
        Expect.hasLength warnings 1 "should have one warning"
        match List.head warnings with
        | RedundantPattern (idx, _) -> Expect.equal idx 2 "third pattern is redundant"
        | _ -> failtest "expected RedundantPattern warning"
    }

    test "analyzeMatch no warnings for complete match" {
        let cases = [
            (locP (PConstructor ("None", None)), None, Located.noLoc (ELiteral (LInt 0)))
            (locP (PConstructor ("Some", Some (locP PWildcard))), None, Located.noLoc (ELiteral (LInt 1)))
        ]
        let optionType = TConstructor ("Option", [TInt])
        let pos = { Line = 1; Column = 1; File = None }
        let warnings = analyzeMatch optionType cases optionRegistry pos
        Expect.isEmpty warnings "should have no warnings"
    }
]

// =============================================================================
// All Tests
// =============================================================================

// =============================================================================
// Phase 9.3: TypeInfer Integration Tests
// =============================================================================

open FunLang.TypeInfer

let typeInferIntegrationTests = testList "TypeInfer Integration" [

    test "inferTypeWithWarnings detects missing bool case" {
        // match true with | true -> 1
        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (ELiteral (LBool true)),
            [(locP (PLiteral (LBool true)), None, Located.noLoc (ELiteral (LInt 1)))]
        ))
        let result = inferTypeWithWarnings Map.empty Map.empty matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            Expect.hasLength warnings 1 "should have one warning"
            match List.head warnings with
            | NonExhaustive (missing, _) ->
                Expect.isTrue (List.contains "false" missing) "should report false missing"
            | _ -> failtest "expected NonExhaustive warning"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }

    test "inferTypeWithWarnings detects redundant bool case" {
        // match true with | true -> 1 | false -> 2 | true -> 3
        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (ELiteral (LBool true)),
            [
                (locP (PLiteral (LBool true)), None, Located.noLoc (ELiteral (LInt 1)))
                (locP (PLiteral (LBool false)), None, Located.noLoc (ELiteral (LInt 2)))
                (locP (PLiteral (LBool true)), None, Located.noLoc (ELiteral (LInt 3)))
            ]
        ))
        let result = inferTypeWithWarnings Map.empty Map.empty matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            Expect.hasLength warnings 1 "should have one warning"
            match List.head warnings with
            | RedundantPattern (idx, _) -> Expect.equal idx 2 "third pattern is redundant"
            | _ -> failtest "expected RedundantPattern warning"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }

    test "inferTypeWithWarnings no warnings for complete bool match" {
        // match true with | true -> 1 | false -> 2
        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (ELiteral (LBool true)),
            [
                (locP (PLiteral (LBool true)), None, Located.noLoc (ELiteral (LInt 1)))
                (locP (PLiteral (LBool false)), None, Located.noLoc (ELiteral (LInt 2)))
            ]
        ))
        let result = inferTypeWithWarnings Map.empty Map.empty matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            Expect.isEmpty warnings "should have no warnings"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }

    test "inferTypeWithWarnings detects missing list case" {
        // match [1] with | x :: xs -> 1
        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (EList [Located.noLoc (ELiteral (LInt 1))]),
            [(locP (PCons (locP (PVariable "x"), locP (PVariable "xs"))), None, Located.noLoc (ELiteral (LInt 1)))]
        ))
        let result = inferTypeWithWarnings Map.empty Map.empty matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            Expect.hasLength warnings 1 "should have one warning"
            match List.head warnings with
            | NonExhaustive (missing, _) ->
                Expect.isTrue (List.contains "[]" missing) "should report [] missing"
            | _ -> failtest "expected NonExhaustive warning"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }

    test "inferTypeWithWarnings with user-defined type" {
        // type Option 'a = None | Some of 'a
        // match Some 1 with | Some x -> x
        let typeDef: FunLang.Ast.TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefs = [typeDef]
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv typeDefs
        let registry = TypeDefRegistryBuilder.buildTypeDefRegistry typeDefs

        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (EConstructor ("Some", Some (Located.noLoc (ELiteral (LInt 1))))),
            [(locP (PConstructor ("Some", Some (locP (PVariable "x")))), None, Located.noLoc (EVariable "x"))]
        ))
        let result = inferTypeWithWarnings typeDefEnv registry matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            Expect.hasLength warnings 1 "should have one warning"
            match List.head warnings with
            | NonExhaustive (missing, _) ->
                Expect.isTrue (List.contains "None" missing) "should report None missing"
            | _ -> failtest "expected NonExhaustive warning"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }

    test "inferTypeWithWarnings nested match detects all warnings" {
        // match (true, false) with
        // | (true, true) -> 1
        // | (false, _) -> 2
        let matchExpr = Located.noLoc (EMatch (
            Located.noLoc (ETuple [
                Located.noLoc (ELiteral (LBool true))
                Located.noLoc (ELiteral (LBool false))
            ]),
            [
                (locP (PTuple [locP (PLiteral (LBool true)); locP (PLiteral (LBool true))]), None, Located.noLoc (ELiteral (LInt 1)))
                (locP (PTuple [locP (PLiteral (LBool false)); locP PWildcard]), None, Located.noLoc (ELiteral (LInt 2)))
            ]
        ))
        let result = inferTypeWithWarnings Map.empty Map.empty matchExpr
        match result with
        | Ok (t, warnings) ->
            Expect.equal t TInt "result type should be int"
            // Missing: (true, false)
            Expect.hasLength warnings 1 "should have one warning for missing case"
        | Error e -> failtest (sprintf "type inference failed: %A" e)
    }
]

[<Tests>]
let tests = testList "Pattern Analysis" [
    typeDefRegistryTests
    simplePatternTests
    patternToStringTests
    matrixOperationsTests
    isUsefulTests
    findMissingTests
    checkRedundancyTests
    analyzeMatchTests
    typeInferIntegrationTests
]
