module FixityEnv

open Ast

/// Fixity information for an operator
type FixityInfo = { Assoc: Assoc; Prec: int }

/// Map from operator name to its declared fixity
type FixityEnv = Map<string, FixityInfo>

/// Determine default fixity from first character (mirrors Lexer.classifyOperator).
/// Precedence mapping: INFIXOP0=4, INFIXOP1=5, INFIXOP2=6, INFIXOP3=7, INFIXOP4=8
let defaultFixity (op: string) : FixityInfo =
    let eff = op.TrimStart('.')
    if eff.Length = 0 then { Assoc = Left; Prec = 7 }  // pure dots -> INFIXOP3 multiplicative
    elif eff.Length >= 2 && eff.[0] = '*' && eff.[1] = '*' then { Assoc = Right; Prec = 8 }  // INFIXOP4
    else
        match eff.[0] with
        | '=' | '<' | '>' | '|' | '&' | '$' | '!' -> { Assoc = Left; Prec = 4 }   // INFIXOP0
        | '@' | '^' -> { Assoc = Right; Prec = 5 }                                   // INFIXOP1
        | '+' | '-' -> { Assoc = Left; Prec = 6 }                                    // INFIXOP2
        | '*' | '/' | '%' -> { Assoc = Left; Prec = 7 }                              // INFIXOP3
        | _ -> { Assoc = Left; Prec = 4 }                                             // fallback -> INFIXOP0

/// Collect fixity declarations from a list of module-level declarations.
/// Accumulates into the existing FixityEnv.
let rec collectFixity (existing: FixityEnv) (decls: Decl list) : FixityEnv =
    (existing, decls) ||> List.fold (fun env decl ->
        match decl with
        | InfixDecl(attrs, name, _, _) ->
            match attrs |> List.tryPick (function FixityAttr(assoc, prec) -> Some(assoc, prec)) with
            | Some(assoc, prec) -> Map.add name { Assoc = assoc; Prec = prec } env
            | None -> env
        | ModuleDecl(_, innerDecls, _) -> collectFixity env innerDecls
        | _ -> env)

/// Look up an operator's fixity, falling back to default if not declared.
let lookupFixity (env: FixityEnv) (op: string) : FixityInfo =
    match Map.tryFind op env with
    | Some info -> info
    | None -> defaultFixity op

/// Check if a name looks like an operator (starts with operator characters).
/// Must match the character set that Lexer.classifyOperator handles.
let private isOperator (name: string) : bool =
    name.Length > 0 &&
    let c = name.[0]
    c = '!' || c = '#' || c = '$' || c = '%' || c = '&' || c = '*' ||
    c = '+' || c = '-' || c = '.' || c = '/' || c = ':' || c = '<' ||
    c = '=' || c = '>' || c = '?' || c = '@' || c = '\\' || c = '^' ||
    c = '|' || c = '~'

/// Try to extract an operator name and its (lhs, rhs) from a curried infix application.
/// Infix `a op b` is parsed as App(App(Var(op, _), a, _), b, _).
/// Returns Some (op, lhs, rhs) if the expression matches that shape.
let private tryExtractInfix (expr: Expr) : (string * Expr * Expr) option =
    match expr with
    | App(App(Var(op, _), lhs, _), rhs, _) when isOperator op -> Some (op, lhs, rhs)
    | _ -> None

/// Flatten a left-associative chain of infix applications at the same LALR level.
/// Returns (operands, operators) where len(operands) = len(operators) + 1.
///
/// Example: `((a +++ b) +++ c)` -> ([a; b; c], [+++; +++])
///
/// The LALR parser groups same-level operators left-associatively, so we walk
/// the left spine. We stop when we encounter an operator at a different default
/// precedence level (LALR groups them at different grammar levels).
let private flattenInfixChain (expr: Expr) : (Expr list * string list) option =
    match tryExtractInfix expr with
    | None -> None
    | Some(outerOp, lhs, rhs) ->
        let outerDefaultPrec = (defaultFixity outerOp).Prec
        // Walk the left spine collecting operands and operators
        // The left spine of ((a op b) op c) op d is a chain of op applications
        let rec walkLeft (e: Expr) : Expr list * string list =
            match tryExtractInfix e with
            | Some(op, l, r) when (defaultFixity op).Prec = outerDefaultPrec ->
                let (leftOperands, leftOps) = walkLeft l
                (leftOperands @ [r], leftOps @ [op])
            | _ ->
                // Base case: this is the leftmost operand
                ([e], [])
        let (innerOperands, innerOps) = walkLeft lhs
        // Full chain: innerOperands[0], innerOps[0], innerOperands[1], ..., outerOp, rhs
        let allOperands = innerOperands @ [rhs]
        let allOps = innerOps @ [outerOp]
        Some (allOperands, allOps)

