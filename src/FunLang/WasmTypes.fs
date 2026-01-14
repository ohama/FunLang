module FunLang.WasmTypes

// =============================================================================
// WASM Intermediate Representation Types
// =============================================================================
//
// This module defines the WASM IR used as an intermediate representation
// between FunLang AST and WASM binary output.
//
// Reference: WebAssembly Core Specification 2.0
// https://webassembly.github.io/spec/core/

open FunLang.Ast

// =============================================================================
// WASM Value Types
// =============================================================================

/// WASM numeric value types
type WasmValType =
    | I32       // 32-bit integer (used for int, bool)
    | I64       // 64-bit integer (reserved for future)
    | F32       // 32-bit float (reserved for future)
    | F64       // 64-bit float (reserved for future)

// =============================================================================
// WASM Instructions
// =============================================================================

/// WASM instruction set (MVP subset)
type WasmInstr =
    // ----- Constants -----
    | I32Const of int               // push i32 constant
    | I64Const of int64             // push i64 constant (reserved)

    // ----- Arithmetic (i32) -----
    | I32Add                        // a + b
    | I32Sub                        // a - b
    | I32Mul                        // a * b
    | I32DivS                       // a / b (signed)
    | I32RemS                       // a % b (signed remainder)

    // ----- Comparison (i32) -----
    | I32Eqz                        // a == 0 (unary)
    | I32Eq                         // a == b
    | I32Ne                         // a != b
    | I32LtS                        // a < b (signed)
    | I32GtS                        // a > b (signed)
    | I32LeS                        // a <= b (signed)
    | I32GeS                        // a >= b (signed)

    // ----- Logical (i32 bitwise) -----
    | I32And                        // a and b
    | I32Or                         // a or b
    | I32Xor                        // a xor b

    // ----- Control Flow -----
    | If of result: WasmValType option * thenBlock: WasmInstr list * elseBlock: WasmInstr list
    | Block of result: WasmValType option * body: WasmInstr list
    | Loop of result: WasmValType option * body: WasmInstr list
    | Br of labelIdx: int           // branch to label
    | BrIf of labelIdx: int         // conditional branch
    | Return                        // return from function
    | Unreachable                   // trap

    // ----- Variables -----
    | LocalGet of idx: int          // get local variable
    | LocalSet of idx: int          // set local variable
    | LocalTee of idx: int          // set and return local variable

    // ----- Function Calls -----
    | Call of funcIdx: int          // call function by index

    // ----- Stack Operations -----
    | Drop                          // drop top value from stack
    | Select                        // select between two values based on condition

// =============================================================================
// WASM Function
// =============================================================================

/// WASM function definition
type WasmFunc = {
    /// Function name (for debugging/export)
    Name: string

    /// Parameter types with names (for debugging)
    Params: (string * WasmValType) list

    /// Return types (WASM supports multi-value, but MVP uses single)
    Results: WasmValType list

    /// Local variable types with names (for debugging)
    Locals: (string * WasmValType) list

    /// Function body (instruction sequence)
    Body: WasmInstr list
}

/// Create an empty function with given name
let emptyFunc name = {
    Name = name
    Params = []
    Results = []
    Locals = []
    Body = []
}

// =============================================================================
// WASM Module
// =============================================================================

/// WASM module definition
type WasmModule = {
    /// All functions in the module
    Functions: WasmFunc list

    /// Exported items: (export_name, func_index)
    Exports: (string * int) list

    /// Memory declarations (pages)
    Memory: int option

    /// Global variables (for future use)
    Globals: (string * WasmValType * WasmInstr list) list
}

/// Create an empty module
let emptyModule = {
    Functions = []
    Exports = []
    Memory = None
    Globals = []
}

// =============================================================================
// Compilation Environment
// =============================================================================

/// Environment for tracking variables during compilation
type CompileEnv = {
    /// Map from variable name to local index
    Locals: Map<string, int>

    /// Next available local index
    NextLocalIdx: int

    /// Map from function name to function index
    Functions: Map<string, int>

    /// Source position for error reporting
    CurrentPos: Position option
}

/// Create an empty compilation environment
let emptyEnv = {
    Locals = Map.empty
    NextLocalIdx = 0
    Functions = Map.empty
    CurrentPos = None
}

/// Add a local variable to the environment
let addLocal (name: string) (env: CompileEnv) : int * CompileEnv =
    let idx = env.NextLocalIdx
    let newLocals = Map.add name idx env.Locals
    idx, { env with Locals = newLocals; NextLocalIdx = idx + 1 }

/// Look up a local variable
let lookupLocal (name: string) (env: CompileEnv) : int option =
    Map.tryFind name env.Locals

/// Add a function to the environment
let addFunction (name: string) (idx: int) (env: CompileEnv) : CompileEnv =
    { env with Functions = Map.add name idx env.Functions }

/// Look up a function
let lookupFunction (name: string) (env: CompileEnv) : int option =
    Map.tryFind name env.Functions

// =============================================================================
// WASM Binary Constants
// =============================================================================

/// WASM binary magic number: \0asm
let wasmMagic = [| 0x00uy; 0x61uy; 0x73uy; 0x6duy |]

/// WASM version 1
let wasmVersion = [| 0x01uy; 0x00uy; 0x00uy; 0x00uy |]

/// Section IDs
module SectionId =
    let Custom    = 0x00uy
    let Type      = 0x01uy
    let Import    = 0x02uy
    let Function  = 0x03uy
    let Table     = 0x04uy
    let Memory    = 0x05uy
    let Global    = 0x06uy
    let Export    = 0x07uy
    let Start     = 0x08uy
    let Element   = 0x09uy
    let Code      = 0x0auy
    let Data      = 0x0buy

/// Value type encodings
module ValTypeCode =
    let I32 = 0x7fuy
    let I64 = 0x7euy
    let F32 = 0x7duy
    let F64 = 0x7cuy

/// Instruction opcodes
module Opcode =
    // Control
    let Unreachable = 0x00uy
    let Nop         = 0x01uy
    let Block       = 0x02uy
    let Loop        = 0x03uy
    let If          = 0x04uy
    let Else        = 0x05uy
    let End         = 0x0buy
    let Br          = 0x0cuy
    let BrIf        = 0x0duy
    let Return      = 0x0fuy
    let Call        = 0x10uy

    // Parametric
    let Drop        = 0x1auy
    let Select      = 0x1buy

    // Variable
    let LocalGet    = 0x20uy
    let LocalSet    = 0x21uy
    let LocalTee    = 0x22uy

    // Constants
    let I32Const    = 0x41uy
    let I64Const    = 0x42uy

    // i32 comparison
    let I32Eqz      = 0x45uy
    let I32Eq       = 0x46uy
    let I32Ne       = 0x47uy
    let I32LtS      = 0x48uy
    let I32GtS      = 0x4auy
    let I32LeS      = 0x4cuy
    let I32GeS      = 0x4euy

    // i32 arithmetic
    let I32Add      = 0x6auy
    let I32Sub      = 0x6buy
    let I32Mul      = 0x6cuy
    let I32DivS     = 0x6duy
    let I32RemS     = 0x6fuy

    // i32 bitwise
    let I32And      = 0x71uy
    let I32Or       = 0x72uy
    let I32Xor      = 0x73uy

/// Block type encodings
module BlockType =
    let Empty = 0x40uy  // void / no result
    let I32   = 0x7fuy
    let I64   = 0x7euy
    let F32   = 0x7duy
    let F64   = 0x7cuy
