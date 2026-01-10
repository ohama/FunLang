# CLAUDE.md

This file provides guidance to Claude Code when working with this F# project.

## Project Overview

This is an F# project using .NET.

## Build & Run Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the project
dotnet run

# Run tests
dotnet test

# Watch mode (rebuild on file changes)
dotnet watch run

# Publish for production
dotnet publish -c Release
```

## Project Structure

```
├── src/                    # Source code
│   └── ProjectName/        # Main project
│       ├── Program.fs      # Entry point
│       └── *.fs            # F# source files
├── tests/                  # Test projects
│   └── ProjectName.Tests/
│       └── Tests.fs
├── *.fsproj                # F# project file
├── *.sln                   # Solution file
└── .config/                # .NET tool configuration
```

## F# Coding Conventions

### File Organization
- F# files compile in order listed in .fsproj - order matters
- Place types before functions that use them
- Module structure: types at top, then helper functions, then public API

### Naming Conventions
- **Types/Modules**: PascalCase (`type Customer`, `module Validation`)
- **Functions/Values**: camelCase (`let processOrder`, `let maxRetries`)
- **Parameters**: camelCase (`customerId`, `orderDate`)
- **Discriminated Union Cases**: PascalCase (`| Success | Failure`)

### Idiomatic F# Patterns
- Prefer immutability - use `let` over `let mutable`
- Use pattern matching over if/else chains
- Prefer discriminated unions for domain modeling
- Use `Result<'T, 'Error>` for error handling instead of exceptions
- Use `Option<'T>` for nullable values
- Prefer piping (`|>`) for data transformations
- Use computation expressions for async/result workflows

### Code Style
```fsharp
// Prefer this pattern matching style
let describe value =
    match value with
    | Some x -> sprintf "Has value: %A" x
    | None -> "No value"

// Use pipeline for transformations
let processItems items =
    items
    |> List.filter isValid
    |> List.map transform
    |> List.sortBy (fun x -> x.Priority)

// Prefer Result for error handling
let divide x y =
    if y = 0 then Error "Division by zero"
    else Ok (x / y)
```

## Testing

- Use Expecto, NUnit, or xUnit for testing
- Name tests descriptively: `"should return error when input is empty"`
- Group related tests in modules
- Use property-based testing with FsCheck for complex logic

## Dependencies

Common F# libraries:
- **FSharp.Core**: Core F# library (included)
- **Expecto**: Testing framework
- **FsCheck**: Property-based testing
- **Thoth.Json**: JSON serialization
- **FSharp.Data**: Type providers for data access
- **Giraffe/Saturn**: Web frameworks
- **Dapper.FSharp**: Database access

## Error Handling

- Use `Result<'T, 'Error>` for expected failures
- Use `Option<'T>` for missing values
- Reserve exceptions for truly exceptional cases
- Define domain-specific error types as discriminated unions

```fsharp
type ValidationError =
    | EmptyInput
    | InvalidFormat of string
    | OutOfRange of min: int * max: int

let validate input : Result<ValidatedInput, ValidationError> =
    // validation logic
```

## Common Tasks

### Adding a new file
1. Create the .fs file
2. Add it to the .fsproj in the correct order (files compile top-to-bottom)

### Adding a dependency
```bash
dotnet add package PackageName
```

### Creating a new project
```bash
dotnet new console -lang F# -o src/ProjectName
dotnet new xunit -lang F# -o tests/ProjectName.Tests
dotnet sln add src/ProjectName/ProjectName.fsproj
dotnet sln add tests/ProjectName.Tests/ProjectName.Tests.fsproj
```
