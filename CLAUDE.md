# LangThree

## Build & Test

```bash
# Build
dotnet build src/LangThree/LangThree.fsproj -c Release

# F# unit tests
dotnet test tests/LangThree.Tests/LangThree.Tests.fsproj -c Release

# flt integration tests (uses external FsLit runner)
../fslit/dist/FsLit tests/flt/              # run all
../fslit/dist/FsLit tests/flt/file/array/   # run a subdirectory
../fslit/dist/FsLit tests/flt/file/array/array-basic.flt  # run a single file
../fslit/dist/FsLit -v tests/flt/           # verbose (show diff on failure)
```
