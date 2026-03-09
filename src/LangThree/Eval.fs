module Eval

open Ast
open Type

/// Exception type for raise/try-with runtime support
exception LangThreeException of Value

/// Empty environment for top-level evaluation
/// Phase 5: Env type now defined in Ast.fs for mutual recursion with Value
let emptyEnv : Env = Map.empty

/// Module value environment for runtime qualified access
type ModuleValueEnv = {
    Values: Env
    CtorEnv: Map<string, Value>
    RecEnv: RecordEnv
    SubModules: Map<string, ModuleValueEnv>
}

/// Empty module value environment
let emptyModuleValueEnv: Map<string, ModuleValueEnv> = Map.empty

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
and evalMatchClauses (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (scrutinee: Value) (clauses: MatchClause list) : Value =
    match clauses with
    | [] -> failwith "Match failure: no pattern matched"
    | (pattern, guard, resultExpr) :: rest ->
        match matchPattern pattern scrutinee with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            match guard with
            | None ->
                eval recEnv moduleEnv extendedEnv resultExpr
            | Some guardExpr ->
                match eval recEnv moduleEnv extendedEnv guardExpr with
                | BoolValue true -> eval recEnv moduleEnv extendedEnv resultExpr
                | _ -> evalMatchClauses recEnv moduleEnv env scrutinee rest
        | None ->
            evalMatchClauses recEnv moduleEnv env scrutinee rest

/// Evaluate an expression in an environment
/// Returns Value (IntValue, BoolValue, or FunctionValue)
/// Raises exception for type errors and undefined variables
and eval (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (expr: Expr) : Value =
    match expr with
    | Number (n, _) -> IntValue n
    | Bool (b, _) -> BoolValue b
    | String (s, _) -> StringValue s

    | Var (name, _) ->
        match Map.tryFind name env with
        | Some value -> value
        | None -> failwithf "Undefined variable: %s" name

    | Let (name, binding, body, _) ->
        let value = eval recEnv moduleEnv env binding
        let extendedEnv = Map.add name value env
        eval recEnv moduleEnv extendedEnv body

    // Phase 1 (v3.0): Tuples
    | Tuple (exprs, _) ->
        let values = List.map (eval recEnv moduleEnv env) exprs
        TupleValue values

    | LetPat (pat, bindingExpr, bodyExpr, _) ->
        let value = eval recEnv moduleEnv env bindingExpr
        match matchPattern pat value with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            eval recEnv moduleEnv extendedEnv bodyExpr
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
        let value = eval recEnv moduleEnv env scrutinee
        evalMatchClauses recEnv moduleEnv env value clauses

    // Phase 2 (v3.0): Lists
    | EmptyList _ ->
        ListValue []

    | List (exprs, _) ->
        let values = List.map (eval recEnv moduleEnv env) exprs
        ListValue values

    | Cons (headExpr, tailExpr, _) ->
        let headVal = eval recEnv moduleEnv env headExpr
        match eval recEnv moduleEnv env tailExpr with
        | ListValue tailVals -> ListValue (headVal :: tailVals)
        | _ -> failwith "Type error: cons (::) requires list as second argument"

    // Arithmetic operations - type check for IntValue
    | Add (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> IntValue (l + r)
        | StringValue l, StringValue r -> StringValue (l + r)
        | _ -> failwith "Type error: + requires operands of same type (int or string)"

    | Subtract (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> IntValue (l - r)
        | _ -> failwith "Type error: - requires integer operands"

    | Multiply (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> IntValue (l * r)
        | _ -> failwith "Type error: * requires integer operands"

    | Divide (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> IntValue (l / r)
        | _ -> failwith "Type error: / requires integer operands"

    | Negate (e, _) ->
        match eval recEnv moduleEnv env e with
        | IntValue n -> IntValue (-n)
        | _ -> failwith "Type error: unary - requires integer operand"

    // Comparison operators - type check for IntValue, return BoolValue
    | LessThan (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> BoolValue (l < r)
        | _ -> failwith "Type error: < requires integer operands"

    | GreaterThan (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> BoolValue (l > r)
        | _ -> failwith "Type error: > requires integer operands"

    | LessEqual (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> BoolValue (l <= r)
        | _ -> failwith "Type error: <= requires integer operands"

    | GreaterEqual (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
        | IntValue l, IntValue r -> BoolValue (l >= r)
        | _ -> failwith "Type error: >= requires integer operands"

    // Equal and NotEqual work on both int and bool (same type required)
    | Equal (left, right, _) ->
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
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
        match eval recEnv moduleEnv env left, eval recEnv moduleEnv env right with
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
        match eval recEnv moduleEnv env left with
        | BoolValue false -> BoolValue false
        | BoolValue true ->
            match eval recEnv moduleEnv env right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: && requires boolean operands"
        | _ -> failwith "Type error: && requires boolean operands"

    | Or (left, right, _) ->
        match eval recEnv moduleEnv env left with
        | BoolValue true -> BoolValue true
        | BoolValue false ->
            match eval recEnv moduleEnv env right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: || requires boolean operands"
        | _ -> failwith "Type error: || requires boolean operands"

    // If-then-else - condition must be boolean
    | If (condition, thenBranch, elseBranch, _) ->
        match eval recEnv moduleEnv env condition with
        | BoolValue true -> eval recEnv moduleEnv env thenBranch
        | BoolValue false -> eval recEnv moduleEnv env elseBranch
        | _ -> failwith "Type error: if condition must be boolean"

    // Phase 2 (ADT): Constructor evaluation
    | Constructor (name, argOpt, _) ->
        let argValue = argOpt |> Option.map (eval recEnv moduleEnv env)
        DataValue (name, argValue)

    // v6.0: Type annotations - erased at runtime
    | Annot (expr, _, _) ->
        eval recEnv moduleEnv env expr  // Just evaluate the underlying expression

    | LambdaAnnot (param, _, body, _) ->
        FunctionValue (param, body, env)  // Same as regular lambda at runtime

    // Phase 5: Functions

    // Lambda creates a closure capturing current environment
    | Lambda (param, body, _) ->
        FunctionValue (param, body, env)

    // Function application
    | App (funcExpr, argExpr, _) ->
        let funcVal = eval recEnv moduleEnv env funcExpr
        match funcVal with
        | FunctionValue (param, body, closureEnv) ->
            let argValue = eval recEnv moduleEnv env argExpr
            // For recursive functions: when calling by name, add self to closure
            // This enables recursion by ensuring the function can find itself
            let augmentedClosureEnv =
                match funcExpr with
                | Var (name, _) -> Map.add name funcVal closureEnv
                | _ -> closureEnv
            let callEnv = Map.add param argValue augmentedClosureEnv
            eval recEnv moduleEnv callEnv body
        | _ -> failwith "Type error: attempted to call non-function"

    // Let rec - recursive function definition
    // Creates a function whose closure will be augmented at call time (in App)
    | LetRec (name, param, funcBody, inExpr, _) ->
        let funcVal = FunctionValue (param, funcBody, env)
        let recFuncEnv = Map.add name funcVal env
        eval recEnv moduleEnv recFuncEnv inExpr

    // Phase 3 (Records): Record expression - create RecordValue with resolved type name
    | RecordExpr (_, fieldExprs, _) ->
        let fieldValues =
            fieldExprs
            |> List.map (fun (name, expr) -> (name, ref (eval recEnv moduleEnv env expr)))
            |> Map.ofList
        let fieldNames = fieldExprs |> List.map fst |> Set.ofList
        let typeName = resolveRecordTypeName recEnv fieldNames
        RecordValue (typeName, fieldValues)

    // Phase 3 (Records) + Phase 5 (Modules): Field access / qualified access
    | FieldAccess (expr, fieldName, _) ->
        // Helper to extract module name from Var or Constructor (uppercase idents parsed as Constructor)
        let tryGetModuleName e =
            match e with
            | Var (name, _) when Map.containsKey name moduleEnv -> Some name
            | Constructor (name, None, _) when Map.containsKey name moduleEnv -> Some name
            | _ -> None
        match tryGetModuleName expr with
        | Some modName ->
            // Module qualified access: Module.member
            let modEnv = Map.find modName moduleEnv
            match Map.tryFind fieldName modEnv.Values with
            | Some value -> value
            | None ->
                match Map.tryFind fieldName modEnv.CtorEnv with
                | Some ctorValue -> ctorValue
                | None ->
                    // Could be a submodule reference for further chained access
                    match Map.tryFind fieldName modEnv.SubModules with
                    | Some _subMod ->
                        // Submodule access without further member -- error
                        failwithf "Module %s.%s is a module, not a value" modName fieldName
                    | None ->
                        failwithf "Module %s has no member or constructor %s" modName fieldName
        | None ->
        match expr with
        | FieldAccess (innerExpr, innerField, _) ->
            // Chained access: A.B.c where A.B is a submodule
            match tryGetModuleName innerExpr with
            | Some modName ->
                let outerMod = Map.find modName moduleEnv
                match Map.tryFind innerField outerMod.SubModules with
                | Some innerMod ->
                    match Map.tryFind fieldName innerMod.Values with
                    | Some value -> value
                    | None ->
                        match Map.tryFind fieldName innerMod.CtorEnv with
                        | Some ctorValue -> ctorValue
                        | None -> failwithf "Module %s.%s has no member %s" modName innerField fieldName
                | None ->
                    // innerField is not a submodule, try evaluating as record field access
                    let v = eval recEnv moduleEnv env expr
                    match v with
                    | RecordValue (_, fields) ->
                        match Map.tryFind fieldName fields with
                        | Some valueRef -> !valueRef
                        | None -> failwithf "Field not found: %s" fieldName
                    | _ -> failwithf "Field access on non-record/module value: %s" (formatValue v)
            | None ->
                // Regular record field access on chained expression
                let v = eval recEnv moduleEnv env expr
                match v with
                | RecordValue (_, fields) ->
                    match Map.tryFind fieldName fields with
                    | Some valueRef -> !valueRef
                    | None -> failwithf "Field not found: %s" fieldName
                | _ -> failwithf "Field access on non-record value: %s" (formatValue v)
        | _ ->
            // Regular record field access
            match eval recEnv moduleEnv env expr with
            | RecordValue (_, fields) ->
                match Map.tryFind fieldName fields with
                | Some valueRef -> !valueRef
                | None -> failwithf "Field not found: %s" fieldName
            | v -> failwithf "Field access on non-record value: %s" (formatValue v)

    // Phase 3 (Records): Copy-and-update (record update)
    | RecordUpdate (source, updates, _) ->
        match eval recEnv moduleEnv env source with
        | RecordValue (typeName, fields) ->
            let copiedFields = fields |> Map.map (fun _ vr -> ref !vr)
            let updatedFields =
                updates
                |> List.fold (fun acc (name, expr) ->
                    Map.add name (ref (eval recEnv moduleEnv env expr)) acc) copiedFields
            RecordValue (typeName, updatedFields)
        | v -> failwithf "Copy-and-update on non-record value: %s" (formatValue v)

    // Phase 6 (Exceptions): raise throws, try-with catches
    | Raise(arg, _) ->
        let exnVal = eval recEnv moduleEnv env arg
        raise (LangThreeException exnVal)
    | TryWith(body, handlers, _) ->
        try
            eval recEnv moduleEnv env body
        with
        | LangThreeException exnVal ->
            evalMatchClauses recEnv moduleEnv env exnVal handlers

    // Phase 3 (Records): Mutable field assignment
    | SetField (expr, fieldName, value, _) ->
        match eval recEnv moduleEnv env expr with
        | RecordValue (_, fields) ->
            match Map.tryFind fieldName fields with
            | Some valueRef ->
                valueRef := eval recEnv moduleEnv env value
                TupleValue []
            | None -> failwithf "Field not found: %s" fieldName
        | v -> failwithf "SetField on non-record value: %s" (formatValue v)

/// Convenience function for top-level evaluation
let evalExpr (expr: Expr) : Value =
    eval Map.empty Map.empty emptyEnv expr

/// Evaluate module declarations, building value and module environments
let rec evalModuleDecls
    (recEnv: RecordEnv)
    (moduleEnv: Map<string, ModuleValueEnv>)
    (initialEnv: Env)
    (decls: Decl list)
    : Env * Map<string, ModuleValueEnv> =
    decls
    |> List.fold (fun (env, modEnv) decl ->
        match decl with
        | LetDecl(name, body, _) ->
            let value = eval recEnv modEnv env body
            (Map.add name value env, modEnv)
        | ModuleDecl(name, innerDecls, _) ->
            let innerEnv, innerModEnv = evalModuleDecls recEnv modEnv env innerDecls
            // Extract only the bindings added by this module (not inherited from parent)
            let moduleValues =
                innerEnv
                |> Map.filter (fun k _ -> not (Map.containsKey k env))
            // Collect constructors from TypeDecl and ExceptionDecl siblings inside this module
            let ctorNames =
                innerDecls |> List.collect (fun d ->
                    match d with
                    | Decl.TypeDecl (Ast.TypeDecl(_, _, ctors, _)) ->
                        ctors |> List.collect (fun ctor ->
                            match ctor with
                            | ConstructorDecl(cname, _, _) -> [cname]
                            | GadtConstructorDecl(cname, _, _, _) -> [cname])
                    | ExceptionDecl(cname, _, _) -> [cname]
                    | _ -> [])
            let ctorEnv =
                ctorNames
                |> List.choose (fun cname ->
                    Map.tryFind cname innerEnv |> Option.map (fun v -> (cname, v)))
                |> Map.ofList
            let modValEnv = {
                Values = moduleValues
                CtorEnv = ctorEnv
                RecEnv = recEnv  // Share parent recEnv
                SubModules = innerModEnv
            }
            (env, Map.add name modValEnv modEnv)
        | OpenDecl(path, _) ->
            match path with
            | [name] ->
                match Map.tryFind name modEnv with
                | Some modValEnv ->
                    // Merge values into current env
                    let env' = Map.fold (fun acc k v -> Map.add k v acc) env modValEnv.Values
                    // Also merge constructors so they're accessible unqualified
                    let env'' = Map.fold (fun acc k v -> Map.add k v acc) env' modValEnv.CtorEnv
                    (env'', modEnv)
                | None -> (env, modEnv)  // Already caught by type checker
            | _ -> (env, modEnv)  // Multi-segment open paths: v2
        | Decl.TypeDecl (Ast.TypeDecl(_, _, ctors, _)) ->
            // Register constructor values/functions in the environment
            let dummySpan = unknownSpan
            let env' =
                ctors |> List.fold (fun acc ctor ->
                    let cname =
                        match ctor with
                        | ConstructorDecl(n, _, _) -> n
                        | GadtConstructorDecl(n, _, _, _) -> n
                    let hasArg =
                        match ctor with
                        | ConstructorDecl(_, Some _, _) -> true
                        | GadtConstructorDecl(_, args, _, _) -> not (List.isEmpty args)
                        | _ -> false
                    if hasArg then
                        // Constructor with argument: register as a function that wraps arg in DataValue
                        let param = "__ctorArg"
                        let body = Constructor(cname, Some(Var(param, dummySpan)), dummySpan)
                        Map.add cname (FunctionValue(param, body, acc)) acc
                    else
                        // Nullary constructor: register as a value
                        Map.add cname (DataValue(cname, None)) acc) env
            (env', modEnv)
        | ExceptionDecl(name, dataTypeOpt, _) ->
            let dummySpan = unknownSpan
            let env' =
                match dataTypeOpt with
                | None ->
                    // Nullary exception: register as DataValue
                    Map.add name (DataValue(name, None)) env
                | Some _ ->
                    // Exception with data: register as constructor function
                    let param = "__x"
                    let body = Constructor(name, Some(Var(param, dummySpan)), dummySpan)
                    Map.add name (FunctionValue(param, body, env)) env
            // Also register in moduleEnv's CtorEnv if we're inside a module
            (env', modEnv)
        | _ -> (env, modEnv)  // RecordTypeDecl handled elsewhere
    ) (initialEnv, moduleEnv)
