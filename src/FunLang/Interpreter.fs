module FunLang.Interpreter

open FunLang.Ast
open FunLang.Errors

// =============================================================================
// Helper Functions
// =============================================================================

let private typeOf = function
    | VInt _ -> "int"
    | VBool _ -> "bool"
    | VString _ -> "string"
    | VUnit -> "unit"
    | VTuple _ -> "tuple"
    | VList _ -> "list"
    | VClosure _ -> "function"
    | VRecClosure _ -> "function"

// =============================================================================
// Binary Operations
// =============================================================================

let private evalBinaryOp op left right =
    match op, left, right with
    // Arithmetic (int -> int -> int)
    | Add, VInt a, VInt b -> Ok (VInt (a + b))
    | Sub, VInt a, VInt b -> Ok (VInt (a - b))
    | Mul, VInt a, VInt b -> Ok (VInt (a * b))
    | Div, VInt _, VInt 0 -> Error (Error.runtime "Division by zero" None)
    | Div, VInt a, VInt b -> Ok (VInt (a / b))
    | Mod, VInt _, VInt 0 -> Error (Error.runtime "Modulo by zero" None)
    | Mod, VInt a, VInt b -> Ok (VInt (a % b))

    // Comparison (int -> int -> bool)
    | Lt, VInt a, VInt b -> Ok (VBool (a < b))
    | Gt, VInt a, VInt b -> Ok (VBool (a > b))
    | Lte, VInt a, VInt b -> Ok (VBool (a <= b))
    | Gte, VInt a, VInt b -> Ok (VBool (a >= b))

    // Equality (polymorphic)
    | Eq, VInt a, VInt b -> Ok (VBool (a = b))
    | Eq, VBool a, VBool b -> Ok (VBool (a = b))
    | Eq, VString a, VString b -> Ok (VBool (a = b))
    | Neq, VInt a, VInt b -> Ok (VBool (a <> b))
    | Neq, VBool a, VBool b -> Ok (VBool (a <> b))
    | Neq, VString a, VString b -> Ok (VBool (a <> b))

    // Boolean (bool -> bool -> bool)
    | And, VBool a, VBool b -> Ok (VBool (a && b))
    | Or, VBool a, VBool b -> Ok (VBool (a || b))

    // Type errors
    | Add, _, _ -> Error (Error.runtime (sprintf "Cannot add %s and %s" (typeOf left) (typeOf right)) None)
    | Sub, _, _ -> Error (Error.runtime (sprintf "Cannot subtract %s from %s" (typeOf right) (typeOf left)) None)
    | Mul, _, _ -> Error (Error.runtime (sprintf "Cannot multiply %s and %s" (typeOf left) (typeOf right)) None)
    | Div, _, _ -> Error (Error.runtime (sprintf "Cannot divide %s by %s" (typeOf left) (typeOf right)) None)
    | Mod, _, _ -> Error (Error.runtime (sprintf "Cannot modulo %s by %s" (typeOf left) (typeOf right)) None)
    | Lt, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s with <" (typeOf left) (typeOf right)) None)
    | Gt, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s with >" (typeOf left) (typeOf right)) None)
    | Lte, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s with <=" (typeOf left) (typeOf right)) None)
    | Gte, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s with >=" (typeOf left) (typeOf right)) None)
    | Eq, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s for equality" (typeOf left) (typeOf right)) None)
    | Neq, _, _ -> Error (Error.runtime (sprintf "Cannot compare %s and %s for inequality" (typeOf left) (typeOf right)) None)
    | And, _, _ -> Error (Error.runtime (sprintf "Cannot 'and' %s and %s" (typeOf left) (typeOf right)) None)
    | Or, _, _ -> Error (Error.runtime (sprintf "Cannot 'or' %s and %s" (typeOf left) (typeOf right)) None)

// =============================================================================
// Unary Operations
// =============================================================================

let private evalUnaryOp op operand =
    match op, operand with
    | Neg, VInt n -> Ok (VInt (-n))
    | Not, VBool b -> Ok (VBool (not b))
    | Neg, _ -> Error (Error.runtime (sprintf "Cannot negate %s" (typeOf operand)) None)
    | Not, _ -> Error (Error.runtime (sprintf "Cannot 'not' %s" (typeOf operand)) None)

