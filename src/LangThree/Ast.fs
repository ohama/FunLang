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
    // Phase 29: Char literals
    | Char of char * span: Span            // Char literal: 'A'
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
    | LetRec of bindings: (string * string * TypeExpr option * Expr * Span) list * inExpr: Expr * span: Span
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
    // Phase 2 (ADT): Constructor expression
    | Constructor of name: string * arg: Expr option * span: Span
    // v6.0: Type annotations
    | Annot of expr: Expr * typeExpr: TypeExpr * span: Span          // (e : T)
    | LambdaAnnot of param: string * paramType: TypeExpr * body: Expr * span: Span  // fun (x: T) -> e
    // Phase 3 (Records): Record expressions
    | RecordExpr of typeName: string option * fields: (string * Expr) list * span: Span
    | FieldAccess of expr: Expr * fieldName: string * span: Span
    | RecordUpdate of source: Expr * fields: (string * Expr) list * span: Span
    // Phase 3 (Records-06): Mutable field assignment
    | SetField of expr: Expr * fieldName: string * value: Expr * span: Span
    // Phase 6 (Exceptions): Raise and try-with
    | Raise of expr: Expr * span: Span
    | TryWith of body: Expr * handlers: MatchClause list * span: Span
    // Phase 9 (Pipe & Composition): Pipe and composition operators
    | PipeRight of left: Expr * right: Expr * span: Span       // x |> f
    | ComposeRight of left: Expr * right: Expr * span: Span    // f >> g
    | ComposeLeft of left: Expr * right: Expr * span: Span     // f << g
    // Phase 18 (Ranges): List range syntax
    | Range of start: Expr * stop: Expr * step: Expr option * Span
    // Phase 21: Modulo operator
    | Modulo of Expr * Expr * Span
    // Phase 42 (Mutable Variables): Mutable variable support
    | LetMut of name: string * value: Expr * body: Expr * span: Span
    | Assign of name: string * value: Expr * span: Span
    // Phase 46 (Loop Constructs): while and for loops
    | WhileExpr of cond: Expr * body: Expr * span: Span
    | ForExpr of var: string * start: Expr * isTo: bool * stop: Expr * body: Expr * span: Span
    // Phase 51 (For-In Collection Loops): for-in loop
    | ForInExpr of var: Pattern * collection: Expr * body: Expr * span: Span
    // Phase 47 (Array/Hashtable Indexing): index access
    | IndexGet of collection: Expr * index: Expr * span: Span
    | IndexSet of collection: Expr * index: Expr * value: Expr * span: Span
    // Phase 58 (String Slicing): s.[start..stop] inclusive, s.[start..] to end
    | StringSliceExpr of str: Expr * start: Expr * stop: Expr option * span: Span
    // Phase 58 (List Comprehension): [for x in coll -> expr] or [for i in start..stop -> expr]
    | ListCompExpr of var: string * collection: Expr * body: Expr * span: Span

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
    // Phase 2 (ADT): Constructor pattern for ADT matching
    | ConstructorPat of name: string * argPattern: Pattern option * span: Span
    // Phase 3 (Records): Record pattern
    | RecordPat of fields: (string * Pattern) list * span: Span
    // Phase 16: Or-pattern (multiple alternatives share one body)
    | OrPat of Pattern list * span: Span

/// Match clause: pattern -> when guard -> expression
/// Phase 3 (v3.0), Phase 6: Extended with optional when guard
and MatchClause = Pattern * Expr option * Expr

/// Constant values for patterns
/// Phase 3 (v3.0)
and Constant =
    | IntConst of int
    | BoolConst of bool
    | StringConst of string
    | CharConst of char   // Phase 29: Char literal pattern

/// Type expression AST for type annotations
/// v6.0: Bidirectional type system
and TypeExpr =
    | TEInt                               // int
    | TEBool                              // bool
    | TEString                            // string
    | TEChar                              // char (Phase 29)
    | TEList of TypeExpr                  // T list
    | TEArrow of TypeExpr * TypeExpr      // T1 -> T2 (right-associative)
    | TETuple of TypeExpr list            // T1 * T2 * ... (n >= 2)
    | TEVar of string                     // 'a, 'b (includes apostrophe)
    | TEName of string                    // Named type: Tree, Option, etc. (Phase 2 ADT-01)
    | TEData of name: string * args: TypeExpr list  // Parameterized named type: int expr, 'a option (Phase 4 GADT)

