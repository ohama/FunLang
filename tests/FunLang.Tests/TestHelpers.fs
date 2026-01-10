module FunLang.Tests.TestHelpers

open Expecto

/// Helper to expect Result.Ok
let expectOk msg result =
    match result with
    | Ok v -> v
    | Error e -> failtest (sprintf "%s: %A" msg e)

/// Helper to expect Result.Error
let expectError msg result =
    match result with
    | Ok v -> failtest (sprintf "%s: expected error but got %A" msg v)
    | Error e -> e
