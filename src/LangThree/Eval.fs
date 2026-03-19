module Eval

open Ast
open Type

/// Exception type for raise/try-with runtime support
exception LangThreeException of Value

/// Empty environment for top-level evaluation
/// Phase 5: Env type now defined in Ast.fs for mutual recursion with Value
let emptyEnv : Env = Map.empty

/// Counter for generating unique compose variable names (avoids name collision in chained composition)
let mutable composeCounter = 0

// Phase 12: Printf helpers

/// Parse format string to extract specifier characters in order.
/// Returns list of specifier chars: "d", "s", or "b". Skips %% (escaped percent).
let parsePrintfSpecifiers (fmt: string) : string list =
    let mutable i = 0
    let specs = System.Collections.Generic.List<string>()
    while i < fmt.Length do
        if fmt.[i] = '%' && i + 1 < fmt.Length then
            let spec = fmt.[i + 1]
            match spec with
            | 'd' | 's' | 'b' ->
                specs.Add(string spec)
                i <- i + 2
            | '%' ->
                i <- i + 2  // %% is a literal %, not a specifier
            | _ ->
                i <- i + 1  // unknown % sequence, skip
        else
            i <- i + 1
    specs |> Seq.toList

/// Format a value for printf output. NEVER adds quotes around strings (unlike formatValue).
/// %s -> raw string; %d -> int as string; %b -> bool as "true"/"false"
let printfFormatArg (spec: string) (v: Value) : string =
    match spec, v with
    | "s", StringValue s -> s
    | "d", IntValue n    -> string n
    | "b", BoolValue b   -> if b then "true" else "false"
    | "s", _ -> failwith "printf: %s requires a string argument"
    | "d", _ -> failwith "printf: %d requires an int argument"
    | "b", _ -> failwith "printf: %b requires a bool argument"
    | s, _   -> failwithf "printf: unknown specifier %%%s" s

/// Substitute format specifiers in fmt left-to-right with collected argument values.
/// Handles %% -> literal %. Collects chars unchanged for non-% positions.
let substitutePrintfArgs (fmt: string) (args: Value list) : string =
    let sb = System.Text.StringBuilder()
    let mutable i = 0
    let mutable argIdx = 0
    let argsArr = List.toArray args
    while i < fmt.Length do
        if fmt.[i] = '%' && i + 1 < fmt.Length then
            let spec = fmt.[i + 1]
            match spec with
            | 'd' | 's' | 'b' ->
                if argIdx < argsArr.Length then
                    sb.Append(printfFormatArg (string spec) argsArr.[argIdx]) |> ignore
                    argIdx <- argIdx + 1
                i <- i + 2
            | '%' ->
                sb.Append('%') |> ignore
                i <- i + 2
            | _ ->
                sb.Append(fmt.[i]) |> ignore
                i <- i + 1
        else
            sb.Append(fmt.[i]) |> ignore
            i <- i + 1
    sb.ToString()

/// Build a curried BuiltinValue chain for printf with the given format string.
/// remaining: specifiers left to collect. collected: args gathered so far (in reverse).
/// When remaining is empty, substitute and flush.
let rec applyPrintfArgs (fmt: string) (remaining: string list) (collected: Value list) : Value =
    match remaining with
    | [] ->
        let result = substitutePrintfArgs fmt (List.rev collected)
        stdout.Write(result)
        stdout.Flush()
        TupleValue []
    | _ :: rest ->
        BuiltinValue (fun argVal ->
            applyPrintfArgs fmt rest (argVal :: collected))

