module FunLang.WasmCompiler

// =============================================================================
// FunLang to WASM Compiler
// =============================================================================
//
// Compiles FunLang AST to WASM IR.
// MVP scope: integers, booleans, binary ops, let bindings, if/then/else

open FunLang.Ast
open FunLang.Errors
open FunLang.WasmTypes

// =============================================================================
// Compilation Error Helpers
// =============================================================================

module CompileError =
    let unsupported feature pos =
        { Kind = RuntimeError (sprintf "WASM compilation: unsupported feature '%s'" feature, pos)
          Message = sprintf "Cannot compile to WASM: %s is not supported in MVP" feature
          Hint = Some "This feature requires closures or memory management"
          Position = pos }

    let unboundVar name pos =
        Error.unboundVar name pos

// =============================================================================
// Expression Compiler
// =============================================================================

/// Compile a single expression to WASM instructions
let rec compileExpr (env: CompileEnv) (expr: LExpr) : Result<WasmInstr list * CompileEnv, FunLangError> =
    let pos = Some expr.Pos
    match expr.Node with

    // ----- Literals -----
    | ELiteral (LInt n) ->
        Ok ([I32Const n], env)

    | ELiteral (LBool b) ->
        // Booleans are represented as i32: true = 1, false = 0
        Ok ([I32Const (if b then 1 else 0)], env)

    | ELiteral LUnit ->
        // Unit is represented as i32(0) - will be dropped if unused
        Ok ([I32Const 0], env)

    | ELiteral (LString _) ->
        Error (CompileError.unsupported "strings" pos)

    // ----- Variables -----
    | EVariable name ->
        match lookupLocal name env with
        | Some idx -> Ok ([LocalGet idx], env)
        | None -> Error (CompileError.unboundVar name expr.Pos)

    | EQualifiedVar _ ->
        Error (CompileError.unsupported "qualified variables (modules)" pos)

    // ----- Binary Operators -----
    | EBinaryOp (op, left, right) ->
        result {
            let! (leftInstrs, env1) = compileExpr env left
            let! (rightInstrs, env2) = compileExpr env1 right
            let opInstr = compileBinaryOp op
            return (leftInstrs @ rightInstrs @ [opInstr], env2)
        }

    // ----- Unary Operators -----
    | EUnaryOp (Neg, operand) ->
        result {
            let! (operandInstrs, env1) = compileExpr env operand
            // -x = 0 - x
            return ([I32Const 0] @ operandInstrs @ [I32Sub], env1)
        }

    | EUnaryOp (Not, operand) ->
        result {
            let! (operandInstrs, env1) = compileExpr env operand
            // not x = x == 0
            return (operandInstrs @ [I32Eqz], env1)
        }

    // ----- Let Binding -----
    | ELet (name, value, body) ->
        result {
            // Compile the value
            let! (valueInstrs, env1) = compileExpr env value
            // Allocate a new local for the variable
            let (idx, env2) = addLocal name env1
            // Compile the body with the new local
            let! (bodyInstrs, env3) = compileExpr env2 body
            // Set local and evaluate body
            return (valueInstrs @ [LocalSet idx] @ bodyInstrs, env3)
        }

    // ----- Let Rec (not supported in MVP) -----
    | ELetRec _ ->
        Error (CompileError.unsupported "recursive let bindings" pos)

    // ----- Lambda (not supported in MVP) -----
    | ELambda _ ->
        Error (CompileError.unsupported "lambda functions" pos)

    // ----- Apply (not supported in MVP) -----
    | EApply _ ->
        Error (CompileError.unsupported "function application" pos)

    // ----- If/Then/Else -----
    | EIf (cond, thenBr, elseBr) ->
        result {
            let! (condInstrs, env1) = compileExpr env cond
            let! (thenInstrs, env2) = compileExpr env1 thenBr
            let! (elseInstrs, env3) = compileExpr env2 elseBr
            // WASM if with i32 result type
            let ifInstr = If (Some I32, thenInstrs, elseInstrs)
            return (condInstrs @ [ifInstr], env3)
        }

    // ----- Tuple (not supported in MVP) -----
    | ETuple _ ->
        Error (CompileError.unsupported "tuples" pos)

    // ----- List (not supported in MVP) -----
    | EList _ ->
        Error (CompileError.unsupported "lists" pos)

    | ECons _ ->
        Error (CompileError.unsupported "cons operator (::)" pos)

    // ----- Block -----
    | EBlock exprs ->
        compileBlock env exprs

    // ----- Match (not supported in MVP) -----
    | EMatch _ ->
        Error (CompileError.unsupported "pattern matching" pos)

    // ----- Constructor (not supported in MVP) -----
    | EConstructor _ ->
        Error (CompileError.unsupported "constructors" pos)

    | EQualifiedCons _ ->
        Error (CompileError.unsupported "qualified constructors" pos)