// =============================================================================
// Evaluator
// =============================================================================

/// Evaluate an expression in the given environment
let rec eval (env: Env) (expr: Expr) : EvalResult =
    match expr with
    // Literals
    | ELiteral (LInt n) -> Ok (VInt n)
    | ELiteral (LBool b) -> Ok (VBool b)
    | ELiteral (LString s) -> Ok (VString s)
    | ELiteral LUnit -> Ok VUnit

    // Variables
    | EVariable name ->
        match Map.tryFind name env with
        | Some v -> Ok v
        | None -> Error (Error.runtime (sprintf "Unbound variable: %s" name) None)

    // Binary operations
    | EBinaryOp (op, left, right) ->
        match eval env left with
        | Error e -> Error e
        | Ok leftVal ->
            match eval env right with
            | Error e -> Error e
            | Ok rightVal -> evalBinaryOp op leftVal rightVal

    // Unary operations
    | EUnaryOp (op, operand) ->
        match eval env operand with
        | Error e -> Error e
        | Ok v -> evalUnaryOp op v

    // Let binding
    | ELet (name, value, body) ->
        match eval env value with
        | Error e -> Error e
        | Ok v ->
            let env' = Map.add name v env
            eval env' body

    // Recursive let binding
    | ELetRec (name, value, body) ->
        // For recursive functions, we need to create a closure that captures itself
        match value with
        | ELambda (param, funcBody) ->
            let closure = VRecClosure (name, param, funcBody, env)
            let env' = Map.add name closure env
            eval env' body
        | _ ->
            // Non-function recursive bindings - evaluate normally
            match eval env value with
            | Error e -> Error e
            | Ok v ->
                let env' = Map.add name v env
                eval env' body

    // If expression
    | EIf (cond, thenBr, elseBr) ->
        match eval env cond with
        | Error e -> Error e
        | Ok (VBool true) -> eval env thenBr
        | Ok (VBool false) -> eval env elseBr
        | Ok v -> Error (Error.runtime (sprintf "Condition must be bool, got %s" (typeOf v)) None)

    // Lambda (create closure)
    | ELambda (param, body) ->
        Ok (VClosure (param, body, env))

    // Function application
    | EApply (func, arg) ->
        match eval env func with
        | Error e -> Error e
        | Ok (VClosure (param, body, closureEnv)) ->
            match eval env arg with
            | Error e -> Error e
            | Ok argVal ->
                let env' = Map.add param argVal closureEnv
                eval env' body
        | Ok (VRecClosure (name, param, body, closureEnv)) ->
            match eval env arg with
            | Error e -> Error e
            | Ok argVal ->
                let closure = VRecClosure (name, param, body, closureEnv)
                let env' = Map.add name closure (Map.add param argVal closureEnv)
                eval env' body
        | Ok v -> Error (Error.runtime (sprintf "Cannot apply %s as function" (typeOf v)) None)

    // Tuple
    | ETuple exprs ->
        let rec evalAll es acc =
            match es with
            | [] -> Ok (VTuple (List.rev acc))
            | e :: rest ->
                match eval env e with
                | Error err -> Error err
                | Ok v -> evalAll rest (v :: acc)
        evalAll exprs []

    // List
    | EList exprs ->
        let rec evalAll es acc =
            match es with
            | [] -> Ok (VList (List.rev acc))
            | e :: rest ->
                match eval env e with
                | Error err -> Error err
                | Ok v -> evalAll rest (v :: acc)
        evalAll exprs []

    // Cons
    | ECons (head, tail) ->
        match eval env head with
        | Error e -> Error e
        | Ok headVal ->
            match eval env tail with
            | Error e -> Error e
            | Ok (VList tailList) -> Ok (VList (headVal :: tailList))
            | Ok v -> Error (Error.runtime (sprintf "Cannot cons onto %s" (typeOf v)) None)

    // Block (evaluate all, return last)
    | EBlock exprs ->
        let rec evalBlock es lastVal =
            match es with
            | [] -> Ok lastVal
            | e :: rest ->
                match eval env e with
                | Error err -> Error err
                | Ok v -> evalBlock rest v
        evalBlock exprs VUnit

    // Match expression (placeholder - will implement in Phase 4)
    | EMatch _ ->
        Error (Error.runtime "Pattern matching not yet implemented" None)