/// Type declaration AST for algebraic data types (discriminated unions)
/// Phase 2 (ADT-01): F# discriminated union syntax
and TypeDecl =
    | TypeDecl of name: string * typeParams: string list * constructors: ConstructorDecl list * Span

/// Constructor definition within a type declaration
and ConstructorDecl =
    | ConstructorDecl of name: string * dataType: TypeExpr option * Span
    | GadtConstructorDecl of name: string * argTypes: TypeExpr list * returnType: TypeExpr * Span

/// Record field declaration
/// Phase 3 (Records): Named, typed fields with optional mutability
and RecordFieldDecl =
    | RecordFieldDecl of name: string * fieldType: TypeExpr * isMutable: bool * Span

/// Record type declaration
/// Phase 3 (Records): type T = { field1: Type1; field2: Type2 }
and RecordDecl =
    | RecordDecl of name: string * typeParams: string list * fields: RecordFieldDecl list * Span

/// Value type for evaluation results
/// Phase 4: Heterogeneous types (int and bool)
/// Phase 5: FunctionValue for first-class functions (mutual recursion with Expr, Env)
/// Phase 11: CustomEquality/CustomComparison required because BuiltinValue carries a function type
/// which F# cannot derive equality for automatically.
and [<CustomEquality; CustomComparison>] Value =
    | IntValue of int
    | BoolValue of bool
    | FunctionValue of param: string * body: Expr * closure: Env
    | StringValue of string   // v2.0: String values
    | CharValue of char       // Phase 29: Char values
    | TupleValue of Value list  // v3.0: Tuple values
    | ListValue of Value list  // v3.0: List values
    | DataValue of constructor: string * value: Value option  // Phase 2 (ADT): ADT value
    | RecordValue of typeName: string * fields: Map<string, Value ref>  // Phase 3 (Records): Record value (ref cells for mutable field support)
    | BuiltinValue of fn: (Value -> Value)  // Phase 11: Native F# built-in function carrier
    | TailCall of func: Value * arg: Value  // Phase 15: Deferred tail call for trampoline TCO
    | ArrayValue of Value array             // Phase 38: Mutable fixed-size array
    | HashtableValue of System.Collections.Generic.Dictionary<Value, Value>  // Phase 39: Mutable key-value hashtable
    | RefValue of Value ref  // Phase 42: Mutable variable ref cell (transparent to user code)
    | StringBuilderValue of System.Text.StringBuilder  // Phase 55: Mutable string builder
    | HashSetValue of System.Collections.Generic.HashSet<Value>  // Phase 56: Mutable unique-element set
    | QueueValue of System.Collections.Generic.Queue<Value>       // Phase 56: Mutable FIFO queue
    | MutableListValue of System.Collections.Generic.List<Value>  // Phase 57: Mutable resizable list

    override x.Equals(obj) =
        match obj with
        | :? Value as y -> Value.valueEqual x y
        | _ -> false

    override x.GetHashCode() =
        match x with
        | IntValue n -> hash n
        | BoolValue b -> hash b
        | StringValue s -> hash s
        | CharValue c -> hash c
        | FunctionValue(p, _, _) -> hash p
        | TupleValue vs -> hash vs
        | ListValue vs -> hash vs
        | DataValue(ctor, v) -> hash (ctor, v)
        | RecordValue(name, _) -> hash name
        | BuiltinValue _ -> 0
        | TailCall _ -> 0
        | ArrayValue arr -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(arr)
        | HashtableValue ht -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ht)
        | RefValue r -> hash (!r)
        | StringBuilderValue sb -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sb)
        | HashSetValue hs -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(hs)
        | QueueValue q    -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(q)
        | MutableListValue ml -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ml)

    interface System.IEquatable<Value> with
        member x.Equals(y: Value) = Value.valueEqual x y

    interface System.IComparable with
        member x.CompareTo(obj) =
            match obj with
            | :? Value as y -> Value.valueCompare x y
            | _ -> 0

    static member valueEqual (x: Value) (y: Value) =
        match x, y with
        | IntValue a, IntValue b -> a = b
        | BoolValue a, BoolValue b -> a = b
        | StringValue a, StringValue b -> a = b
        | CharValue a, CharValue b -> a = b
        | TupleValue a, TupleValue b -> a = b
        | ListValue a, ListValue b -> a = b
        | DataValue(c1, v1), DataValue(c2, v2) -> c1 = c2 && v1 = v2
        | RecordValue(n1, f1), RecordValue(n2, f2) -> n1 = n2 && f1 = f2
        | FunctionValue(p1, b1, _), FunctionValue(p2, b2, _) -> p1 = p2 && b1 = b2
        | BuiltinValue _, BuiltinValue _ -> false  // functions are never equal
        | TailCall _, _ | _, TailCall _ -> false  // TailCall is transient, never compared
        | ArrayValue r1, ArrayValue r2 -> System.Object.ReferenceEquals(r1, r2)
        | HashtableValue h1, HashtableValue h2 -> System.Object.ReferenceEquals(h1, h2)
        | StringBuilderValue sb1, StringBuilderValue sb2 -> System.Object.ReferenceEquals(sb1, sb2)
        | HashSetValue h1, HashSetValue h2 -> System.Object.ReferenceEquals(h1, h2)
        | QueueValue q1,   QueueValue q2   -> System.Object.ReferenceEquals(q1, q2)
        | MutableListValue ml1, MutableListValue ml2 -> System.Object.ReferenceEquals(ml1, ml2)
        | RefValue r1, RefValue r2 -> Value.valueEqual !r1 !r2
        | _ -> false

    static member valueCompare (x: Value) (y: Value) =
        match x, y with
        | IntValue a, IntValue b -> compare a b
        | BoolValue a, BoolValue b -> compare a b
        | StringValue a, StringValue b -> compare a b
        | CharValue a, CharValue b -> compare a b
        | TailCall _, _ | _, TailCall _ -> 0
        | ArrayValue _, _ | _, ArrayValue _ -> 0
        | HashtableValue _, _ | _, HashtableValue _ -> 0
        | StringBuilderValue _, _ | _, StringBuilderValue _ -> 0
        | HashSetValue _, _ | _, HashSetValue _ -> 0
        | QueueValue _,   _ | _, QueueValue _   -> 0
        | MutableListValue _, _ | _, MutableListValue _ -> 0
        | RefValue r1, RefValue r2 -> Value.valueCompare !r1 !r2
        | _ -> 0