/// Initial built-in environment: all 6 string functions as BuiltinValue.
/// Merged into the evaluation environment at startup (Program.fs, Repl.fs).
/// Curried multi-arg builtins use nested BuiltinValue wrappers.
let initialBuiltinEnv : Env =
    Map.ofList [
        // string_length : string -> int
        "string_length", BuiltinValue (fun v ->
            match v with
            | StringValue s -> IntValue s.Length
            | _ -> failwith "string_length: expected string argument")

        // string_concat : string -> string -> string
        "string_concat", BuiltinValue (fun v1 ->
            match v1 with
            | StringValue s1 ->
                BuiltinValue (fun v2 ->
                    match v2 with
                    | StringValue s2 -> StringValue (s1 + s2)
                    | _ -> failwith "string_concat: second argument must be string")
            | _ -> failwith "string_concat: first argument must be string")

        // string_sub : string -> int -> int -> string  (start index, length)
        // string_sub "hello" 1 3 = "ell"  (start=1, length=3)
        "string_sub", BuiltinValue (fun v1 ->
            match v1 with
            | StringValue s ->
                BuiltinValue (fun v2 ->
                    match v2 with
                    | IntValue start ->
                        BuiltinValue (fun v3 ->
                            match v3 with
                            | IntValue len ->
                                if start < 0 || len < 0 || start + len > s.Length then
                                    failwithf "string_sub: index out of range (start=%d, len=%d, string_length=%d)"
                                              start len s.Length
                                else
                                    StringValue (s.[start .. start + len - 1])
                            | _ -> failwith "string_sub: third argument must be int")
                    | _ -> failwith "string_sub: second argument must be int")
            | _ -> failwith "string_sub: first argument must be string")

        // string_contains : string -> string -> bool
        "string_contains", BuiltinValue (fun v1 ->
            match v1 with
            | StringValue haystack ->
                BuiltinValue (fun v2 ->
                    match v2 with
                    | StringValue needle -> BoolValue (haystack.Contains(needle))
                    | _ -> failwith "string_contains: second argument must be string")
            | _ -> failwith "string_contains: first argument must be string")

        // to_string : 'a -> string  (polymorphic; runtime rejects unsupported types)
        "to_string", BuiltinValue (fun v ->
            match v with
            | IntValue n -> StringValue (string n)
            | BoolValue b -> StringValue (if b then "true" else "false")
            | StringValue s -> StringValue s
            | _ -> failwith "to_string: unsupported type (expected int, bool, or string)")

        // string_to_int : string -> int
        "string_to_int", BuiltinValue (fun v ->
            match v with
            | StringValue s ->
                match System.Int32.TryParse(s) with
                | true, n -> IntValue n
                | false, _ -> failwithf "string_to_int: cannot parse '%s' as integer" s
            | _ -> failwith "string_to_int: expected string argument")

        // print : string -> unit  (no newline; flushes immediately)
        "print", BuiltinValue (fun v ->
            match v with
            | StringValue s ->
                stdout.Write(s)
                stdout.Flush()
                TupleValue []
            | _ -> failwith "print: expected string argument")

        // println : string -> unit  (with newline; flushes immediately)
        "println", BuiltinValue (fun v ->
            match v with
            | StringValue s ->
                stdout.WriteLine(s)
                stdout.Flush()
                TupleValue []
            | _ -> failwith "println: expected string argument")

        // printf : string -> ...  (variadic at runtime via curried BuiltinValue chain)
        "printf", BuiltinValue (fun fmtVal ->
            match fmtVal with
            | StringValue fmt ->
                let specifiers = parsePrintfSpecifiers fmt
                applyPrintfArgs fmt specifiers []
            | _ -> failwith "printf: first argument must be a format string")
    ]

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
    | BuiltinValue _ -> "<builtin>"
    | TailCall _ -> "<tailcall>"

