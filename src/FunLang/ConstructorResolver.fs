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
let rec resolveExpr (ctorInfo: Map<string, ConstructorInfo>) (lexpr: LExpr) : LExpr =
    let resolvedNode =
        match lexpr.Node with
        | ELiteral _ -> lexpr.Node

        // Check if variable is a nullary constructor
        | EVariable name ->
            match Map.tryFind name ctorInfo with
            | Some info when info.Arity = 0 -> EConstructor (name, None)
            | _ -> lexpr.Node

        // Check if application is a unary constructor
        | EApply (func, arg) ->
            match func.Node with
            | EVariable name ->
                match Map.tryFind name ctorInfo with
                | Some info when info.Arity = 1 ->
                    EConstructor (name, Some (resolveExpr ctorInfo arg))
                | _ ->
                    EApply (resolveExpr ctorInfo func, resolveExpr ctorInfo arg)
            | _ ->
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

        | EQualifiedVar _ -> lexpr.Node

        | EQualifiedCons (path, argOpt) ->
            EQualifiedCons (path, Option.map (resolveExpr ctorInfo) argOpt)

    { lexpr with Node = resolvedNode }

/// Resolve constructors in a pattern
and resolvePattern (ctorInfo: Map<string, ConstructorInfo>) (lpattern: LPattern) : LPattern =
    let resolvedNode =
        match lpattern.Node with
        | PWildcard -> lpattern.Node
        | PLiteral _ -> lpattern.Node

        // Check if variable is a nullary constructor
        | PVariable name ->
            match Map.tryFind name ctorInfo with
            | Some info when info.Arity = 0 -> PConstructor (name, None)
            | _ -> lpattern.Node

        | PTuple patterns ->
            PTuple (List.map (resolvePattern ctorInfo) patterns)

        | PList patterns ->
            PList (List.map (resolvePattern ctorInfo) patterns)

        | PCons (headP, tailP) ->
            PCons (resolvePattern ctorInfo headP, resolvePattern ctorInfo tailP)

        | PConstructor (name, argPatOpt) ->
            PConstructor (name, Option.map (resolvePattern ctorInfo) argPatOpt)

        | PQualifiedCons (path, argPatOpt) ->
            PQualifiedCons (path, Option.map (resolvePattern ctorInfo) argPatOpt)

    { lpattern with Node = resolvedNode }

/// Resolve a full program
let resolveProgram (program: Program) : Program =
    let ctorInfo = buildConstructorInfo program.TypeDefs
    let resolvedMainExpr = Option.map (resolveExpr ctorInfo) program.MainExpr
    { program with MainExpr = resolvedMainExpr }
