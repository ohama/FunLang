# File-Based Testing Specification

## Overview

File-based testing allows running external commands on input files and comparing results against expected output.

## File Format

```
// --COMMAND: <command with %s placeholder>
// --INPUT
<input content>
// --EXPECTED
<expected result>
```

### Sections

| Section | Description |
|---------|-------------|
| `// --COMMAND:` | Command to execute. `%s` is replaced with input file path |
| `// --INPUT` | Start of input content |
| `// --EXPECTED` | Start of expected output |

## Execution Flow

```
1. Parse test file
   ├── Extract COMMAND line
   ├── Extract INPUT section
   └── Extract EXPECTED section

2. Create temporary files
   ├── <input file> ← INPUT content
   └── <expected file> ← EXPECTED content

3. Execute command
   └── command --options <input file> > <actual result file>

4. Compare results
   ├── Trim empty lines from start of both files
   ├── Trim empty lines from end of both files
   └── Compare trimmed content

5. Cleanup temporary files
```

## Examples

### Simple Command

```
// --COMMAND: dotnet run --project src/FunLang -- %s
// --INPUT
1 + 2
// --EXPECTED
3
```

Execution:
```bash
dotnet run --project src/FunLang -- /tmp/input.fun > /tmp/actual.txt
```

### Command with Options

```
// --COMMAND: dotnet run --project src/FunLang -- -d --show-tokens %s
// --INPUT
let x = 1
// --EXPECTED
=== LEXER TOKENS ===
...
```

### Piped Commands

Multiple commands can be piped together. Each `%s` is replaced with the same input file path.

```
// --COMMAND: cat %s | dotnet run --project src/FunLang -- -e "$(cat)"
// --INPUT
let x = 10
x * 2
// --EXPECTED
20
```

```
// --COMMAND: commandA --options %s | commandB --filter
// --INPUT
...
// --EXPECTED
...
```

Execution:
```bash
commandA --options <input file> | commandB --filter > <actual result file>
```

## Comparison Rules

When comparing expected and actual output:

1. **Trim leading empty lines**: Remove empty lines from the beginning of both files
2. **Trim trailing empty lines**: Remove empty lines from the end of both files
3. **Compare remaining content**: Exact match required for remaining lines

### Example

Expected file:
```

result: 42

```

Actual file:
```
result: 42
```

These are considered **equal** after trimming.

## Implementation Notes

### Temporary File Management

- Input file: `<temp dir>/test_<hash>_input.txt`
- Expected file: `<temp dir>/test_<hash>_expected.txt`
- Actual file: `<temp dir>/test_<hash>_actual.txt`
- All temporary files are cleaned up after test completion

### Error Handling

| Scenario | Result |
|----------|--------|
| Command exits with non-zero | Test fails with command error |
| Output doesn't match expected | Test fails with diff |
| Missing COMMAND section | Test skipped/error |
| Missing INPUT section | Empty input used |
| Missing EXPECTED section | Test fails |

### Shell Execution

Commands are executed via shell (`/bin/sh -c` on Unix, `cmd /c` on Windows) to support:
- Pipes (`|`)
- Subshells (`$(...)`)
- Redirections
- Environment variables

## Migration from Old Format

### Old Format (demos/*.fun) - Deprecated
```funlang
-- Expected: 42
let x = 40
x + 2
```

### New Format (tests/file-tests/*.test)
```
// --COMMAND: dotnet run --project src/FunLang -- %s
// --INPUT
let x = 40
x + 2
// --EXPECTED
42
```

## Directory Structure

```
tests/
└── file-tests/              # File-based test files
    ├── lex-tests/           # Lexer token output tests (--show-tokens)
    │   ├── 001-integer.test
    │   ├── 002-arithmetic.test
    │   └── ...
    ├── parse-tests/         # Parser AST output tests (--show-ast)
    │   ├── 001-literal.test
    │   ├── 002-addition.test
    │   └── ...
    ├── indent-tests/        # Indentation tests (internal only)
    │   └── README.md
    └── eval-tests/          # Evaluation result tests
        ├── 001-arithmetic.test
        ├── 002-boolean.test
        └── ...
```
