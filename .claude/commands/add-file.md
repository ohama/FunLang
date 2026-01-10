# Add F# Source File

Add a new F# source file to the project. F# file order matters for compilation.

## Arguments

- `$ARGUMENTS` - Name of the file to create (without .fs extension)

## Steps

1. Create the new .fs file with a module declaration
2. Add the file to the .fsproj in the correct compilation order
3. Ensure the file is placed after any files it depends on
