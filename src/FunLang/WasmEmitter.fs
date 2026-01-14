module FunLang.WasmEmitter

// =============================================================================
// WASM Binary Emitter
// =============================================================================
//
// Emits WASM binary format from WasmModule IR.
// Reference: WebAssembly Binary Format Specification
// https://webassembly.github.io/spec/core/binary/

open System
open System.IO
open FunLang.Errors
open FunLang.WasmTypes

// =============================================================================
// LEB128 Encoding
// =============================================================================
// WASM uses LEB128 (Little Endian Base 128) for variable-length integers

/// Encode unsigned integer as LEB128
let encodeULEB128 (value: uint32) : byte list =
    let rec loop v acc =
        let b = v &&& 0x7Fu
        let rest = v >>> 7
        if rest = 0u then
            List.rev (byte b :: acc)
        else
            loop rest (byte (b ||| 0x80u) :: acc)
    loop value []

/// Encode signed integer as LEB128
let encodeSLEB128 (value: int) : byte list =
    let rec loop v acc =
        let b = v &&& 0x7F
        let rest = v >>> 7
        let signBit = (b &&& 0x40) <> 0
        // Check if we're done
        let done' =
            (rest = 0 && not signBit) ||
            (rest = -1 && signBit)
        if done' then
            List.rev (byte b :: acc)
        else
            loop rest (byte (b ||| 0x80) :: acc)
    loop value []

/// Encode length-prefixed vector
let encodeVector (items: byte list list) : byte list =
    let count = uint32 items.Length
    encodeULEB128 count @ List.concat items

// =============================================================================
// Value Type Encoding
// =============================================================================

/// Encode a value type
let encodeValType (vt: WasmValType) : byte =
    match vt with
    | I32 -> ValTypeCode.I32
    | I64 -> ValTypeCode.I64
    | F32 -> ValTypeCode.F32
    | F64 -> ValTypeCode.F64

/// Encode block type (result type for blocks)
let encodeBlockType (result: WasmValType option) : byte =
    match result with
    | None -> BlockType.Empty
    | Some I32 -> BlockType.I32
    | Some I64 -> BlockType.I64
    | Some F32 -> BlockType.F32
    | Some F64 -> BlockType.F64

// =============================================================================
// Instruction Encoding
// =============================================================================

/// Encode a single instruction
let rec encodeInstr (instr: WasmInstr) : byte list =
    match instr with
    // Constants
    | I32Const n ->
        [Opcode.I32Const] @ encodeSLEB128 n

    | I64Const n ->
        [Opcode.I64Const] @ encodeSLEB128 (int n)

    // Arithmetic
    | I32Add -> [Opcode.I32Add]
    | I32Sub -> [Opcode.I32Sub]
    | I32Mul -> [Opcode.I32Mul]
    | I32DivS -> [Opcode.I32DivS]
    | I32RemS -> [Opcode.I32RemS]

    // Comparison
    | I32Eqz -> [Opcode.I32Eqz]
    | I32Eq -> [Opcode.I32Eq]
    | I32Ne -> [Opcode.I32Ne]
    | I32LtS -> [Opcode.I32LtS]
    | I32GtS -> [Opcode.I32GtS]
    | I32LeS -> [Opcode.I32LeS]
    | I32GeS -> [Opcode.I32GeS]

    // Logical
    | I32And -> [Opcode.I32And]
    | I32Or -> [Opcode.I32Or]
    | I32Xor -> [Opcode.I32Xor]

    // Control flow
    | If (result, thenBlock, elseBlock) ->
        let blockType = encodeBlockType result
        let thenBytes = List.collect encodeInstr thenBlock
        let elseBytes = List.collect encodeInstr elseBlock
        [Opcode.If; blockType] @ thenBytes @
        (if List.isEmpty elseBlock then [] else [Opcode.Else] @ elseBytes) @
        [Opcode.End]

    | Block (result, body) ->
        let blockType = encodeBlockType result
        let bodyBytes = List.collect encodeInstr body
        [Opcode.Block; blockType] @ bodyBytes @ [Opcode.End]

    | Loop (result, body) ->
        let blockType = encodeBlockType result
        let bodyBytes = List.collect encodeInstr body
        [Opcode.Loop; blockType] @ bodyBytes @ [Opcode.End]

    | Br labelIdx ->
        [Opcode.Br] @ encodeULEB128 (uint32 labelIdx)

    | BrIf labelIdx ->
        [Opcode.BrIf] @ encodeULEB128 (uint32 labelIdx)

    | Return -> [Opcode.Return]
    | Unreachable -> [Opcode.Unreachable]

    // Variables
    | LocalGet idx ->
        [Opcode.LocalGet] @ encodeULEB128 (uint32 idx)

    | LocalSet idx ->
        [Opcode.LocalSet] @ encodeULEB128 (uint32 idx)

    | LocalTee idx ->
        [Opcode.LocalTee] @ encodeULEB128 (uint32 idx)

    // Function calls
    | Call funcIdx ->
        [Opcode.Call] @ encodeULEB128 (uint32 funcIdx)

    // Stack operations
    | Drop -> [Opcode.Drop]
    | Select -> [Opcode.Select]