/// Check if any operator in the chain has fixity that differs from the LALR default.
let private needsRewrite (env: FixityEnv) (ops: string list) : bool =
    ops |> List.exists (fun op ->
        match Map.tryFind op env with
        | Some info ->
            let dflt = defaultFixity op
            info.Assoc <> dflt.Assoc || info.Prec <> dflt.Prec
        | None -> false)

/// Build a single infix application node: lhs `op` rhs
let private mkInfix (op: string) (opSpan: Span) (lhs: Expr) (rhs: Expr) : Expr =
    App(App(Var(op, opSpan), lhs, opSpan), rhs, opSpan)

/// Rebuild a flat operand/operator chain using fixity information.
/// operands has length = operators + 1.
///
/// For same-op chains: use the declared associativity directly.
/// For mixed-op chains: use precedence climbing (reduce highest-prec operators first).
let private rebuildChain (env: FixityEnv) (operands: Expr list) (ops: string list) (span: Span) : Expr =
    if ops.Length = 0 then
        List.head operands
    // All operators are the same — use their common declared associativity
    elif ops |> List.distinct |> List.length = 1 then
        let info = lookupFixity env ops.[0]
        match info.Assoc with
        | Left ->
            // fold left: ((a op b) op c)
            let first = operands.[0]
            List.fold2 (fun acc op rhs ->
                mkInfix op span acc rhs
            ) first ops (List.tail operands)
        | Right ->
            // fold right: (a op (b op c))
            // Work right-to-left: start from the last operand
            let revOps = List.rev ops
            let revOperands = List.rev operands
            let last = revOperands.[0]
            List.fold2 (fun acc op lhs ->
                mkInfix op span lhs acc
            ) last revOps (List.tail revOperands)
    else
        // Mixed operators at the same LALR level with different FixityEnv precedences.
        // Use precedence climbing: repeatedly reduce the highest-precedence operator.
        let mutable items : Expr list = operands
        let mutable operators : string list = ops

        while operators.Length > 0 do
            // Find the maximum precedence among remaining operators
            let maxPrec =
                operators |> List.map (fun op -> (lookupFixity env op).Prec) |> List.max
            // Get associativity of operators at maxPrec
            let assocAtMax =
                operators
                |> List.pick (fun op ->
                    let info = lookupFixity env op
                    if info.Prec = maxPrec then Some info.Assoc else None)
            // Find all operator indices at maxPrec
            let indicesAtMax =
                operators
                |> List.mapi (fun i op ->
                    let info = lookupFixity env op
                    if info.Prec = maxPrec then Some i else None)
                |> List.choose id
            // Pick one index to reduce based on associativity
            let reduceIdx =
                match assocAtMax with
                | Left -> List.head indicesAtMax     // leftmost for left-assoc
                | Right -> List.last indicesAtMax    // rightmost for right-assoc
            // Reduce: items[reduceIdx] op items[reduceIdx+1] -> combined
            let combined = mkInfix operators.[reduceIdx] span items.[reduceIdx] items.[reduceIdx + 1]
            // Replace items[reduceIdx] with combined, remove items[reduceIdx+1]
            let newItems =
                items
                |> List.mapi (fun i e -> if i = reduceIdx then Some combined elif i = reduceIdx + 1 then None else Some e)
                |> List.choose id
            // Remove operator at reduceIdx
            let newOps =
                operators
                |> List.mapi (fun i op -> if i = reduceIdx then None else Some op)
                |> List.choose id
            items <- newItems
            operators <- newOps

        List.head items

