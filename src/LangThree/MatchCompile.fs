module MatchCompile

open Ast

/// Fresh variable identifier for sub-values during compilation
type TestVar = int

/// Mutable counter for fresh variable generation
let mutable private nextTestVar = 0

let freshTestVar () =
    let v = nextTestVar
    nextTestVar <- nextTestVar + 1
    v

let resetTestVarCounter () = nextTestVar <- 0

/// A clause under compilation
type MatchRow = {
    Patterns: Map<TestVar, Pattern>   // variable -> pattern constraint
    Guard: Expr option                // optional when guard
    Body: Expr                        // right-hand side
    Bindings: Map<string, TestVar>    // pattern var name -> test variable
    OriginalIndex: int                // for redundancy tracking
}

/// Decision tree: the compiled form of pattern matching
type DecisionTree =
    /// Leaf: match succeeded, apply bindings and evaluate body
    | Leaf of bindings: Map<string, TestVar> * guard: Expr option
            * body: Expr * fallback: DecisionTree option
    /// Switch: test one variable against one constructor, binary branch
    | Switch of testVar: TestVar * constructorName: string
              * argVars: TestVar list * ifMatch: DecisionTree * ifNoMatch: DecisionTree
    /// Fail: non-exhaustive match failure
    | Fail

// ============================================================
// 1. patternToConstructor: map pattern to constructor name + arity
// ============================================================
let patternToConstructor (pat: Pattern) : (string * int) option =
    match pat with
    | ConstructorPat(name, None, _) -> Some(name, 0)
    | ConstructorPat(name, Some _, _) -> Some(name, 1)
    | TuplePat(pats, _) -> Some("#tuple_" + string(List.length pats), List.length pats)
    | ConsPat _ -> Some("::", 2)
    | EmptyListPat _ -> Some("[]", 0)
    | ConstPat(IntConst n, _) -> Some("#int_" + string n, 0)
    | ConstPat(BoolConst b, _) -> Some("#bool_" + (string b).ToLower(), 0)
    | RecordPat(fields, _) -> Some("#record_" + string(List.length fields), List.length fields)
    | VarPat _ | WildcardPat _ -> None

// ============================================================
// 2. extractSubPatterns: get sub-patterns from a constructor pattern
// ============================================================
let extractSubPatterns (pat: Pattern) : Pattern list =
    match pat with
    | ConstructorPat(_, None, _) -> []
    | ConstructorPat(_, Some arg, _) -> [arg]
    | TuplePat(pats, _) -> pats
    | ConsPat(h, t, _) -> [h; t]
    | EmptyListPat _ -> []
    | ConstPat _ -> []
    | RecordPat(fields, _) ->
        fields |> List.sortBy fst |> List.map snd
    | VarPat _ | WildcardPat _ -> []

// ============================================================
// 3. pushVarBindings: move VarPat/WildcardPat from Patterns to Bindings
// ============================================================
let pushVarBindings (row: MatchRow) : MatchRow =
    let mutable patterns = row.Patterns
    let mutable bindings = row.Bindings
    for KeyValue(tv, pat) in row.Patterns do
        match pat with
        | VarPat(name, _) ->
            patterns <- Map.remove tv patterns
            bindings <- Map.add name tv bindings
        | WildcardPat _ ->
            patterns <- Map.remove tv patterns
        | _ -> ()
    { row with Patterns = patterns; Bindings = bindings }

// ============================================================
// 4. selectTestVariable: heuristic - pick var in most clauses
// ============================================================
let selectTestVariable (first: MatchRow) (allClauses: MatchRow list) : TestVar =
    first.Patterns
    |> Map.toSeq |> Seq.map fst
    |> Seq.maxBy (fun tv ->
        let count = allClauses |> List.sumBy (fun c ->
            if Map.containsKey tv c.Patterns then 1 else 0)
        (count, -tv))  // maximize count, then minimize tv for tie-breaking

// ============================================================
// 5. splitClauses: cases a/b/c from Jacobs Section 3
// ============================================================
let splitClauses (testVar: TestVar) (ctorName: string) (freshVars: TestVar list) (clauses: MatchRow list) : MatchRow list * MatchRow list =
    let yesClauses = ResizeArray()
    let noClauses = ResizeArray()
    for clause in clauses do
        match Map.tryFind testVar clause.Patterns with
        | Some pat ->
            match patternToConstructor pat with
            | Some(name, _) when name = ctorName ->
                // Case (a): same constructor, expand sub-patterns
                let subPats = extractSubPatterns pat
                let newPatterns = clause.Patterns |> Map.remove testVar
                let expanded =
                    List.zip freshVars subPats
                    |> List.fold (fun acc (fv, sp) -> Map.add fv sp acc) newPatterns
                yesClauses.Add({ clause with Patterns = expanded })
            | Some _ ->
                // Case (b): different constructor, NO branch only
                noClauses.Add(clause)
            | None ->
                // VarPat/WildcardPat still here (defensive): treat as case (c)
                yesClauses.Add(clause)
                noClauses.Add(clause)
        | None ->
            // Case (c): no test for this variable, add to BOTH
            yesClauses.Add(clause)
            noClauses.Add(clause)
    (Seq.toList yesClauses, Seq.toList noClauses)