/// Encode a list of instructions (function body)
let encodeInstrs (instrs: WasmInstr list) : byte list =
    List.collect encodeInstr instrs @ [Opcode.End]

// =============================================================================
// Section Encoding
// =============================================================================

/// Encode a section with ID and content
let encodeSection (sectionId: byte) (content: byte list) : byte list =
    if List.isEmpty content then
        []
    else
        let contentBytes = List.toArray content
        let size = encodeULEB128 (uint32 contentBytes.Length)
        [sectionId] @ size @ content

/// Encode function type (params -> results)
let encodeFuncType (func: WasmFunc) : byte list =
    let paramTypes = func.Params |> List.map (snd >> encodeValType >> List.singleton)
    let resultTypes = func.Results |> List.map (encodeValType >> List.singleton)
    [0x60uy] @  // func type marker
    encodeVector paramTypes @
    encodeVector resultTypes

/// Encode Type Section (0x01)
let encodeTypeSection (funcs: WasmFunc list) : byte list =
    if List.isEmpty funcs then [] else
    let types = funcs |> List.map encodeFuncType
    encodeSection SectionId.Type (encodeVector types)

/// Encode Function Section (0x03) - type indices only
let encodeFunctionSection (funcs: WasmFunc list) : byte list =
    if List.isEmpty funcs then [] else
    let typeIndices = funcs |> List.mapi (fun i _ -> encodeULEB128 (uint32 i))
    encodeSection SectionId.Function (encodeVector typeIndices)

/// Encode Export Section (0x07)
let encodeExportSection (exports: (string * int) list) : byte list =
    if List.isEmpty exports then [] else
    let encodeExport (name: string, funcIdx) =
        let nameBytes = System.Text.Encoding.UTF8.GetBytes(name) |> Array.toList
        encodeULEB128 (uint32 nameBytes.Length) @
        nameBytes @
        [0x00uy] @  // export kind: func
        encodeULEB128 (uint32 funcIdx)
    let exportBytes = exports |> List.map encodeExport
    encodeSection SectionId.Export (encodeVector exportBytes)

/// Encode local declarations in a function body
let encodeLocals (locals: (string * WasmValType) list) : byte list =
    if List.isEmpty locals then
        encodeULEB128 0u  // 0 local groups
    else
        // Group consecutive locals of same type
        let groups =
            locals
            |> List.map snd
            |> List.fold (fun acc vt ->
                match acc with
                | (count, lastVt) :: rest when vt = lastVt ->
                    (count + 1, lastVt) :: rest
                | _ -> (1, vt) :: acc
            ) []
            |> List.rev
        let encodeGroup (count, vt) =
            encodeULEB128 (uint32 count) @ [encodeValType vt]
        encodeVector (List.map encodeGroup groups)

/// Encode Code Section (0x0a) - function bodies
let encodeCodeSection (funcs: WasmFunc list) : byte list =
    if List.isEmpty funcs then [] else
    let encodeFuncBody (func: WasmFunc) =
        let locals = encodeLocals func.Locals
        let body = encodeInstrs func.Body
        let funcBytes = locals @ body
        encodeULEB128 (uint32 funcBytes.Length) @ funcBytes
    let bodies = funcs |> List.map encodeFuncBody
    encodeSection SectionId.Code (encodeVector bodies)

// =============================================================================
// Module Encoding
// =============================================================================

/// Encode a complete WASM module to binary
let encodeModule (wasmMod: WasmModule) : byte array =
    let sections =
        encodeTypeSection wasmMod.Functions @
        encodeFunctionSection wasmMod.Functions @
        encodeExportSection wasmMod.Exports @
        encodeCodeSection wasmMod.Functions

    Array.concat [
        wasmMagic
        wasmVersion
        List.toArray sections
    ]

// =============================================================================
// File I/O
// =============================================================================

/// Write WASM binary to file
let writeBinary (path: string) (wasmMod: WasmModule) : Result<unit, FunLangError> =
    try
        let binary = encodeModule wasmMod
        File.WriteAllBytes(path, binary)
        Ok ()
    with ex ->
        Error {
            Kind = RuntimeError (sprintf "Failed to write WASM binary: %s" ex.Message, None)
            Message = sprintf "Failed to write to %s: %s" path ex.Message
            Hint = Some "Check file path and permissions"
            Position = None
        }

// =============================================================================
// WAT (Text Format) Generation
// =============================================================================

/// Convert value type to WAT string
let valTypeToWat (vt: WasmValType) : string =
    match vt with
    | I32 -> "i32"
    | I64 -> "i64"
    | F32 -> "f32"
    | F64 -> "f64"

