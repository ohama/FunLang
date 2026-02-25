module Ast

open FSharp.Text.Lexing

/// Source location span for error messages
type Span = {
    FileName: string
    StartLine: int
    StartColumn: int
    EndLine: int
    EndColumn: int
}

/// Create span from FsLexYacc Position records
let mkSpan (startPos: Position) (endPos: Position) : Span =
    {
        FileName = startPos.FileName
        StartLine = startPos.Line
        StartColumn = startPos.Column
        EndLine = endPos.Line
        EndColumn = endPos.Column
    }

/// Sentinel span for built-in/synthetic definitions (like F# compiler's range0)
let unknownSpan : Span =
    {
        FileName = "<unknown>"
        StartLine = 0
        StartColumn = 0
        EndLine = 0
        EndColumn = 0
    }

/// Format span for error messages
let formatSpan (span: Span) : string =
    if span = unknownSpan then
        "<unknown location>"
    elif span.StartLine = span.EndLine then
        sprintf "%s:%d:%d-%d" span.FileName span.StartLine span.StartColumn span.EndColumn
    else
        sprintf "%s:%d:%d-%d:%d" span.FileName span.StartLine span.StartColumn span.EndLine span.EndColumn

/// Expression AST for arithmetic operations
/// Phase 2: Arithmetic expressions with precedence
/// Phase 3: Variables and let binding
/// Phase 4: Control flow, comparisons, and logical operators
/// Phase 5: Functions (Lambda, App, LetRec)
/// v5.0: Every variant carries span as last parameter for error diagnostics
type Expr =
    | Number of int * span: Span
    | Add of Expr * Expr * span: Span
    | Subtract of Expr * Expr * span: Span
    | Multiply of Expr * Expr * span: Span
    | Divide of Expr * Expr * span: Span
    | Negate of Expr * span: Span  // Unary minus
    // Phase 3: Variables
    | Var of string * span: Span           // Variable reference
    | Let of string * Expr * Expr * span: Span  // let name = expr1 in expr2
    // Phase 4: Control flow
    | Bool of bool * span: Span            // Boolean literal (true, false)
    | If of Expr * Expr * Expr * span: Span  // if condition then expr1 else expr2
    // Phase 2 (v2.0): Strings
    | String of string * span: Span        // String literal
    // Phase 4: Comparison operators (return BoolValue)
    | Equal of Expr * Expr * span: Span       // =
    | NotEqual of Expr * Expr * span: Span    // <>
    | LessThan of Expr * Expr * span: Span    // <
    | GreaterThan of Expr * Expr * span: Span // >
    | LessEqual of Expr * Expr * span: Span   // <=
    | GreaterEqual of Expr * Expr * span: Span // >=
    // Phase 4: Logical operators (short-circuit evaluation)
    | And of Expr * Expr * span: Span  // &&
    | Or of Expr * Expr * span: Span   // ||
    // Phase 5: Functions
    | Lambda of param: string * body: Expr * span: Span      // fun param -> body
    | App of func: Expr * arg: Expr * span: Span             // func arg (function application)
    | LetRec of name: string * param: string * body: Expr * inExpr: Expr * span: Span
    // let rec name param = body in inExpr
    // Phase 1 (v3.0): Tuples
    | Tuple of Expr list * span: Span               // Tuple expression: (e1, e2, ...)
    | LetPat of Pattern * Expr * Expr * span: Span  // Let with pattern binding: let pat = expr in body
    // Phase 2 (v3.0): Lists
    | EmptyList of span: Span                       // Empty list: []
    | List of Expr list * span: Span                // List literal: [e1, e2, ...]
    | Cons of Expr * Expr * span: Span              // Cons operator: h :: t
    // Phase 3 (v3.0): Pattern Matching
    | Match of scrutinee: Expr * clauses: MatchClause list * span: Span
    // v6.0: Type annotations
    | Annot of expr: Expr * typeExpr: TypeExpr * span: Span          // (e : T)
    | LambdaAnnot of param: string * paramType: TypeExpr * body: Expr * span: Span  // fun (x: T) -> e

/// Pattern for destructuring bindings
/// Phase 1 (v3.0): Tuple patterns
/// Phase 3 (v3.0): Extended with ConsPat, EmptyListPat, ConstPat
/// v5.0: Every variant carries span as last parameter for error diagnostics
and Pattern =
    | VarPat of string * span: Span           // Variable pattern: x
    | TuplePat of Pattern list * span: Span   // Tuple pattern: (p1, p2, ...)
    | WildcardPat of span: Span               // Wildcard pattern: _
    // Phase 3 (v3.0): New pattern types for match expressions
    | ConsPat of Pattern * Pattern * span: Span     // Cons pattern: h :: t
    | EmptyListPat of span: Span                    // Empty list pattern: []
    | ConstPat of Constant * span: Span             // Constant pattern: 1, true, false

/// Match clause: pattern -> expression
/// Phase 3 (v3.0)
and MatchClause = Pattern * Expr

/// Constant values for patterns
/// Phase 3 (v3.0)
and Constant =
    | IntConst of int
    | BoolConst of bool

/// Type expression AST for type annotations
/// v6.0: Bidirectional type system
and TypeExpr =
    | TEInt                               // int
    | TEBool                              // bool
    | TEString                            // string
    | TEList of TypeExpr                  // T list
    | TEArrow of TypeExpr * TypeExpr      // T1 -> T2 (right-associative)
    | TETuple of TypeExpr list            // T1 * T2 * ... (n >= 2)
    | TEVar of string                     // 'a, 'b (includes apostrophe)

/// Value type for evaluation results
/// Phase 4: Heterogeneous types (int and bool)
/// Phase 5: FunctionValue for first-class functions (mutual recursion with Expr, Env)
and Value =
    | IntValue of int
    | BoolValue of bool
    | FunctionValue of param: string * body: Expr * closure: Env
    | StringValue of string   // v2.0: String values
    | TupleValue of Value list  // v3.0: Tuple values
    | ListValue of Value list  // v3.0: List values

/// Environment mapping variable names to values
/// Phase 5: Defined here for mutual recursion with Value
and Env = Map<string, Value>

/// Extract span from any Expr
let spanOf (expr: Expr) : Span =
    match expr with
    | Number(_, s) | Bool(_, s) | String(_, s) | Var(_, s) -> s
    | Add(_, _, s) | Subtract(_, _, s) | Multiply(_, _, s) | Divide(_, _, s) -> s
    | Negate(_, s) -> s
    | Let(_, _, _, s) | LetPat(_, _, _, s) | LetRec(_, _, _, _, s) -> s
    | If(_, _, _, s) -> s
    | Equal(_, _, s) | NotEqual(_, _, s) -> s
    | LessThan(_, _, s) | GreaterThan(_, _, s) | LessEqual(_, _, s) | GreaterEqual(_, _, s) -> s
    | And(_, _, s) | Or(_, _, s) -> s
    | Lambda(_, _, s) | App(_, _, s) -> s
    | Tuple(_, s) | EmptyList s | List(_, s) | Cons(_, _, s) -> s
    | Match(_, _, s) -> s
    | Annot(_, _, s) | LambdaAnnot(_, _, _, s) -> s

/// Extract span from any Pattern
let patternSpanOf (pat: Pattern) : Span =
    match pat with
    | VarPat(_, s) | WildcardPat s | TuplePat(_, s) -> s
    | ConsPat(_, _, s) | EmptyListPat s | ConstPat(_, s) -> s
