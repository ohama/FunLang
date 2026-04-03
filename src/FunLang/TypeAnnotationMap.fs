module TypeAnnotationMap

open Ast
open Type

/// Create a fresh annotation map
let create () : System.Collections.Concurrent.ConcurrentDictionary<Span, Type> =
    System.Collections.Concurrent.ConcurrentDictionary<Span, Type>()

/// Record a type annotation for the given span.
/// Skips unknownSpan entries (synthetic nodes from elaboration).
let record (map: System.Collections.Concurrent.ConcurrentDictionary<Span, Type>) (span: Span) (ty: Type) =
    if span <> Ast.unknownSpan then
        map.[span] <- ty

/// Look up the inferred type for a span
let tryFind (map: System.Collections.Concurrent.ConcurrentDictionary<Span, Type>) (span: Span) : Type option =
    match map.TryGetValue(span) with
    | true, ty -> Some ty
    | _ -> None

/// Get all annotations as a sequence of (Span, Type) pairs
let toSeq (map: System.Collections.Concurrent.ConcurrentDictionary<Span, Type>) =
    map |> Seq.map (fun kv -> (kv.Key, kv.Value))