/// Apply a mapping function to all immediate child expressions of an Expr node.
let private mapExprChildren (f: Expr -> Expr) (expr: Expr) : Expr =
    match expr with
    | Number _ | Bool _ | String _ | Char _ | Var _ | EmptyList _ -> expr
    | Add(a, b, s) -> Add(f a, f b, s)
    | Subtract(a, b, s) -> Subtract(f a, f b, s)
    | Multiply(a, b, s) -> Multiply(f a, f b, s)
    | Divide(a, b, s) -> Divide(f a, f b, s)
    | Negate(a, s) -> Negate(f a, s)
    | Let(n, e1, e2, s) -> Let(n, f e1, f e2, s)
    | LetPat(p, e1, e2, s) -> LetPat(p, f e1, f e2, s)
    | LetRec(bindings, body, s) ->
        let newBindings = bindings |> List.map (fun (n, param, ty, spOpt, e, bs) -> (n, param, ty, spOpt, f e, bs))
        LetRec(newBindings, f body, s)
    | If(cond, t, e, s) -> If(f cond, f t, f e, s)
    | Equal(a, b, s) -> Equal(f a, f b, s)
    | NotEqual(a, b, s) -> NotEqual(f a, f b, s)
    | LessThan(a, b, s) -> LessThan(f a, f b, s)
    | GreaterThan(a, b, s) -> GreaterThan(f a, f b, s)
    | LessEqual(a, b, s) -> LessEqual(f a, f b, s)
    | GreaterEqual(a, b, s) -> GreaterEqual(f a, f b, s)
    | And(a, b, s) -> And(f a, f b, s)
    | Or(a, b, s) -> Or(f a, f b, s)
    | Lambda(p, body, s) -> Lambda(p, f body, s)
    | LambdaAnnot(p, ty, body, s) -> LambdaAnnot(p, ty, f body, s)
    | App(func, arg, s) -> App(f func, f arg, s)
    | Tuple(es, s) -> Tuple(List.map f es, s)
    | List(es, s) -> List(List.map f es, s)
    | Cons(h, t, s) -> Cons(f h, f t, s)
    | Match(scrutinee, clauses, s) ->
        let newClauses = clauses |> List.map (fun (p, guard, body) -> (p, Option.map f guard, f body))
        Match(f scrutinee, newClauses, s)
    | Constructor(name, arg, s) -> Constructor(name, Option.map f arg, s)
    | Annot(e, ty, s) -> Annot(f e, ty, s)
    | RecordExpr(tn, fields, s) -> RecordExpr(tn, fields |> List.map (fun (n, e) -> (n, f e)), s)
    | FieldAccess(e, fn, s) -> FieldAccess(f e, fn, s)
    | RecordUpdate(src, fields, s) -> RecordUpdate(f src, fields |> List.map (fun (n, e) -> (n, f e)), s)
    | SetField(e, fn, v, s) -> SetField(f e, fn, f v, s)
    | Raise(e, s) -> Raise(f e, s)
    | TryWith(body, handlers, s) ->
        let newHandlers = handlers |> List.map (fun (p, guard, body2) -> (p, Option.map f guard, f body2))
        TryWith(f body, newHandlers, s)
    | Range(start, stop, step, s) -> Range(f start, f stop, Option.map f step, s)
    | Modulo(a, b, s) -> Modulo(f a, f b, s)
    | LetMut(n, v, body, s) -> LetMut(n, f v, f body, s)
    | Assign(n, v, s) -> Assign(n, f v, s)
    | WhileExpr(cond, body, s) -> WhileExpr(f cond, f body, s)
    | ForExpr(var, start, isTo, stop, body, s) -> ForExpr(var, f start, isTo, f stop, f body, s)
    | ForInExpr(var, coll, body, s) -> ForInExpr(var, f coll, f body, s)
    | IndexGet(coll, idx, s) -> IndexGet(f coll, f idx, s)
    | IndexSet(coll, idx, v, s) -> IndexSet(f coll, f idx, f v, s)
    | StringSliceExpr(str, start, stop, s) -> StringSliceExpr(f str, f start, Option.map f stop, s)
    | ListCompExpr(var, coll, body, s) -> ListCompExpr(var, f coll, f body, s)

/// Rewrite a module's AST to apply fixity overrides.
/// If env is empty, returns module unchanged (optimization).
let rewriteFixity (env: FixityEnv) (m: Module) : Module =
    if Map.isEmpty env then m
    else
    let rec rewriteExpr (e: Expr) : Expr =
        match flattenInfixChain e with
        | Some(operands, ops) when needsRewrite env ops ->
            // Rewrite child operands first, then rebuild with correct fixity
            let rewrittenOperands = operands |> List.map rewriteExpr
            rebuildChain env rewrittenOperands ops (spanOf e)
        | _ ->
            // No rewrite needed at this level — recurse into children
            mapExprChildren rewriteExpr e

    let rec rewriteDecl (d: Decl) : Decl =
        match d with
        | LetDecl(name, body, s) -> LetDecl(name, rewriteExpr body, s)
        | LetPatDecl(pat, body, s) -> LetPatDecl(pat, rewriteExpr body, s)
        | InfixDecl(attrs, name, body, s) -> InfixDecl(attrs, name, rewriteExpr body, s)
        | LetRecDecl(bindings, s) ->
            let newBindings = bindings |> List.map (fun (n, param, ty, spOpt, e, bs) -> (n, param, ty, spOpt, rewriteExpr e, bs))
            LetRecDecl(newBindings, s)
        | LetMutDecl(name, body, s) -> LetMutDecl(name, rewriteExpr body, s)
        | ModuleDecl(name, decls, s) -> ModuleDecl(name, List.map rewriteDecl decls, s)
        | InstanceDecl(cn, it, methods, constraints, s) ->
            let newMethods = methods |> List.map (fun (n, e) -> (n, rewriteExpr e))
            InstanceDecl(cn, it, newMethods, constraints, s)
        | _ -> d

    let rewriteDecls = List.map rewriteDecl

    match m with
    | Module(decls, s) -> Module(rewriteDecls decls, s)
    | NamedModule(name, decls, s) -> NamedModule(name, rewriteDecls decls, s)
    | EmptyModule _ -> m
