# Publish for Release

Build and publish the project for production deployment.

## Arguments

- `$ARGUMENTS` - Optional: target runtime (e.g., linux-x64, win-x64, osx-x64)

## Steps

1. Run `dotnet publish -c Release` to create release build
2. If runtime specified, use `dotnet publish -c Release -r <runtime> --self-contained`
3. Report publish location and output files