/// Convert instruction to WAT string
let rec instrToWat (indent: int) (instr: WasmInstr) : string =
    let pad = String.replicate indent "  "
    match instr with
    | I32Const n -> sprintf "%si32.const %d" pad n
    | I64Const n -> sprintf "%si64.const %d" pad n
    | I32Add -> sprintf "%si32.add" pad
    | I32Sub -> sprintf "%si32.sub" pad
    | I32Mul -> sprintf "%si32.mul" pad
    | I32DivS -> sprintf "%si32.div_s" pad
    | I32RemS -> sprintf "%si32.rem_s" pad
    | I32Eqz -> sprintf "%si32.eqz" pad
    | I32Eq -> sprintf "%si32.eq" pad
    | I32Ne -> sprintf "%si32.ne" pad
    | I32LtS -> sprintf "%si32.lt_s" pad
    | I32GtS -> sprintf "%si32.gt_s" pad
    | I32LeS -> sprintf "%si32.le_s" pad
    | I32GeS -> sprintf "%si32.ge_s" pad
    | I32And -> sprintf "%si32.and" pad
    | I32Or -> sprintf "%si32.or" pad
    | I32Xor -> sprintf "%si32.xor" pad
    | If (result, thenBlock, elseBlock) ->
        let resultStr =
            match result with
            | Some vt -> sprintf " (result %s)" (valTypeToWat vt)
            | None -> ""
        let thenStr = thenBlock |> List.map (instrToWat (indent + 1)) |> String.concat "\n"
        let elseStr =
            if List.isEmpty elseBlock then ""
            else
                let elseInstrs = elseBlock |> List.map (instrToWat (indent + 1)) |> String.concat "\n"
                sprintf "\n%s(else\n%s\n%s)" pad elseInstrs pad
        sprintf "%s(if%s\n%s(then\n%s\n%s)%s\n%s)" pad resultStr pad thenStr pad elseStr pad
    | Block (result, body) ->
        let resultStr = match result with Some vt -> sprintf " (result %s)" (valTypeToWat vt) | None -> ""
        let bodyStr = body |> List.map (instrToWat (indent + 1)) |> String.concat "\n"
        sprintf "%s(block%s\n%s\n%s)" pad resultStr bodyStr pad
    | Loop (result, body) ->
        let resultStr = match result with Some vt -> sprintf " (result %s)" (valTypeToWat vt) | None -> ""
        let bodyStr = body |> List.map (instrToWat (indent + 1)) |> String.concat "\n"
        sprintf "%s(loop%s\n%s\n%s)" pad resultStr bodyStr pad
    | Br labelIdx -> sprintf "%sbr %d" pad labelIdx
    | BrIf labelIdx -> sprintf "%sbr_if %d" pad labelIdx
    | Return -> sprintf "%sreturn" pad
    | Unreachable -> sprintf "%sunreachable" pad
    | LocalGet idx -> sprintf "%slocal.get %d" pad idx
    | LocalSet idx -> sprintf "%slocal.set %d" pad idx
    | LocalTee idx -> sprintf "%slocal.tee %d" pad idx
    | Call funcIdx -> sprintf "%scall %d" pad funcIdx
    | Drop -> sprintf "%sdrop" pad
    | Select -> sprintf "%sselect" pad

/// Convert function to WAT string
let funcToWat (func: WasmFunc) : string =
    let paramsStr =
        func.Params
        |> List.map (fun (name, vt) -> sprintf "(param $%s %s)" name (valTypeToWat vt))
        |> String.concat " "
    let resultsStr =
        func.Results
        |> List.map (fun vt -> sprintf "(result %s)" (valTypeToWat vt))
        |> String.concat " "
    let localsStr =
        func.Locals
        |> List.map (fun (name, vt) -> sprintf "    (local $%s %s)" name (valTypeToWat vt))
        |> String.concat "\n"
    let bodyStr =
        func.Body
        |> List.map (instrToWat 2)
        |> String.concat "\n"
    sprintf "  (func $%s %s %s\n%s%s\n  )"
        func.Name
        paramsStr
        resultsStr
        (if String.IsNullOrEmpty localsStr then "" else localsStr + "\n")
        bodyStr

/// Convert module to WAT string
let moduleToWat (wasmMod: WasmModule) : string =
    let funcsStr =
        wasmMod.Functions
        |> List.map funcToWat
        |> String.concat "\n"
    let exportsStr =
        wasmMod.Exports
        |> List.map (fun (name, idx) ->
            sprintf "  (export \"%s\" (func $%s))" name wasmMod.Functions.[idx].Name)
        |> String.concat "\n"
    sprintf "(module\n%s\n%s\n)" funcsStr exportsStr

/// Write WAT text to file
let writeWat (path: string) (wasmMod: WasmModule) : Result<unit, FunLangError> =
    try
        let wat = moduleToWat wasmMod
        File.WriteAllText(path, wat)
        Ok ()
    with ex ->
        Error {
            Kind = RuntimeError (sprintf "Failed to write WAT: %s" ex.Message, None)
            Message = sprintf "Failed to write to %s: %s" path ex.Message
            Hint = Some "Check file path and permissions"
            Position = None
        }

// =============================================================================
// Convenience Functions
// =============================================================================

/// Get binary bytes (for testing)
let toBinary (wasmMod: WasmModule) : byte array =
    encodeModule wasmMod

/// Get WAT text (for testing/debugging)
let toWat (wasmMod: WasmModule) : string =
    moduleToWat wasmMod
