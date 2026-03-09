module Eval

open Ast
open Type

/// Empty environment for top-level evaluation
/// Phase 5: Env type now defined in Ast.fs for mutual recursion with Value
let emptyEnv : Env = Map.empty

/// Format a value for user-friendly output
let rec formatValue (v: Value) : string =
    match v with
    | IntValue n -> string n
    | BoolValue b -> if b then "true" else "false"
    | FunctionValue _ -> "<function>"
    | StringValue s -> sprintf "\"%s\"" s
    | TupleValue values ->
        let formattedElements = List.map formatValue values
        sprintf "(%s)" (String.concat ", " formattedElements)
    | ListValue values ->
        let formattedElements = List.map formatValue values
        sprintf "[%s]" (String.concat ", " formattedElements)
    | DataValue (name, None) -> name
    | DataValue (name, Some v) ->
        let argStr = formatValue v
        // Wrap compound values in parens for clarity
        let needsParens = match v with DataValue (_, Some _) | TupleValue _ -> true | _ -> false
        if needsParens then sprintf "%s (%s)" name argStr
        else sprintf "%s %s" name argStr
    | RecordValue (_typeName, fields) ->
        let fieldStrs =
            fields
            |> Map.toList
            |> List.map (fun (name, valueRef) -> sprintf "%s = %s" name (formatValue !valueRef))
        sprintf "{ %s }" (String.concat "; " fieldStrs)

/// Resolve record type name from field names using RecordEnv
let resolveRecordTypeName (recEnv: RecordEnv) (fieldNames: Set<string>) : string =
    recEnv
    |> Map.tryPick (fun typeName info ->
        let declFields = info.Fields |> List.map (fun f -> f.Name) |> Set.ofList
        if fieldNames = declFields then Some typeName else None)
    |> Option.defaultValue ""

/// Match a pattern against a value, returning bindings if successful
let rec matchPattern (pat: Pattern) (value: Value) : (string * Value) list option =
    match pat, value with
    | VarPat (name, _), v -> Some [(name, v)]
    | WildcardPat _, _ -> Some []
    | TuplePat (pats, _), TupleValue vals ->
        if List.length pats <> List.length vals then
            None  // Arity mismatch
        else
            let bindings = List.map2 matchPattern pats vals
            if List.forall Option.isSome bindings then
                Some (List.collect Option.get bindings)
            else
                None
    // Constant patterns
    | ConstPat (IntConst n, _), IntValue m ->
        if n = m then Some [] else None
    | ConstPat (BoolConst b1, _), BoolValue b2 ->
        if b1 = b2 then Some [] else None
    // Empty list pattern
    | EmptyListPat _, ListValue [] -> Some []
    // Cons pattern - matches non-empty list
    | ConsPat (headPat, tailPat, _), ListValue (h :: t) ->
        match matchPattern headPat h with
        | Some headBindings ->
            match matchPattern tailPat (ListValue t) with
            | Some tailBindings -> Some (headBindings @ tailBindings)
            | None -> None
        | None -> None
    // Phase 2 (ADT): Constructor pattern matching
    | ConstructorPat (name, argPatOpt, _), DataValue (ctorName, argValOpt) ->
        if name = ctorName then
            match argPatOpt, argValOpt with
            | None, None -> Some []  // Nullary constructor
            | Some argPat, Some argVal -> matchPattern argPat argVal  // Constructor with argument
            | _ -> None  // Arity mismatch
        else
            None  // Different constructor
    // Phase 3 (Records): Record pattern matching (partial field patterns)
    | RecordPat (fieldPats, _), RecordValue (_, fields) ->
        let bindings =
            fieldPats
            |> List.map (fun (fieldName, pat) ->
                match Map.tryFind fieldName fields with
                | Some valueRef -> matchPattern pat !valueRef
                | None -> None)
        if List.forall Option.isSome bindings then
            Some (List.collect Option.get bindings)
        else
            None
    | _ -> None  // Type mismatch (e.g., TuplePat vs IntValue)

/// Evaluate match clauses sequentially, returning first match
and evalMatchClauses (recEnv: RecordEnv) (env: Env) (scrutinee: Value) (clauses: MatchClause list) : Value =
    match clauses with
    | [] -> failwith "Match failure: no pattern matched"
    | (pattern, resultExpr) :: rest ->
        match matchPattern pattern scrutinee with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            eval recEnv extendedEnv resultExpr
        | None ->
            evalMatchClauses recEnv env scrutinee rest

