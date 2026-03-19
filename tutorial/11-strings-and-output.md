# Chapter 11: Strings and Output

## String Literals

String literals use double quotes with standard escape sequences:

```
funlang> "hello world"
"hello world"

funlang> "tab\there"
"tab	here"
```

## String Concatenation

The `+` operator concatenates strings:

```
funlang> "hello" + " " + "world"
"hello world"
```

## Built-in String Functions

### string_length

Returns the number of characters:

```
funlang> string_length "hello"
5

funlang> string_length ""
0
```

### string_concat

Curried concatenation -- takes two strings:

```
funlang> string_concat "hello" " world"
"hello world"
```

Since it is curried, partial application works:

```
$ cat prefix.l3
let add_prefix = string_concat "prefix:"
let result = add_prefix "value"

$ langthree prefix.l3
"prefix:value"
```

### string_sub

Extract a substring with start index and length:

```
funlang> string_sub "hello" 1 3
"ell"
```

`string_sub s start len` returns characters from index `start` for `len` characters.
Indices are zero-based.

### string_contains

Check if a string contains a substring:

```
funlang> string_contains "hello world" "world"
true

funlang> string_contains "hello" "xyz"
false
```

### to_string

Convert values to their string representation:

```
funlang> to_string 42
"42"

funlang> to_string true
"true"

funlang> to_string "already a string"
"already a string"
```

Accepts `int`, `bool`, and `string` at runtime.

### string_to_int

Parse a string as an integer:

```
funlang> string_to_int "123"
123
```

## Print Functions

### print

Writes a string to output with no newline, returns unit:

```
funlang> print "hello"
hello()
```

In the interactive session, the side-effect text appears immediately before the `()` result.

### println

Writes a string with a trailing newline, returns unit:

```
funlang> println "hello"
hello
()
```

### printf

Formatted output using specifiers:

| Specifier | Type | Example |
|-----------|------|---------|
| `%d` | int | `printf "%d" 42` |
| `%s` | string | `printf "%s" "hi"` |
| `%b` | bool | `printf "%b" true` |
| `%%` | literal `%` | `printf "100%%"` |

`printf` is curried -- each specifier consumes one argument:

```
$ cat printf_demo.l3
let _ = printf "%s is %d years old\n" "Alice" 30
let result = 0

$ langthree printf_demo.l3
Alice is 30 years old
0
```

Multiple specifiers:

```
$ cat printf_multi.l3
let _ = printf "name=%s, active=%b\n" "Bob" true
let result = 0

$ langthree printf_multi.l3
name=Bob, active=true
0
```

## Side-Effect Sequencing

Use `let _ =` to sequence operations that return unit:

```
$ cat sequence.l3
let _ = println "first"
let _ = println "second"
let _ = println "third"
let result = "done"

$ langthree sequence.l3
first
second
third
"done"
```

Each `let _ =` binds the unit result and discards it, ensuring
the side effects execute in order.

## Pipe with String Functions

String functions work naturally with the pipe operator:

```
funlang> "hello" |> string_length
5
```

Build pipelines with string transformations:

```
$ cat string_pipe.l3
let result = 42 |> to_string |> string_concat "answer: "

$ langthree string_pipe.l3
"answer: 42"
```

## Practical Example: Formatted Report

Combining string operations for structured output:

```
$ cat report.l3
let format_line label value =
    label + ": " + to_string value
let _ = println (format_line "width" 800)
let _ = println (format_line "height" 600)
let result = "report complete"

$ langthree report.l3
width: 800
height: 600
"report complete"
```

## Notes

- **`string_sub` uses start+length** (not start+end): `string_sub "hello" 1 3` = `"ell"`
- **`string_concat` is curried:** `string_concat "prefix"` returns a function
- **`to_string`** accepts int, bool, and string (polymorphic type, runtime-checked)
- **`printf`** is curried: each `%` specifier consumes one additional argument
- **Sequencing:** Use `let _ =` at module level to chain side effects
