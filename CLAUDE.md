# FunLang

## Build & Test

```bash
# Build
dotnet build src/FunLang/FunLang.fsproj -c Release

# F# unit tests
dotnet test tests/FunLang.Tests/FunLang.Tests.fsproj -c Release

# flt integration tests (fslit submodule, auto-builds on first run)
scripts/fslit tests/flt/              # run all
scripts/fslit tests/flt/file/array/   # run a subdirectory
scripts/fslit -v tests/flt/           # verbose (show diff on failure)
```