/// Environment mapping variable names to values
/// Phase 5: Defined here for mutual recursion with Value
and Env = Map<string, Value>

/// Extract span from TypeDecl
let typeSpanOf (td: TypeDecl) : Span =
    match td with
    | TypeDecl(_, _, _, s) -> s

/// Extract span from ConstructorDecl
let constructorSpanOf (cd: ConstructorDecl) : Span =
    match cd with
    | ConstructorDecl(_, _, s) -> s
    | GadtConstructorDecl(_, _, _, s) -> s

/// Extract span from any Expr
let spanOf (expr: Expr) : Span =
    match expr with
    | Number(_, s) | Bool(_, s) | String(_, s) | Char(_, s) | Var(_, s) -> s
    | Add(_, _, s) | Subtract(_, _, s) | Multiply(_, _, s) | Divide(_, _, s) -> s
    | Negate(_, s) -> s
    | Let(_, _, _, s) | LetPat(_, _, _, s) | LetRec(_, _, s) -> s
    | If(_, _, _, s) -> s
    | Equal(_, _, s) | NotEqual(_, _, s) -> s
    | LessThan(_, _, s) | GreaterThan(_, _, s) | LessEqual(_, _, s) | GreaterEqual(_, _, s) -> s
    | And(_, _, s) | Or(_, _, s) -> s
    | Lambda(_, _, s) | App(_, _, s) -> s
    | Tuple(_, s) | EmptyList s | List(_, s) | Cons(_, _, s) -> s
    | Match(_, _, s) -> s
    | Constructor(_, _, s) -> s
    | Annot(_, _, s) | LambdaAnnot(_, _, _, s) -> s
    | RecordExpr(_, _, s) | FieldAccess(_, _, s) | RecordUpdate(_, _, s) | SetField(_, _, _, s) -> s
    | Raise(_, s) | TryWith(_, _, s) -> s
    | PipeRight(_, _, s) | ComposeRight(_, _, s) | ComposeLeft(_, _, s) -> s
    | Range(_, _, _, s) -> s
    | Modulo(_, _, s) -> s
    | LetMut(_, _, _, s) | Assign(_, _, s) -> s
    | WhileExpr(_, _, s) -> s
    | ForExpr(_, _, _, _, _, s) -> s
    | ForInExpr(_, _, _, s) -> s
    | IndexGet(_, _, s) | IndexSet(_, _, _, s) -> s
    | StringSliceExpr(_, _, _, s) -> s
    | ListCompExpr(_, _, _, s) -> s

