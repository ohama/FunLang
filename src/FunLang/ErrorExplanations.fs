module FunLang.ErrorExplanations

// =============================================================================
// Error Explanation Types
// =============================================================================

/// Detailed explanation for an error code
type ErrorExplanation = {
    Code: string              // "E202"
    Title: string             // "Unbound variable"
    Brief: string             // One-liner for inline display
    Explanation: string       // Full explanation paragraph
    BadExample: string        // Code that causes this error
    GoodExample: string       // Fixed version
    RelatedCodes: string list // ["E201"; "E208"]
}

// =============================================================================
// Error Explanations Data
// =============================================================================

/// Helper to create explanation
let private mkExplanation code title brief explanation badEx goodEx related =
    { Code = code
      Title = title
      Brief = brief
      Explanation = explanation
      BadExample = badEx
      GoodExample = goodEx
      RelatedCodes = related }

// -----------------------------------------------------------------------------
// Lexer Errors (E001-E099)
// -----------------------------------------------------------------------------

let private e001 =
    mkExplanation
        "E001"
        "Unexpected character"
        "character not recognized by lexer"
        "The lexer encountered a character that is not part of the FunLang language. This usually means a typo or an unsupported symbol was used."
        "let x = 5 @ 3"
        "let x = 5 + 3"
        ["E002"]

let private e002 =
    mkExplanation
        "E002"
        "Unterminated string"
        "string literal missing closing quote"
        "A string literal was started with a double quote but never closed. Every opening quote must have a matching closing quote on the same line."
        "let msg = \"hello"
        "let msg = \"hello\""
        ["E003"]

let private e003 =
    mkExplanation
        "E003"
        "Invalid escape sequence"
        "unrecognized escape in string"
        "An escape sequence in a string literal is not recognized. Valid escapes are: \\n (newline), \\t (tab), \\\" (quote), \\\\ (backslash)."
        "let path = \"C:\\users\""
        "let path = \"C:\\\\users\""
        ["E002"]

let private e004 =
    mkExplanation
        "E004"
        "Invalid number"
        "malformed numeric literal"
        "A numeric literal is malformed. Numbers must be valid integers. Floating point numbers are not yet supported."
        "let x = 12.34"
        "let x = 12"
        []

// -----------------------------------------------------------------------------
// Parser Errors (E100-E199)
// -----------------------------------------------------------------------------

let private e101 =
    mkExplanation
        "E101"
        "Unexpected token"
        "parser found token it didn't expect"
        "The parser encountered a token that doesn't fit the expected syntax at this position. Check for missing operators, keywords, or parentheses."
        "let x = 1 + + 2"
        "let x = 1 + 2"
        ["E102"; "E105"]

let private e102 =
    mkExplanation
        "E102"
        "Missing token"
        "expected token not found"
        "A required token is missing from the expression. Common causes include missing 'in' after let binding, missing 'then' after if condition."
        "let x = 1 x + 1"
        "let x = 1 in x + 1"
        ["E101"]

let private e103 =
    mkExplanation
        "E103"
        "Invalid syntax"
        "general syntax error"
        "The code structure is not valid FunLang syntax. Review the expression for structural issues like mismatched brackets or invalid constructs."
        "let = 5"
        "let x = 5"
        ["E101"; "E102"]

let private e104 =
    mkExplanation
        "E104"
        "Indentation error"
        "incorrect indentation level"
        "The indentation level is incorrect. FunLang uses indentation to define blocks. Inner expressions must be indented more than the containing expression."
        "let f x =\nx + 1"
        "let f x =\n    x + 1"
        ["E106"]

let private e105 =
    mkExplanation
        "E105"
        "Unclosed delimiter"
        "missing closing bracket or paren"
        "An opening delimiter (parenthesis, bracket) was not closed. Every '(' needs a matching ')' and every '[' needs a matching ']'."
        "let xs = [1, 2, 3"
        "let xs = [1, 2, 3]"
        ["E101"]

let private e106 =
    mkExplanation
        "E106"
        "Empty block"
        "block requires at least one expression"
        "A block was opened but contains no expressions. Every indented block must contain at least one expression that produces a value."
        "let x =\n    \nlet y = 1"
        "let x = 0\nlet y = 1"
        ["E104"]

// -----------------------------------------------------------------------------
// Type Errors (E200-E299)
// -----------------------------------------------------------------------------

let private e201 =
    mkExplanation
        "E201"
        "Type mismatch"
        "expression type doesn't match expected type"
        "The type of an expression doesn't match what was expected. This often happens when mixing different types in operations or function calls."
        "1 + true"
        "1 + 2"
        ["E204"; "E206"]

let private e202 =
    mkExplanation
        "E202"
        "Unbound variable"
        "variables must be defined with 'let' before use"
        "A variable was used before it was defined. In FunLang, all variables must be introduced with 'let' before they can be referenced."
        "x + 1"
        "let x = 10 in x + 1"
        ["E207"; "E208"]

let private e203 =
    mkExplanation
        "E203"
        "Infinite type"
        "type refers to itself (occurs check)"
        "A type definition would be infinitely recursive. This happens when a type variable appears in its own definition, like 'a = 'a -> int."
        "let f x = f"
        "let f x = x"
        ["E201"]

