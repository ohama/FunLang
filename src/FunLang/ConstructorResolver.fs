module FunLang.ConstructorResolver

open FunLang.Ast

// =============================================================================
// Constructor Resolver
// =============================================================================
//
// After parsing, constructors are represented as:
// - Nullary: EVariable "None" → should become EConstructor ("None", None)
// - Unary: EApply (EVariable "Some", arg) → should become EConstructor ("Some", Some arg)
//
// This module resolves constructor references based on type definitions.
// =============================================================================

/// Information about a constructor
type ConstructorInfo = {
    Name: string
    Arity: int  // 0 for nullary, 1 for unary
}

/// Build a set of constructor information from type definitions
let buildConstructorInfo (typeDefs: TypeDef list) : Map<string, ConstructorInfo> =
    typeDefs
    |> List.collect (fun td ->
        td.Constructors
        |> List.map (fun (name, argOpt) ->
            let arity = if argOpt.IsSome then 1 else 0
            (name, { Name = name; Arity = arity })))
    |> Map.ofList

/// Resolve constructors in an expression
let rec resolveExpr (ctorInfo: Map<string, ConstructorInfo>) (expr: Expr) : Expr =
    match expr with
    | ELiteral _ -> expr

    // Check if variable is a nullary constructor
    | EVariable name ->
        match Map.tryFind name ctorInfo with
        | Some info when info.Arity = 0 -> EConstructor (name, None)
        | _ -> expr

    // Check if application is a unary constructor
    | EApply (EVariable name, arg) ->
        match Map.tryFind name ctorInfo with
        | Some info when info.Arity = 1 ->
            EConstructor (name, Some (resolveExpr ctorInfo arg))
        | _ ->
            EApply (resolveExpr ctorInfo (EVariable name), resolveExpr ctorInfo arg)

    | EApply (func, arg) ->
        EApply (resolveExpr ctorInfo func, resolveExpr ctorInfo arg)

    | EBinaryOp (op, e1, e2) ->
        EBinaryOp (op, resolveExpr ctorInfo e1, resolveExpr ctorInfo e2)

    | EUnaryOp (op, e) ->
        EUnaryOp (op, resolveExpr ctorInfo e)

    | ELet (name, e1, e2) ->
        ELet (name, resolveExpr ctorInfo e1, resolveExpr ctorInfo e2)

    | ELetRec (name, e1, e2) ->
        ELetRec (name, resolveExpr ctorInfo e1, resolveExpr ctorInfo e2)

    | ELambda (param, body) ->
        ELambda (param, resolveExpr ctorInfo body)

    | EIf (cond, thenE, elseE) ->
        EIf (resolveExpr ctorInfo cond, resolveExpr ctorInfo thenE, resolveExpr ctorInfo elseE)

    | ETuple exprs ->
        ETuple (List.map (resolveExpr ctorInfo) exprs)

    | EList exprs ->
        EList (List.map (resolveExpr ctorInfo) exprs)

    | ECons (head, tail) ->
        ECons (resolveExpr ctorInfo head, resolveExpr ctorInfo tail)

    | EBlock exprs ->
        EBlock (List.map (resolveExpr ctorInfo) exprs)

    | EMatch (scrutinee, cases) ->
        let resolvedScrutinee = resolveExpr ctorInfo scrutinee
        let resolvedCases =
            cases
            |> List.map (fun (pat, guard, body) ->
                let resolvedPat = resolvePattern ctorInfo pat
                let resolvedGuard = Option.map (resolveExpr ctorInfo) guard
                let resolvedBody = resolveExpr ctorInfo body
                (resolvedPat, resolvedGuard, resolvedBody))
        EMatch (resolvedScrutinee, resolvedCases)

    | EConstructor (name, argOpt) ->
        EConstructor (name, Option.map (resolveExpr ctorInfo) argOpt)

/// Resolve constructors in a pattern
and resolvePattern (ctorInfo: Map<string, ConstructorInfo>) (pattern: Pattern) : Pattern =
    match pattern with
    | PWildcard -> pattern
    | PLiteral _ -> pattern

    // Check if variable is a nullary constructor
    | PVariable name ->
        match Map.tryFind name ctorInfo with
        | Some info when info.Arity = 0 -> PConstructor (name, None)
        | _ -> pattern

    | PTuple patterns ->
        PTuple (List.map (resolvePattern ctorInfo) patterns)

    | PList patterns ->
        PList (List.map (resolvePattern ctorInfo) patterns)

    | PCons (headP, tailP) ->
        PCons (resolvePattern ctorInfo headP, resolvePattern ctorInfo tailP)

    | PConstructor (name, argPatOpt) ->
        PConstructor (name, Option.map (resolvePattern ctorInfo) argPatOpt)

/// Resolve a full program
let resolveProgram (program: Program) : Program =
    let ctorInfo = buildConstructorInfo program.TypeDefs
    let resolvedMainExpr = Option.map (resolveExpr ctorInfo) program.MainExpr
    { program with MainExpr = resolvedMainExpr }
