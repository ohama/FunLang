module MatchCompile

open Ast

/// Fresh variable identifier for sub-values during compilation
type TestVar = int


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
    | ConstPat(StringConst s, _) -> Some("#str_" + s, 0)
    | ConstPat(CharConst c, _) -> Some("#char_" + string (int c), 0)
    | OrPat _ -> None  // OrPat should be expanded before reaching here
    | RecordPat(fields, _) ->
        let fieldNames = fields |> List.map fst |> List.sort |> String.concat ","
        Some("#record:" + fieldNames, List.length fields)
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
    | OrPat _ -> []  // OrPat should be expanded before reaching here
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

// ============================================================
// 6. compile: main recursive algorithm (Jacobs Section 3)
// ============================================================
let rec compile (freshTestVar: unit -> TestVar) (clauses: MatchRow list) : DecisionTree =
    match clauses with
    | [] -> Fail
    | _ ->
        // Step 1: Push variable bindings
        let clauses' = clauses |> List.map pushVarBindings
        let first = clauses'.Head

        // Base case: first clause has no remaining constructor tests
        if Map.isEmpty first.Patterns then
            let fallback =
                if clauses'.Tail.IsEmpty then None
                else Some(compile freshTestVar clauses'.Tail)
            Leaf(first.Bindings, first.Guard, first.Body, fallback)
        else
            // Step 2: Select test variable (heuristic)
            let testVar = selectTestVariable first clauses'
            let pat = Map.find testVar first.Patterns
            let ctorName, arity =
                match patternToConstructor pat with
                | Some(n, a) -> (n, a)
                | None -> failwith "Expected constructor pattern after pushVarBindings"
            let freshVars = List.init arity (fun _ -> freshTestVar())

            // Steps 3-4: Split and generate Switch
            let yesClauses, noClauses = splitClauses testVar ctorName freshVars clauses'
            Switch(testVar, ctorName, freshVars, compile freshTestVar yesClauses, compile freshTestVar noClauses)

// ============================================================
// 7. matchesConstructor: test if runtime Value matches a constructor name
// ============================================================
let matchesConstructor (value: Value) (ctor: string) : bool =
    match value, ctor with
    | DataValue(name, _), c -> name = c
    | ListValue [], "[]" -> true
    | ListValue(_ :: _), "::" -> true
    | IntValue n, c when c.StartsWith("#int_") ->
        string n = c.Substring(5)
    | BoolValue b, c when c.StartsWith("#bool_") ->
        (string b).ToLower() = c.Substring(6)
    | StringValue s, c when c.StartsWith("#str_") ->
        s = c.Substring(5)
    | CharValue c2, c when c.StartsWith("#char_") ->
        string (int c2) = c.Substring(6)
    | TupleValue _, c when c.StartsWith("#tuple_") -> true
    | RecordValue _, c when c.StartsWith("#record:") -> true
    | _ -> false

// ============================================================
// 8. destructureValue: extract sub-values after constructor match
// ============================================================
let destructureValue (ctor: string) (value: Value) : Value list =
    match value, ctor with
    | DataValue(_, None), _ -> []
    | DataValue(_, Some arg), _ -> [arg]
    | ListValue(h :: t), "::" -> [h; ListValue t]
    | ListValue [], "[]" -> []
    | TupleValue vals, _ -> vals
    | RecordValue(_, fields), c when c.StartsWith("#record:") ->
        let fieldNames = c.Substring(8).Split(',') |> Array.toList
        fieldNames |> List.map (fun name -> !(Map.find name fields))
    | _ -> []

// ============================================================
// 9. evalDecisionTree: walk the tree at runtime
// ============================================================
/// Phase 15: evalFn callback takes tailPos: bool to thread tail position through match clause bodies
let evalDecisionTree (evalFn: Env -> bool -> Expr -> Value) (env: Env) (tailPos: bool) (varEnv: Map<TestVar, Value>) (tree: DecisionTree) : Value =
    let rec walk (env: Env) (varEnv: Map<TestVar, Value>) (tree: DecisionTree) : Value =
        match tree with
        | Fail -> failwith "Match failure: no pattern matched"
        | Leaf(bindings, guard, body, fallback) ->
            let bodyEnv =
                bindings |> Map.fold (fun e name tv ->
                    Map.add name (Map.find tv varEnv) e) env
            match guard with
            | None -> evalFn bodyEnv tailPos body
            | Some guardExpr ->
                match evalFn bodyEnv false guardExpr with
                | BoolValue true -> evalFn bodyEnv tailPos body
                | _ ->
                    match fallback with
                    | Some fb -> walk env varEnv fb
                    | None -> failwith "Match failure: no pattern matched"
        | Switch(testVar, ctorName, argVars, ifMatch, ifNoMatch) ->
            let testValue = Map.find testVar varEnv
            if matchesConstructor testValue ctorName then
                let subValues = destructureValue ctorName testValue
                let varEnv' =
                    List.zip argVars subValues
                    |> List.fold (fun acc (tv, v) -> Map.add tv v acc) varEnv
                walk env varEnv' ifMatch
            else
                walk env varEnv ifNoMatch
    walk env varEnv tree

// ============================================================
// 10. compileMatch: entry point, converts MatchClause list to DecisionTree
// ============================================================
/// Expand or-patterns at the top level: OrPat([p1; p2; p3]) becomes three clauses
let expandOrPatterns (clauses: MatchClause list) : MatchClause list =
    clauses |> List.collect (fun (pat, guard, body) ->
        match pat with
        | OrPat(pats, _) -> pats |> List.map (fun p -> (p, guard, body))
        | _ -> [(pat, guard, body)])

let compileMatch (clauses: MatchClause list) : DecisionTree * TestVar =
    let mutable nextVar = 0
    let freshTestVar () =
        let v = nextVar
        nextVar <- nextVar + 1
        v
    let rootVar = freshTestVar()
    let expandedClauses = expandOrPatterns clauses
    let rows =
        expandedClauses |> List.mapi (fun i (pattern, guard, body) ->
            { Patterns = Map.ofList [(rootVar, pattern)]
              Guard = guard
              Body = body
              Bindings = Map.empty
              OriginalIndex = i })
    let tree = compile freshTestVar rows
    (tree, rootVar)
