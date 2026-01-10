module FunLang.Unification

open FunLang.Types
open FunLang.Errors

// =============================================================================
// Occurs Check
// =============================================================================

/// Check if a type variable occurs in a type (prevents infinite types)
let occursIn (v: TypeVar) (t: Type) : bool =
    Set.contains v (TypeHelpers.freeTypeVars t)

// =============================================================================
// Unification Algorithm
// =============================================================================

/// Unify two types, returning a substitution that makes them equal
let rec unify (t1: Type) (t2: Type) : TypeResult<Substitution> =
    match t1, t2 with
    // Same base types
    | TInt, TInt -> Ok Map.empty
    | TBool, TBool -> Ok Map.empty
    | TString, TString -> Ok Map.empty
    | TUnit, TUnit -> Ok Map.empty

    // Same type variable
    | TVar v1, TVar v2 when v1 = v2 -> Ok Map.empty

    // Type variable with another type
    | TVar v, t | t, TVar v ->
        if occursIn v t then
            Error (TypeError.occursCheck v t None)
        else
            Ok (Map.ofList [(v, t)])

    // Function types
    | TFun (a1, r1), TFun (a2, r2) ->
        result {
            let! s1 = unify a1 a2
            let! s2 = unify (TypeHelpers.apply s1 r1) (TypeHelpers.apply s1 r2)
            return TypeHelpers.compose s2 s1
        }

    // List types
    | TList t1, TList t2 -> unify t1 t2

    // Tuple types (same arity)
    | TTuple ts1, TTuple ts2 when List.length ts1 = List.length ts2 ->
        unifyList ts1 ts2

    // Tuple types (different arity)
    | TTuple ts1, TTuple ts2 ->
        Error (TypeError.arityMismatch (List.length ts1) (List.length ts2) None)

    // Type mismatch
    | _ ->
        Error (TypeError.mismatch t1 t2 None)

/// Unify a list of type pairs
and unifyList (ts1: Type list) (ts2: Type list) : TypeResult<Substitution> =
    match ts1, ts2 with
    | [], [] -> Ok Map.empty
    | t1 :: rest1, t2 :: rest2 ->
        result {
            let! s1 = unify t1 t2
            let rest1' = List.map (TypeHelpers.apply s1) rest1
            let rest2' = List.map (TypeHelpers.apply s1) rest2
            let! s2 = unifyList rest1' rest2'
            return TypeHelpers.compose s2 s1
        }
    | _ ->
        Error (TypeError.arityMismatch (List.length ts1) (List.length ts2) None)

/// Unify all types in a list to a single type
let unifyAll (ts: Type list) : TypeResult<Substitution * Type> =
    match ts with
    | [] ->
        let α = TypeHelpers.freshTypeVar ()
        Ok (Map.empty, α)
    | [t] -> Ok (Map.empty, t)
    | t :: rest ->
        let folder (accResult: TypeResult<Substitution * Type>) (nextT: Type) =
            result {
                let! (accSubst, accType) = accResult
                let! s = unify accType (TypeHelpers.apply accSubst nextT)
                let newSubst = TypeHelpers.compose s accSubst
                let newType = TypeHelpers.apply s accType
                return (newSubst, newType)
            }
        List.fold folder (Ok (Map.empty, t)) rest