let private e204 =
    mkExplanation
        "E204"
        "Not a function"
        "cannot apply arguments to non-function"
        "Attempted to call something that is not a function. Only functions (created with 'fun' or let bindings with parameters) can be applied."
        "let x = 5 in x 10"
        "let f x = x + 1 in f 10"
        ["E201"; "E205"]

let private e205 =
    mkExplanation
        "E205"
        "Arity mismatch"
        "wrong number of arguments"
        "A function was called with the wrong number of arguments. Check the function definition to see how many parameters it expects."
        "let add x y = x + y in add 1"
        "let add x y = x + y in add 1 2"
        ["E204"]

let private e206 =
    mkExplanation
        "E206"
        "Pattern type mismatch"
        "pattern incompatible with matched value"
        "A pattern doesn't match the type of the value being matched. For example, matching an integer with a list pattern."
        "match 42 with | [] -> 0"
        "match [1,2] with | [] -> 0 | _ -> 1"
        ["E201"; "E302"]

let private e207 =
    mkExplanation
        "E207"
        "Undefined constructor"
        "unknown type constructor"
        "A type constructor was used that hasn't been defined. Make sure the type is defined before using its constructors."
        "Some 42"
        "type Option a = None | Some a\nSome 42"
        ["E202"]

let private e208 =
    mkExplanation
        "E208"
        "Duplicate binding"
        "variable already defined in scope"
        "A variable with this name already exists in the current scope. Use a different name or shadow intentionally in a nested scope."
        "let x = 1 in let x = 2 in x"
        "let x = 1 in let y = 2 in x + y"
        ["E202"]

// -----------------------------------------------------------------------------
// Runtime Errors (E300-E399)
// -----------------------------------------------------------------------------

let private e301 =
    mkExplanation
        "E301"
        "Division by zero"
        "cannot divide by zero"
        "Attempted to divide a number by zero, which is undefined. Check the divisor before performing division."
        "10 / 0"
        "let d = 2 in if d = 0 then 0 else 10 / d"
        []

let private e302 =
    mkExplanation
        "E302"
        "Non-exhaustive match"
        "no pattern matched the value"
        "A match expression didn't cover all possible cases and the runtime value didn't match any pattern. Add a wildcard pattern '_' for safety."
        "match xs with | [] -> 0"
        "match xs with | [] -> 0 | _ -> 1"
        ["E206"]

let private e303 =
    mkExplanation
        "E303"
        "Invalid operation"
        "operation not supported"
        "An operation was attempted that is not supported for the given types or values. Check the types of your operands."
        "\"hello\" + 5"
        "\"hello\" + \" world\""
        ["E201"]

let private e304 =
    mkExplanation
        "E304"
        "Stack overflow"
        "too many recursive calls"
        "The program ran out of stack space due to too many recursive calls. This usually indicates infinite recursion or very deep recursion."
        "let rec f n = f (n + 1) in f 0"
        "let rec f n = if n = 0 then 1 else n * f (n - 1) in f 5"
        []

// =============================================================================
// Error Explanations Registry
// =============================================================================

/// All error explanations indexed by code
let private explanations: Map<string, ErrorExplanation> =
    [
        // Lexer errors
        e001; e002; e003; e004
        // Parser errors
        e101; e102; e103; e104; e105; e106
        // Type errors
        e201; e202; e203; e204; e205; e206; e207; e208
        // Runtime errors
        e301; e302; e303; e304
    ]
    |> List.map (fun e -> e.Code, e)
    |> Map.ofList

// =============================================================================
// Public API
// =============================================================================

/// Get brief one-liner for inline display
let getBrief (code: string) : string option =
    explanations
    |> Map.tryFind code
    |> Option.map (fun e -> e.Brief)

/// Get full explanation for --explain
let get (code: string) : ErrorExplanation option =
    Map.tryFind code explanations

/// List all available error codes
let allCodes () : string list =
    explanations
    |> Map.toList
    |> List.map fst
    |> List.sort

/// Check if an error code has an explanation
let hasExplanation (code: string) : bool =
    Map.containsKey code explanations

// =============================================================================
// Formatting for --explain output
// =============================================================================

/// Format an explanation for terminal display
let formatExplanation (e: ErrorExplanation) : string =
    let sep = String.replicate (e.Title.Length + 12) "="
    let lines = [
        sprintf "Error %s: %s" e.Code e.Title
        sep
        ""
        e.Explanation
        ""
        "Example of incorrect code:"
        "--------------------------"
        e.BadExample
        ""
        "How to fix:"
        "-----------"
        e.GoodExample
    ]

    let relatedSection =
        if List.isEmpty e.RelatedCodes then []
        else
            [ ""
              "Related errors:"
              yield! e.RelatedCodes |> List.map (fun c ->
                  match get c with
                  | Some r -> sprintf "- %s: %s" c r.Title
                  | None -> sprintf "- %s" c) ]

    (lines @ relatedSection) |> String.concat "\n"

/// Format multiple explanations for terminal display
let formatExplanations (codes: string list) : string =
    codes
    |> List.choose get
    |> List.map formatExplanation
    |> String.concat "\n\n"

/// List all error codes with their titles
let formatAllCodes () : string =
    let header = "FunLang Error Codes\n==================="
    let codes =
        allCodes ()
        |> List.map (fun code ->
            let title = (get code |> Option.get).Title
            sprintf "%s: %s" code title)
    header + "\n\n" + String.concat "\n" codes