/// Structural equality for Value (needed since BuiltinValue contains a function type
/// which prevents F# from auto-deriving equality on the Value DU)
let rec valuesEqual (v1: Value) (v2: Value) : bool =
    match v1, v2 with
    | IntValue a, IntValue b -> a = b
    | BoolValue a, BoolValue b -> a = b
    | StringValue a, StringValue b -> a = b
    | TupleValue a, TupleValue b ->
        List.length a = List.length b && List.forall2 valuesEqual a b
    | ListValue a, ListValue b ->
        List.length a = List.length b && List.forall2 valuesEqual a b
    | DataValue (n1, None), DataValue (n2, None) -> n1 = n2
    | DataValue (n1, Some av), DataValue (n2, Some bv) -> n1 = n2 && valuesEqual av bv
    | RecordValue (t1, f1), RecordValue (t2, f2) ->
        t1 = t2 &&
        Map.count f1 = Map.count f2 &&
        Map.forall (fun k r1 ->
            match Map.tryFind k f2 with
            | Some r2 -> valuesEqual !r1 !r2
            | None -> false) f1
    | BuiltinValue _, BuiltinValue _ -> false  // Functions not comparable
    | FunctionValue _, FunctionValue _ -> false  // Functions not comparable
    | TailCall _, _ | _, TailCall _ -> false  // TailCall is transient
    | _ -> false

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
    | ConstPat (StringConst s, _), StringValue v ->
        if s = v then Some [] else None
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
    // Phase 16: Or-pattern - try each alternative
    | OrPat(pats, _), value ->
        pats |> List.tryPick (fun p -> matchPattern p value)
    | _ -> None  // Type mismatch (e.g., TuplePat vs IntValue)

