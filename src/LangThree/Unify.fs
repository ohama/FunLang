module Unify

open Type
open Diagnostic
open Ast

/// Occurs check: does type variable v appear in type t?
/// Used to detect infinite types like 'a = 'a -> int
let occurs (v: int) (t: Type): bool =
    Set.contains v (freeVars t)

/// Unify with context and trace tracking for rich error messages
let rec unifyWithContext (ctx: InferContext list) (trace: UnifyPath list)
                         (span: Span) (t1: Type) (t2: Type): Subst =
    match t1, t2 with
    // Primitive types: must be identical
    | TInt, TInt -> empty
    | TBool, TBool -> empty
    | TString, TString -> empty

    // Type variable: symmetric pattern handles both TVar,t and t,TVar
    | TVar n, t | t, TVar n ->
        if t = TVar n then empty  // Same variable
        elif occurs n t then
            raise (TypeException {
                Kind = OccursCheck (n, t)
                Span = span
                Term = None
                ContextStack = ctx
                Trace = trace
            })
        else
            singleton n t

    // Arrow types: unify domains, apply to ranges, unify ranges
    | TArrow (a1, b1), TArrow (a2, b2) ->
        let s1 = unifyWithContext ctx (AtFunctionParam a2 :: trace) span a1 a2
        let s2 = unifyWithContext ctx (AtFunctionReturn b2 :: trace) span
                                  (apply s1 b1) (apply s1 b2)
        compose s2 s1

    // Tuple types: must have same length, fold with substitution threading
    | TTuple ts1, TTuple ts2 when List.length ts1 = List.length ts2 ->
        List.fold2 (fun (s, idx) t1 t2 ->
            let s' = unifyWithContext ctx (AtTupleIndex (idx, t2) :: trace) span
                                      (apply s t1) (apply s t2)
            (compose s' s, idx + 1)
        ) (empty, 0) ts1 ts2
        |> fst

    // List types: recursively unify element types
    | TList t1, TList t2 ->
        unifyWithContext ctx (AtListElement t2 :: trace) span t1 t2

    // Everything else: type mismatch
    | _ ->
        raise (TypeException {
            Kind = UnifyMismatch (t1, t2)
            Span = span
            Term = None
            ContextStack = ctx
            Trace = trace
        })

/// Robinson's unification algorithm (backward compatible)
let unify (t1: Type) (t2: Type): Subst =
    unifyWithContext [] [] unknownSpan t1 t2