/// Compile a binary operator to a WASM instruction
and compileBinaryOp (op: BinaryOp) : WasmInstr =
    match op with
    | Add -> I32Add
    | Sub -> I32Sub
    | Mul -> I32Mul
    | Div -> I32DivS
    | Mod -> I32RemS
    | Eq  -> I32Eq
    | Neq -> I32Ne
    | Lt  -> I32LtS
    | Gt  -> I32GtS
    | Lte -> I32LeS
    | Gte -> I32GeS
    | And -> I32And
    | Or  -> I32Or

/// Compile a block of expressions (sequence)
and compileBlock (env: CompileEnv) (exprs: LExpr list) : Result<WasmInstr list * CompileEnv, FunLangError> =
    match exprs with
    | [] ->
        // Empty block returns unit (0)
        Ok ([I32Const 0], env)
    | [single] ->
        // Single expression
        compileExpr env single
    | head :: tail ->
        result {
            let! (headInstrs, env1) = compileExpr env head
            // Drop intermediate results (except last)
            let! (tailInstrs, env2) = compileBlock env1 tail
            return (headInstrs @ [Drop] @ tailInstrs, env2)
        }

// =============================================================================
// Collect Local Variables
// =============================================================================

/// Count the number of local variables needed for an expression
let rec countLocals (expr: LExpr) : int =
    match expr.Node with
    | ELiteral _ | EVariable _ | EQualifiedVar _ -> 0
    | EBinaryOp (_, left, right) -> countLocals left + countLocals right
    | EUnaryOp (_, operand) -> countLocals operand
    | ELet (_, value, body) -> 1 + countLocals value + countLocals body
    | ELetRec (_, value, body) -> 1 + countLocals value + countLocals body
    | ELambda (_, body) -> countLocals body
    | EApply (func, arg) -> countLocals func + countLocals arg
    | EIf (cond, thenBr, elseBr) -> countLocals cond + countLocals thenBr + countLocals elseBr
    | ETuple exprs -> List.sumBy countLocals exprs
    | EList exprs -> List.sumBy countLocals exprs
    | ECons (head, tail) -> countLocals head + countLocals tail
    | EBlock exprs -> List.sumBy countLocals exprs
    | EMatch (scrutinee, cases) ->
        countLocals scrutinee + List.sumBy (fun (_, g, e) ->
            (match g with Some ge -> countLocals ge | None -> 0) + countLocals e) cases
    | EConstructor (_, arg) -> Option.map countLocals arg |> Option.defaultValue 0
    | EQualifiedCons (_, arg) -> Option.map countLocals arg |> Option.defaultValue 0

// =============================================================================
// Program Compiler
// =============================================================================

/// Compile a program to a WASM module
let compileProgram (program: Program) : Result<WasmModule, FunLangError> =
    match program.MainExpr with
    | None ->
        // No main expression - create empty module
        Ok emptyModule

    | Some mainExpr ->
        result {
            // Compile main expression
            let! (bodyInstrs, finalEnv) = compileExpr emptyEnv mainExpr

            // Count locals needed
            let localCount = finalEnv.NextLocalIdx

            // Create locals list (all i32 for MVP)
            let locals =
                [ for i in 0 .. localCount - 1 ->
                    (sprintf "local_%d" i, I32) ]

            // Create main function
            let mainFunc = {
                Name = "main"
                Params = []
                Results = [I32]  // Main returns i32
                Locals = locals
                Body = bodyInstrs
            }

            return {
                Functions = [mainFunc]
                Exports = [("main", 0)]  // Export main function
                Memory = None
                Globals = []
            }
        }

// =============================================================================
// Compile Helper (convenience function)
// =============================================================================

/// Compile a single expression (for testing)
let compileExpression (expr: LExpr) : Result<WasmInstr list, FunLangError> =
    result {
        let! (instrs, _) = compileExpr emptyEnv expr
        return instrs
    }