/// Evaluate match clauses sequentially, returning first match
/// Phase 15: tailPos parameter for tail call optimization
and evalMatchClauses (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (tailPos: bool) (scrutinee: Value) (clauses: MatchClause list) : Value =
    match clauses with
    | [] -> failwith "Match failure: no pattern matched"
    | (pattern, guard, resultExpr) :: rest ->
        match matchPattern pattern scrutinee with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            match guard with
            | None ->
                eval recEnv moduleEnv extendedEnv tailPos resultExpr
            | Some guardExpr ->
                match eval recEnv moduleEnv extendedEnv false guardExpr with
                | BoolValue true -> eval recEnv moduleEnv extendedEnv tailPos resultExpr
                | _ -> evalMatchClauses recEnv moduleEnv env tailPos scrutinee rest
        | None ->
            evalMatchClauses recEnv moduleEnv env tailPos scrutinee rest

/// Apply a function value to an argument (shared by App, PipeRight, and trampoline loop)
/// Phase 15: When tailPos=true, body evaluates in tail position so nested tail calls return TailCall
and applyFunc (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (funcVal: Value) (argVal: Value) (funcExpr: Expr) (tailPos: bool) : Value =
    match funcVal with
    | FunctionValue (param, body, closureEnv) ->
        // For recursive functions: when calling by name, add self to closure
        let augmentedClosureEnv =
            match funcExpr with
            | Var (name, _) -> Map.add name funcVal closureEnv
            | _ -> closureEnv
        let callEnv = Map.add param argVal augmentedClosureEnv
        eval recEnv moduleEnv callEnv tailPos body
    | BuiltinValue fn -> fn argVal
    | _ -> failwith "Type error: attempted to call non-function"

/// Evaluate an expression in an environment
/// Returns Value (IntValue, BoolValue, or FunctionValue)
/// Raises exception for type errors and undefined variables
/// Phase 15: tailPos parameter enables tail call optimization via trampoline
and eval (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (tailPos: bool) (expr: Expr) : Value =
    match expr with
    | Number (n, _) -> IntValue n
    | Bool (b, _) -> BoolValue b
    | String (s, _) -> StringValue s

    | Var (name, _) ->
        match Map.tryFind name env with
        | Some value -> value
        | None -> failwithf "Undefined variable: %s" name

    | Let (name, binding, body, _) ->
        let value = eval recEnv moduleEnv env false binding
        let extendedEnv = Map.add name value env
        eval recEnv moduleEnv extendedEnv tailPos body

    // Phase 1 (v3.0): Tuples
    | Tuple (exprs, _) ->
        let values = List.map (eval recEnv moduleEnv env false) exprs
        TupleValue values

    | LetPat (pat, bindingExpr, bodyExpr, _) ->
        let value = eval recEnv moduleEnv env false bindingExpr
        match matchPattern pat value with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            eval recEnv moduleEnv extendedEnv tailPos bodyExpr
        | None ->
            match pat, value with
            | TuplePat (pats, _), TupleValue vals ->
                failwithf "Pattern match failed: tuple pattern expects %d elements but value has %d"
                          (List.length pats) (List.length vals)
            | TuplePat _, _ ->
                failwith "Pattern match failed: expected tuple value"
            | _ ->
                failwith "Pattern match failed"

    // Phase 3 (v3.0): Pattern Matching -- compiled via decision tree (Phase 7)
    | Match (scrutinee, clauses, _) ->
        let value = eval recEnv moduleEnv env false scrutinee
        let tree, rootVar = MatchCompile.compileMatch clauses
        let evalFn e tp expr = eval recEnv moduleEnv e tp expr
        let varEnv = Map.ofList [(rootVar, value)]
        MatchCompile.evalDecisionTree evalFn env tailPos varEnv tree

    // Phase 2 (v3.0): Lists
    | EmptyList _ ->
        ListValue []

    | List (exprs, _) ->
        let values = List.map (eval recEnv moduleEnv env false) exprs
        ListValue values

    | Cons (headExpr, tailExpr, _) ->
        let headVal = eval recEnv moduleEnv env false headExpr
        match eval recEnv moduleEnv env false tailExpr with
        | ListValue tailVals -> ListValue (headVal :: tailVals)
        | _ -> failwith "Type error: cons (::) requires list as second argument"

    // Arithmetic operations - type check for IntValue
    | Add (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> IntValue (l + r)
        | StringValue l, StringValue r -> StringValue (l + r)
        | _ -> failwith "Type error: + requires operands of same type (int or string)"

    | Subtract (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> IntValue (l - r)
        | _ -> failwith "Type error: - requires integer operands"

    | Multiply (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> IntValue (l * r)
        | _ -> failwith "Type error: * requires integer operands"

    | Divide (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> IntValue (l / r)
        | _ -> failwith "Type error: / requires integer operands"

    | Negate (e, _) ->
        match eval recEnv moduleEnv env false e with
        | IntValue n -> IntValue (-n)
        | _ -> failwith "Type error: unary - requires integer operand"

    // Comparison operators - type check for IntValue, return BoolValue
    | LessThan (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> BoolValue (l < r)
        | _ -> failwith "Type error: < requires integer operands"

    | GreaterThan (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> BoolValue (l > r)
        | _ -> failwith "Type error: > requires integer operands"

    | LessEqual (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> BoolValue (l <= r)
        | _ -> failwith "Type error: <= requires integer operands"

    | GreaterEqual (left, right, _) ->
        match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
        | IntValue l, IntValue r -> BoolValue (l >= r)
        | _ -> failwith "Type error: >= requires integer operands"

    // Equal and NotEqual delegate to valuesEqual for all value types
    | Equal (left, right, _) ->
        let l = eval recEnv moduleEnv env false left
        let r = eval recEnv moduleEnv env false right
        BoolValue (valuesEqual l r)

    | NotEqual (left, right, _) ->
        let l = eval recEnv moduleEnv env false left
        let r = eval recEnv moduleEnv env false right
        BoolValue (not (valuesEqual l r))

    // Logical operators - short-circuit evaluation
    | And (left, right, _) ->
        match eval recEnv moduleEnv env false left with
        | BoolValue false -> BoolValue false
        | BoolValue true ->
            match eval recEnv moduleEnv env false right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: && requires boolean operands"
        | _ -> failwith "Type error: && requires boolean operands"

    | Or (left, right, _) ->
        match eval recEnv moduleEnv env false left with
        | BoolValue true -> BoolValue true
        | BoolValue false ->
            match eval recEnv moduleEnv env false right with
            | BoolValue b -> BoolValue b
            | _ -> failwith "Type error: || requires boolean operands"
        | _ -> failwith "Type error: || requires boolean operands"

    // If-then-else - condition must be boolean; branches inherit tailPos
    | If (condition, thenBranch, elseBranch, _) ->
        match eval recEnv moduleEnv env false condition with
        | BoolValue true -> eval recEnv moduleEnv env tailPos thenBranch
        | BoolValue false -> eval recEnv moduleEnv env tailPos elseBranch
        | _ -> failwith "Type error: if condition must be boolean"

    // Phase 2 (ADT): Constructor evaluation
    | Constructor (name, argOpt, _) ->
        let argValue = argOpt |> Option.map (eval recEnv moduleEnv env false)
        DataValue (name, argValue)

    // v6.0: Type annotations - erased at runtime
    | Annot (expr, _, _) ->
        eval recEnv moduleEnv env tailPos expr  // Just evaluate the underlying expression

    | LambdaAnnot (param, _, body, _) ->
        FunctionValue (param, body, env)  // Same as regular lambda at runtime

    // Phase 5: Functions

    // Lambda creates a closure capturing current environment
    | Lambda (param, body, _) ->
        FunctionValue (param, body, env)

    // Function application with trampoline for TCO (Phase 15)
    | App (funcExpr, argExpr, _) ->
        let funcVal = eval recEnv moduleEnv env false funcExpr
        let argValue = eval recEnv moduleEnv env false argExpr
        // When in tail position, return TailCall to let the caller's trampoline handle it
        if tailPos then
            TailCall (funcVal, argValue)
        else
            // Not in tail position: apply and trampoline any TailCall chain
            let mutable result = applyFunc recEnv moduleEnv funcVal argValue funcExpr true
            while (match result with TailCall _ -> true | _ -> false) do
                match result with
                | TailCall (f, a) ->
                    result <- applyFunc recEnv moduleEnv f a funcExpr true
                | _ -> ()
            result

    // Let rec - recursive function definition
    // Creates a function whose closure will be augmented at call time (in App)
    | LetRec (name, param, funcBody, inExpr, _) ->
        let funcVal = FunctionValue (param, funcBody, env)
        let recFuncEnv = Map.add name funcVal env
        eval recEnv moduleEnv recFuncEnv tailPos inExpr

    // Phase 3 (Records): Record expression - create RecordValue with resolved type name
    | RecordExpr (_, fieldExprs, _) ->
        let fieldValues =
            fieldExprs
            |> List.map (fun (name, expr) -> (name, ref (eval recEnv moduleEnv env false expr)))
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
                    let v = eval recEnv moduleEnv env false expr
                    match v with
                    | RecordValue (_, fields) ->
                        match Map.tryFind fieldName fields with
                        | Some valueRef -> !valueRef
                        | None -> failwithf "Field not found: %s" fieldName
                    | _ -> failwithf "Field access on non-record/module value: %s" (formatValue v)
            | None ->
                // Regular record field access on chained expression
                let v = eval recEnv moduleEnv env false expr
                match v with
                | RecordValue (_, fields) ->
                    match Map.tryFind fieldName fields with
                    | Some valueRef -> !valueRef
                    | None -> failwithf "Field not found: %s" fieldName
                | _ -> failwithf "Field access on non-record value: %s" (formatValue v)
        | _ ->
            // Regular record field access
            match eval recEnv moduleEnv env false expr with
            | RecordValue (_, fields) ->
                match Map.tryFind fieldName fields with
                | Some valueRef -> !valueRef
                | None -> failwithf "Field not found: %s" fieldName
            | v -> failwithf "Field access on non-record value: %s" (formatValue v)

    // Phase 3 (Records): Copy-and-update (record update)
    | RecordUpdate (source, updates, _) ->
        match eval recEnv moduleEnv env false source with
        | RecordValue (typeName, fields) ->
            let copiedFields = fields |> Map.map (fun _ vr -> ref !vr)
            let updatedFields =
                updates
                |> List.fold (fun acc (name, expr) ->
                    Map.add name (ref (eval recEnv moduleEnv env false expr)) acc) copiedFields
            RecordValue (typeName, updatedFields)
        | v -> failwithf "Copy-and-update on non-record value: %s" (formatValue v)

    // Phase 6 (Exceptions): raise throws, try-with catches
    // TryWith body is NOT tail position (exception handler needs stack frame)
    | Raise(arg, _) ->
        let exnVal = eval recEnv moduleEnv env false arg
        raise (LangThreeException exnVal)
    | TryWith(body, handlers, _) ->
        try
            eval recEnv moduleEnv env false body
        with
        | LangThreeException exnVal ->
            try
                evalMatchClauses recEnv moduleEnv env tailPos exnVal handlers
            with
            | e when e.Message = "Match failure: no pattern matched" ->
                // No handler matched: re-raise the original exception
                raise (LangThreeException exnVal)

    // Phase 9 (Pipe & Composition): Pipe and composition operators
    // PipeRight uses same trampoline pattern as App (Phase 15)
    | PipeRight (left, right, _) ->
        let argVal = eval recEnv moduleEnv env false left
        let funcVal = eval recEnv moduleEnv env false right
        if tailPos then
            TailCall (funcVal, argVal)
        else
            let mutable result = applyFunc recEnv moduleEnv funcVal argVal right true
            while (match result with TailCall _ -> true | _ -> false) do
                match result with
                | TailCall (f, a) ->
                    result <- applyFunc recEnv moduleEnv f a right true
                | _ -> ()
            result

    | ComposeRight (left, right, _) ->
        let fVal = eval recEnv moduleEnv env false left
        let gVal = eval recEnv moduleEnv env false right
        // Use unique names to avoid collision in chained composition
        composeCounter <- composeCounter + 1
        let counter = composeCounter
        let param = sprintf "__compose_x_%d" counter
        let fName = sprintf "__compose_f_%d" counter
        let gName = sprintf "__compose_g_%d" counter
        let body = App(Var(gName, unknownSpan), App(Var(fName, unknownSpan), Var(param, unknownSpan), unknownSpan), unknownSpan)
        let closureEnv = Map.ofList [(fName, fVal); (gName, gVal)]
        FunctionValue(param, body, closureEnv)

    | ComposeLeft (left, right, _) ->
        let fVal = eval recEnv moduleEnv env false left
        let gVal = eval recEnv moduleEnv env false right
        composeCounter <- composeCounter + 1
        let counter = composeCounter
        let param = sprintf "__compose_x_%d" counter
        let fName = sprintf "__compose_f_%d" counter
        let gName = sprintf "__compose_g_%d" counter
        let body = App(Var(fName, unknownSpan), App(Var(gName, unknownSpan), Var(param, unknownSpan), unknownSpan), unknownSpan)
        let closureEnv = Map.ofList [(fName, fVal); (gName, gVal)]
        FunctionValue(param, body, closureEnv)

    // Phase 3 (Records): Mutable field assignment
    | SetField (expr, fieldName, value, _) ->
        match eval recEnv moduleEnv env false expr with
        | RecordValue (_, fields) ->
            match Map.tryFind fieldName fields with
            | Some valueRef ->
                valueRef := eval recEnv moduleEnv env false value
                TupleValue []
            | None -> failwithf "Field not found: %s" fieldName
        | v -> failwithf "SetField on non-record value: %s" (formatValue v)

/// Convenience function for top-level evaluation
let evalExpr (expr: Expr) : Value =
    eval Map.empty Map.empty emptyEnv false expr

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
            let value = eval recEnv modEnv env false body
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
        | TypeAliasDecl _ ->
            // Type aliases are purely a type-level feature, no runtime behavior
            (env, modEnv)
        | _ -> (env, modEnv)  // RecordTypeDecl handled elsewhere
    ) (initialEnv, moduleEnv)