/// Extract span from any Pattern
let patternSpanOf (pat: Pattern) : Span =
    match pat with
    | VarPat(_, s) | WildcardPat s | TuplePat(_, s) -> s
    | ConsPat(_, _, s) | EmptyListPat s | ConstPat(_, s) -> s
    | ConstructorPat(_, _, s) -> s
    | RecordPat(_, s) -> s
    | OrPat(_, s) -> s

/// Module-level declaration
/// Phase 1 (INDENT-05): Module-level declarations
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | LetPatDecl of pat: Pattern * body: Expr * Span   // Phase 28: N-tuple module-level binding
    | TypeDecl of TypeDecl  // Phase 2 (ADT-01): Type declaration (discriminated union)
    | RecordTypeDecl of RecordDecl  // Phase 3 (Records): Record type declaration
    // Phase 5 (Modules): Module system declarations
    | ModuleDecl of name: string * decls: Decl list * Span
    | OpenDecl of path: string list * Span
    | NamespaceDecl of path: string list * decls: Decl list * Span
    // Phase 6 (Exceptions): Exception declaration
    | ExceptionDecl of name: string * dataType: TypeExpr option * Span
    // Phase 17 (Type Aliases): Type alias declaration
    | TypeAliasDecl of name: string * typeParams: string list * body: TypeExpr * Span
    // Phase 18 (Mutual Recursion): Module-level let rec (single and mutual)
    | LetRecDecl of bindings: (string * string * TypeExpr option * Expr * Span) list * Span
    // Phase 31 (Module System): File-based import declaration
    | FileImportDecl of path: string * Span
    // Phase 42 (Mutable Variables): Module-level mutable variable declaration
    | LetMutDecl of name: string * body: Expr * Span

/// Module: Top-level container for declarations
/// Phase 1 (INDENT-05): Module structure for multi-declaration files
type Module =
    | Module of decls: Decl list * Span
    | NamedModule of name: string list * decls: Decl list * Span
    | NamespacedModule of name: string list * decls: Decl list * Span
    | EmptyModule of Span

/// Extract span from Decl
let declSpanOf (decl: Decl) : Span =
    match decl with
    | LetDecl(_, _, s) -> s
    | LetPatDecl(_, _, s) -> s
    | TypeDecl td -> typeSpanOf td
    | RecordTypeDecl (RecordDecl(_, _, _, s)) -> s
    | ModuleDecl(_, _, s) -> s
    | OpenDecl(_, s) -> s
    | NamespaceDecl(_, _, s) -> s
    | ExceptionDecl(_, _, s) -> s
    | TypeAliasDecl(_, _, _, s) -> s
    | LetRecDecl(_, s) -> s
    | FileImportDecl(_, s) -> s
    | LetMutDecl(_, _, s) -> s

/// Extract span from Module
let moduleSpanOf (m: Module) : Span =
    match m with
    | Module(_, s) | EmptyModule s -> s
    | NamedModule(_, _, s) -> s
    | NamespacedModule(_, _, s) -> s
