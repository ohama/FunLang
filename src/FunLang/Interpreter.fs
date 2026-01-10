module FunLang.Interpreter

open FunLang.Ast
open FunLang.Errors

// =============================================================================
// Evaluator (Stub - will implement in Phase 1)
// =============================================================================

/// Evaluate an expression in the given environment
let eval (env: Env) (expr: Expr) : EvalResult =
    // TODO: Implement in Phase 1
    Error (Error.runtime "Not implemented" None)
