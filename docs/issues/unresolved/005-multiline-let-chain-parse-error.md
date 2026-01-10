# Issue 005: Parse Error with Multiple Multiline let rec Chains

## Status
Unresolved

## Created
2026-01-11

## Description
When chaining 5 or more `let rec` expressions with multiline `match` bodies, parsing fails with "parse error". The issue is related to indentation token handling.

## Reproduction
```funlang
-- This parses OK (4 multiline let rec)
let rec a = fun x -> match x with | [] -> [] | h :: t -> a t in
let rec b = fun x -> match x with | [] -> [] | h :: t -> b t in
let rec c = fun x -> match x with | [] -> [] | h :: t -> c t in
let rec d = fun x -> match x with | [] -> [] | h :: t -> d t in
d [1]

-- This fails (5 multiline let rec with proper formatting)
let rec a = fun x ->
  match x with
  | [] -> []
  | h :: t -> a t
in
let rec b = fun x ->
  match x with
  | [] -> []
  | h :: t -> b t
in
let rec c = fun x ->
  match x with
  | [] -> []
  | h :: t -> c t
in
let rec d = fun x ->
  match x with
  | [] -> []
  | h :: t -> d t
in
let rec e = fun x -> x
in
e [1]
```

## Analysis
- Single-line format works for any number of let rec chains
- Multiline format with indentation fails at 5+ chained let rec expressions
- Blank lines between let...in expressions also cause parse errors
- Issue likely in `Indentation.fs` token generation or parser grammar interaction

## Current Workaround
Use single-line format for complex function chains:
```funlang
let rec f = fun x -> match x with | [] -> [] | h :: t -> f t in
```

## Impact
- Medium: Limits code readability for complex programs
- Tree sort demo required single-line format

## Related Files
- `src/FunLang/Indentation.fs`
- `src/FunLang/Parser.fsy`
- `demos/013-tree-sort.fun` (uses workaround)

## Investigation Notes
- 4 multiline let rec chains work fine
- Adding 5th let rec (even with simple body) causes failure
- Without type definition, same issue occurs
- Blank lines between let expressions also trigger the issue
