# Add NuGet Package

Add a NuGet package dependency to the project.

## Arguments

- `$ARGUMENTS` - Name of the NuGet package to add (optionally with version)

## Steps

1. Run `dotnet add package <package-name>` to add the dependency
2. If version specified, use `dotnet add package <package-name> --version <version>`
3. Run `dotnet restore` to ensure package is installed
4. Report installation status