/// Evaluate an expression in an environment
/// Returns Value (IntValue, BoolValue, or FunctionValue)
/// Raises exception for type errors and undefined variables
and eval (recEnv: RecordEnv) (env: Env) (expr: Expr) : Value =
    match expr with
    | Number (n, _) -> IntValue n
    | Bool (b, _) -> BoolValue b
    | String (s, _) -> StringValue s

    | Var (name, _) ->
        match Map.tryFind name env with
        | Some value -> value
        | None -> failwithf "Undefined variable: %s" name

    | Let (name, binding, body, _) ->
        let value = eval recEnv env binding
        let extendedEnv = Map.add name value env
        eval recEnv extendedEnv body

    // Phase 1 (v3.0): Tuples
    | Tuple (exprs, _) ->
        let values = List.map (eval recEnv env) exprs
        TupleValue values

    | LetPat (pat, bindingExpr, bodyExpr, _) ->
        let value = eval recEnv env bindingExpr
        match matchPattern pat value with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            eval recEnv extendedEnv bodyExpr
        | None ->
            match pat, value with
            | TuplePat (pats, _), TupleValue vals ->
                failwithf "Pattern match failed: tuple pattern expects %d elements but value has %d"
                          (List.length pats) (List.length vals)
            | TuplePat _, _ ->
                failwith "Pattern match failed: expected tuple value"
            | _ ->
                failwith "Pattern match failed"

    // Phase 3 (v3.0): Pattern Matching
    | Match (scrutinee, clauses, _) ->
        let value = eval recEnv env scrutinee
        evalMatchClauses recEnv env value clauses

    // Phase 2 (v3.0): Lists
    | EmptyList _ ->
        ListValue []

    | List (exprs, _) ->
        let values = List.map (eval recEnv env) exprs
        ListValue values

    | Cons (headExpr, tailExpr, _) ->
        let headVal = eval recEnv env headExpr
        match eval recEnv env tailExpr with
        | ListValue tailVals -> ListValue (headVal :: tailVals)
        | _ -> failwith "Type error: cons (::) requires list as second argument"

    // Arithmetic operations - type check for IntValue
    | Add (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> IntValue (l + r)
        | StringValue l, StringValue r -> StringValue (l + r)
        | _ -> failwith "Type error: + requires operands of same type (int or string)"

    | Subtract (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> IntValue (l - r)
        | _ -> failwith "Type error: - requires integer operands"

    | Multiply (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> IntValue (l * r)
        | _ -> failwith "Type error: * requires integer operands"

    | Divide (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> IntValue (l / r)
        | _ -> failwith "Type error: / requires integer operands"

    | Negate (e, _) ->
        match eval recEnv env e with
        | IntValue n -> IntValue (-n)
        | _ -> failwith "Type error: unary - requires integer operand"

    // Comparison operators - type check for IntValue, return BoolValue
    | LessThan (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l < r)
        | _ -> failwith "Type error: < requires integer operands"

    | GreaterThan (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l > r)
        | _ -> failwith "Type error: > requires integer operands"

    | LessEqual (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l <= r)
        | _ -> failwith "Type error: <= requires integer operands"

    | GreaterEqual (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l >= r)
        | _ -> failwith "Type error: >= requires integer operands"

    // Equal and NotEqual work on both int and bool (same type required)
    | Equal (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l = r)
        | BoolValue l, BoolValue r -> BoolValue (l = r)
        | StringValue l, StringValue r -> BoolValue (l = r)
        | TupleValue l, TupleValue r -> BoolValue (l = r)  // Structural equality
        | ListValue l, ListValue r -> BoolValue (l = r)
        | RecordValue (t1, f1), RecordValue (t2, f2) ->
            let v1 = f1 |> Map.map (fun _ r -> !r)
            let v2 = f2 |> Map.map (fun _ r -> !r)
            BoolValue (t1 = t2 && v1 = v2)
        | _ -> failwith "Type error: = requires operands of same type"

    | NotEqual (left, right, _) ->
        match eval recEnv env left, eval recEnv env right with
        | IntValue l, IntValue r -> BoolValue (l <> r)
        | BoolValue l, BoolValue r -> BoolValue (l <> r)
        | StringValue l, StringValue r -> BoolValue (l <> r)
        | TupleValue l, TupleValue r -> BoolValue (l <> r)  // Structural inequality
        | ListValue l, ListValue r -> BoolValue (l <> r)
        | RecordValue (t1, f1), RecordValue (t2, f2) ->
            let v1 = f1 |> Map.map (fun _ r -> !r)
            let v2 = f2 |> Map.map (fun _ r -> !r)
            BoolValue (t1 <> t2 || v1 <> v2)
        | _ -> failwith "Type error: <> requires operands of same type"

    // Logical operators - short-circuit evaluation
    | And (left, right, _) ->
        match eval recEnv env left with
        | BoolValue false -> BoolValue false
        | BoolValue true ->
            match eval recEnv env right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: && requires boolean operands"
        | _ -> failwith "Type error: && requires boolean operands"

    | Or (left, right, _) ->
        match eval recEnv env left with
        | BoolValue true -> BoolValue true
        | BoolValue false ->
            match eval recEnv env right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: || requires boolean operands"
        | _ -> failwith "Type error: || requires boolean operands"

    // If-then-else - condition must be boolean
    | If (condition, thenBranch, elseBranch, _) ->
        match eval recEnv env condition with
        | BoolValue true -> eval recEnv env thenBranch
        | BoolValue false -> eval recEnv env elseBranch
        | _ -> failwith "Type error: if condition must be boolean"

    // Phase 2 (ADT): Constructor evaluation
    | Constructor (name, argOpt, _) ->
        let argValue = argOpt |> Option.map (eval recEnv env)
        DataValue (name, argValue)

    // v6.0: Type annotations - erased at runtime
    | Annot (expr, _, _) ->
        eval recEnv env expr  // Just evaluate the underlying expression

    | LambdaAnnot (param, _, body, _) ->
        FunctionValue (param, body, env)  // Same as regular lambda at runtime

    // Phase 5: Functions

    // Lambda creates a closure capturing current environment
    | Lambda (param, body, _) ->
        FunctionValue (param, body, env)

    // Function application
    | App (funcExpr, argExpr, _) ->
        let funcVal = eval recEnv env funcExpr
        match funcVal with
        | FunctionValue (param, body, closureEnv) ->
            let argValue = eval recEnv env argExpr
            // For recursive functions: when calling by name, add self to closure
            // This enables recursion by ensuring the function can find itself
            let augmentedClosureEnv =
                match funcExpr with
                | Var (name, _) -> Map.add name funcVal closureEnv
                | _ -> closureEnv
            let callEnv = Map.add param argValue augmentedClosureEnv
            eval recEnv callEnv body
        | _ -> failwith "Type error: attempted to call non-function"

    // Let rec - recursive function definition
    // Creates a function whose closure will be augmented at call time (in App)
    | LetRec (name, param, funcBody, inExpr, _) ->
        let funcVal = FunctionValue (param, funcBody, env)
        let recFuncEnv = Map.add name funcVal env
        eval recEnv recFuncEnv inExpr

    // Phase 3 (Records): Record expression - create RecordValue with resolved type name
    | RecordExpr (_, fieldExprs, _) ->
        let fieldValues =
            fieldExprs
            |> List.map (fun (name, expr) -> (name, ref (eval recEnv env expr)))
            |> Map.ofList
        let fieldNames = fieldExprs |> List.map fst |> Set.ofList
        let typeName = resolveRecordTypeName recEnv fieldNames
        RecordValue (typeName, fieldValues)

    // Phase 3 (Records): Field access
    | FieldAccess (expr, fieldName, _) ->
        match eval recEnv env expr with
        | RecordValue (_, fields) ->
            match Map.tryFind fieldName fields with
            | Some valueRef -> !valueRef
            | None -> failwithf "Field not found: %s" fieldName
        | v -> failwithf "Field access on non-record value: %s" (formatValue v)

    // Phase 3 (Records): Copy-and-update (record update)
    | RecordUpdate (source, updates, _) ->
        match eval recEnv env source with
        | RecordValue (typeName, fields) ->
            let copiedFields = fields |> Map.map (fun _ vr -> ref !vr)
            let updatedFields =
                updates
                |> List.fold (fun acc (name, expr) ->
                    Map.add name (ref (eval recEnv env expr)) acc) copiedFields
            RecordValue (typeName, updatedFields)
        | v -> failwithf "Copy-and-update on non-record value: %s" (formatValue v)

    // Phase 3 (Records): Mutable field assignment
    | SetField (expr, fieldName, value, _) ->
        match eval recEnv env expr with
        | RecordValue (_, fields) ->
            match Map.tryFind fieldName fields with
            | Some valueRef ->
                valueRef := eval recEnv env value
                TupleValue []
            | None -> failwithf "Field not found: %s" fieldName
        | v -> failwithf "SetField on non-record value: %s" (formatValue v)

/// Convenience function for top-level evaluation
let evalExpr (expr: Expr) : Value =
    eval Map.empty emptyEnv expr
