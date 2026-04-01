# FunLang 프로젝트 파일 기반 빌드 시스템 설계 조사

**Date:** 2026-03-31
**Baseline:** FunLang v8.1, LangBackend v9.0, FunLexYacc
**Purpose:** 멀티파일 프로젝트를 체계적으로 빌드하는 시스템의 설계 방안 심층 조사

---

## 1. 현재 상태 분석

### 1.1 FunLang의 파일 로딩 메커니즘

FunLang는 현재 두 가지 파일 로딩 경로를 가짐:

**경로 A: Prelude 자동 로딩 (`Prelude.fs`)**
- `Prelude/` 디렉토리의 `.fun` 파일을 자동으로 로드
- 생성자 기반 의존성 분석 → 토폴로지 정렬 → 순서대로 로드
- 각 파일을 `module <Stem> = ...` 블록으로 래핑 후 `open <Stem>` 삽입
- `PreludeResult`로 환경(타입/값/생성자/레코드/모듈) 축적

**경로 B: `open "file.fun"` 파일 임포트**
- `FileImportDecl` AST 노드로 표현
- 경로는 임포트하는 파일의 디렉토리 기준 상대 경로로 해석
- `HashSet<string>` 기반 순환 감지 (단일 스레드)
- 재귀적 로드: 임포트된 파일 내의 `open "..."` 도 처리
- 결과 환경이 현재 스코프에 병합

**현재의 한계:**
1. **진입점 하나만 지정 가능** — CLI가 `langthree myfile.fun` 으로 단일 파일만 받음
2. **암시적 의존성** — `open "file.fun"` 체인을 따라가야만 전체 의존성 파악 가능
3. **빌드 순서 제어 불가** — Prelude는 자동 토폴로지 정렬이지만, 사용자 파일은 `open` 순서에 의존
4. **컴파일 단위 부재** — 증분 빌드(incremental build) 불가능, 매번 전체 로드

### 1.2 FunLexYacc의 실제 빌드 패턴

FunLexYacc는 이미 `open "file.fun"` 체인으로 멀티파일 프로젝트를 구성:

```
FunlexMain.fun (진입점)
  ├── open "../common/ErrorInfo.fun"
  ├── open "../common/Diagnostics.fun"
  ├── open "LexSyntax.fun"
  ├── open "LexParser.fun"
  │     ├── open "../common/ErrorInfo.fun"  (중복, 순환 감지로 안전)
  │     └── open "LexSyntax.fun"            (중복)
  ├── open "Nfa.fun"
  │     ├── open "../common/ErrorInfo.fun"
  │     ├── open "../common/Cset.fun"
  │     └── open "LexSyntax.fun"
  ├── open "Dfa.fun"
  │     ├── open "../common/ErrorInfo.fun"
  │     ├── open "../common/Cset.fun"
  │     └── open "Nfa.fun"               (재귀 로드)
  ├── open "DfaMin.fun"
  │     ├── open "../common/ErrorInfo.fun"
  │     ├── open "../common/Cset.fun"
  │     ├── open "LexSyntax.fun"
  │     ├── open "Nfa.fun"
  │     └── open "Dfa.fun"
  └── open "LexEmit.fun"
        ├── open "LexSyntax.fun"
        └── open "DfaMin.fun"
```

**LangBackend(컴파일러)의 빌드 방식:**
- `LBC src/funlex/FunlexMain.fun -o build/funlex` — 진입점 하나만 지정
- LangBackend가 내부적으로 `open "..."` 체인을 추적하여 모든 파일을 컴파일
- 이미 "진입점 기반 자동 의존성 해결" 패턴이 동작 중

### 1.3 핵심 질문

현재 `open "file.fun"` 체인이 이미 동작하므로, "프로젝트 파일 기반 빌드"가 필요한 이유는:

1. **명시적 의존성 선언** — `open` 체인을 따라가지 않고도 프로젝트 구조 파악
2. **빌드 설정 중앙 관리** — 출력 경로, 컴파일 옵션, Prelude 경로 등
3. **증분 빌드** — 변경된 파일만 재컴파일 (LangBackend에서 중요)
4. **멀티 타겟** — 하나의 프로젝트에서 funlex와 funyacc를 각각 빌드
5. **외부 도구 통합** — IDE, LSP, CI/CD가 프로젝트 구조를 이해

---

## 2. 선행 사례 분석

### 2.1 F# (.fsproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Parser.fs" />
    <Compile Include="Eval.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```

**특징:**
- 파일 순서가 컴파일 순서 (명시적 토폴로지 순서)
- XML 기반, MSBuild 통합
- 패키지 참조 (`<PackageReference>`)
- 다중 타겟 프레임워크 지원

**FunLang에의 시사점:**
- 파일 순서 = 컴파일 순서 는 간단하지만 유지보수 부담
- XML은 FunLang 프로젝트에 과도 — 더 간단한 포맷이 적합

### 2.2 OCaml (dune)

```lisp
(executable
 (name main)
 (libraries str)
 (modules ErrorInfo Cset LexSyntax Nfa Dfa DfaMin LexParser LexEmit FunlexMain))
```

**특징:**
- S-expression 기반 선언적 빌드 파일 (`dune`)
- `ocamldep`이 자동으로 의존성 분석
- 모듈 이름 = 파일 이름 (대문자 시작)
- 파일 순서 자동 결정 (의존성 기반)

**FunLang에의 시사점:**
- FunLang의 Prelude 로더가 이미 `ocamldep` 방식의 생성자 기반 의존성 분석을 사용
- 모듈 이름 = 파일 이름 관례를 사용자 코드에도 확장 가능

### 2.3 Haskell (cabal / package.yaml)

```yaml
# package.yaml (hpack)
name: funlexyacc
executables:
  funlex:
    main: FunlexMain.fun
    source-dirs: src/funlex
    dependencies:
      - common
  funyacc:
    main: FunyaccMain.fun
    source-dirs: src/funyacc
    dependencies:
      - common

internal-libraries:
  common:
    source-dirs: src/common
    exposed-modules:
      - ErrorInfo
      - Cset
      - Diagnostics
      - Symtab
```

**특징:**
- YAML 기반 선언적 빌드 파일
- 내부 라이브러리로 코드 공유
- `main` 필드로 진입점 지정
- 의존성은 라이브러리 단위

**FunLang에의 시사점:**
- YAML/TOML은 파싱이 쉽고 사람이 읽기 좋음
- 내부 라이브러리 개념으로 common 모듈을 재사용

### 2.4 Rust (Cargo.toml)

```toml
[package]
name = "funlexyacc"
version = "1.0.0"

[[bin]]
name = "funlex"
path = "src/funlex/main.fun"

[[bin]]
name = "funyacc"
path = "src/funyacc/main.fun"
```

**특징:**
- TOML 기반
- `mod` 선언으로 모듈 구조 (파일 시스템 = 모듈 트리)
- 다중 바이너리 타겟
- 의존성 자동 해결 (파일 시스템 기반)

**FunLang에의 시사점:**
- TOML은 간결하고 타입이 명확
- 다중 바이너리 타겟이 FunLexYacc에 직접 적용 가능

### 2.5 Go (go.mod)

```
module funlexyacc

go 1.21
```

**특징:**
- 극도로 간단 — 모듈 이름만 선언
- 디렉토리 구조가 곧 패키지 구조
- 의존성은 import문에서 자동 감지
- 빌드 순서 자동 결정

**FunLang에의 시사점:**
- FunLang의 `open "file.fun"`이 이미 Go의 import문과 유사
- 최소한의 프로젝트 파일 + 파일 시스템 기반 모듈 해석이 가장 자연스러움

---

## 3. 설계 방안

### 3.1 방안 A: `l3proj.toml` 프로젝트 파일 (Cargo 스타일)

```toml
[project]
name = "funlexyacc"
version = "1.0.0"
prelude = "../FunLang/Prelude"    # Prelude 경로 (기본값: ./Prelude)

[[executable]]
name = "funlex"
main = "src/funlex/FunlexMain.fun"

[[executable]]
name = "funyacc"
main = "src/funyacc/FunyaccMain.fun"

[[test]]
name = "test-common"
main = "tests/common/test_cset.fun"

[[test]]
name = "test-lexparser"
main = "tests/funlex/test_lexparser_basic.fun"
```

**빌드 명령:**
```bash
langthree build                    # 모든 executable 빌드 (인터프리터에서는 type-check)
langthree build funlex             # 특정 타겟만
langthree run funlex -- input.funl -o output.fun
langthree test                     # 모든 테스트 실행
langthree test test-common         # 특정 테스트만
```

**의존성 해결:**
- 각 `main` 파일에서 `open "file.fun"` 체인을 재귀적으로 추적 (기존 메커니즘)
- 프로젝트 파일에 파일 목록을 열거할 필요 없음 (Go 스타일)
- 순환 감지는 기존 `HashSet<string>` 그대로 사용

**장점:**
- TOML은 파싱이 쉽고 F#에서 라이브러리 사용 가능 (Tomlyn NuGet)
- 다중 타겟 자연스럽게 지원
- Prelude 경로 설정 가능 (FunLexYacc의 `make setup` 대체)
- 기존 `open "file.fun"` 메커니즘과 100% 호환

**단점:**
- TOML 파서 의존성 추가
- 프로젝트 파일 포맷 설계/유지보수 부담

**구현 난이도:** MEDIUM

### 3.2 방안 B: `l3proj.fun` FunLang 자체 포맷

```fsharp
// l3proj.fun — FunLang 프로젝트 파일
// 주석과 키-값 쌍의 단순 포맷

project "funlexyacc"
version "1.0.0"
prelude "../FunLang/Prelude"

executable "funlex" "src/funlex/FunlexMain.fun"
executable "funyacc" "src/funyacc/FunyaccMain.fun"

test "test-common" "tests/common/test_cset.fun"
test "test-lexparser" "tests/funlex/test_lexparser_basic.fun"
```

**장점:**
- 외부 파서 의존성 없음 — FunLang 자체 파서로 처리 가능
- 문법이 단순 (키워드 + 문자열 리터럴)

**단점:**
- 사용자가 또 다른 문법을 학습해야 함
- 복잡한 설정 (조건부 빌드 등)에 대한 확장성 부족

**구현 난이도:** LOW

### 3.3 방안 C: 진입점 자동 추적 (Go 스타일 최소주의)

프로젝트 파일 없이, CLI에 여러 진입점을 지정:

```bash
# 기존 (단일 파일)
langthree src/funlex/FunlexMain.fun

# 확장: 여러 파일
langthree --check src/funlex/FunlexMain.fun src/funyacc/FunyaccMain.fun

# 디렉토리 기반: 특정 패턴의 파일을 진입점으로
langthree --check src/**/*Main.fun
```

**의존성 해결:**
- 각 진입점에서 `open "file.fun"` 체인을 추적
- 공유 파일 (ErrorInfo.fun 등)은 한 번만 로드 (캐싱)
- 프로젝트 파일 불필요

**장점:**
- 가장 단순 — 프로젝트 파일 포맷 설계 불필요
- 기존 CLI에 `--check` 플래그 하나만 추가
- Makefile이나 쉘 스크립트로 빌드 자동화 가능 (현재 FunLexYacc 방식 유지)

**단점:**
- 빌드 설정 (Prelude 경로, 출력 경로 등) 관리 불가
- IDE/LSP 통합 어려움 (프로젝트 구조를 파악할 단일 파일이 없음)
- 증분 빌드 상태 저장 위치 불명

**구현 난이도:** LOW

### 3.4 방안 D: `open` 체인 기반 자동 의존성 + 캐싱 (권장)

기존 `open "file.fun"` 메커니즘을 강화하여, 프로젝트 파일 없이도 빌드 시스템의 핵심 기능을 제공:

```bash
# 현재: 단일 파일 실행
langthree src/funlex/FunlexMain.fun

# 확장 1: 타입 체크만 (모든 임포트 파일 포함)
langthree --check src/funlex/FunlexMain.fun

# 확장 2: 의존성 트리 출력
langthree --deps src/funlex/FunlexMain.fun
# Output:
#   src/funlex/FunlexMain.fun
#     src/common/ErrorInfo.fun
#     src/common/Diagnostics.fun
#       src/common/ErrorInfo.fun (cached)
#     src/funlex/LexSyntax.fun
#     src/funlex/LexParser.fun
#       src/funlex/LexSyntax.fun (cached)
#     ...

# 확장 3: 파일 임포트 캐싱
langthree --cache-dir .langthree-cache src/funlex/FunlexMain.fun
```

**핵심 아이디어:**
- 프로젝트 파일 대신, 진입점 파일이 곧 프로젝트 정의
- `open "file.fun"` 체인이 의존성 그래프
- 이미 로드된 파일의 환경을 캐시하여 중복 로드 방지
- LangBackend가 이미 이 방식으로 동작 중

**Prelude 경로 해결:**
```bash
# 환경 변수
export LANGTHREE_PRELUDE=/path/to/Prelude

# 또는 CLI 옵션
langthree --prelude ../FunLang/Prelude src/funlex/FunlexMain.fun

# 또는 심볼릭 링크 (현재 FunLexYacc 방식)
ln -sf ../LangBackend/Prelude Prelude
```

**선택적 l3proj.toml:**
- 방안 A의 프로젝트 파일을 나중에 추가 가능 (backwards compatible)
- 없으면 CLI 인자로 모든 것을 제어
- 있으면 프로젝트 파일에서 설정 읽기

**장점:**
- 최소 변경으로 최대 효과 (기존 메커니즘 위에 구축)
- LangBackend와 일관된 빌드 모델
- 프로젝트 파일 없이도 동작, 있으면 편리
- 점진적 도입 가능

**단점:**
- 진입점에 모든 `open`이 있어야 함 (이미 FunLexYacc가 이 패턴)

**구현 난이도:** LOW-MEDIUM

---

## 4. 구현 세부 설계 (방안 D 기준)

### 4.1 파일 임포트 캐싱

현재 `loadAndTypeCheckFileImpl`은 매번 파일을 읽고 파싱하고 타입 체크함. 동일 파일이 여러 진입점에서 임포트되면 중복 처리.

**개선: 파일 경로 → 환경 캐시**

```fsharp
// Prelude.fs에 캐시 추가
let fileCache = Dictionary<string, TypeCheckResult * EvalResult>()

let loadAndTypeCheckFileImpl resolvedPath ... =
    match fileCache.TryGetValue(resolvedPath) with
    | true, (tcResult, _) -> tcResult   // 캐시 히트
    | false, _ ->
        // 기존 로직: 파일 읽기 → 파싱 → 타입 체크
        let result = ...
        fileCache.[resolvedPath] <- (result, evalResult)
        result
```

**주의:** 캐싱은 동일 프로세스 내에서만 유효. 파일 변경 감지가 필요하면 mtime 비교.

### 4.2 `--deps` 플래그 구현

```fsharp
// Program.fs
| Deps ->
    let file = args.GetResult File
    let deps = collectDependencies file
    deps |> List.iter (fun (path, depth) ->
        let indent = String.replicate (depth * 2) " "
        printfn "%s%s" indent path)
```

`collectDependencies`는 파일을 파싱하여 `FileImportDecl`을 추출하고, 재귀적으로 의존성 트리를 구성:

```fsharp
let rec collectDependencies (filePath: string) (visited: Set<string>) (depth: int) =
    let absPath = Path.GetFullPath(filePath)
    if Set.contains absPath visited then
        [(absPath, depth, true)]  // cached marker
    else
        let source = File.ReadAllText(absPath)
        let ast = parse source
        let imports = ast |> extractFileImports
        let childDeps =
            imports
            |> List.collect (fun importPath ->
                let resolved = resolveImportPath importPath absPath
                collectDependencies resolved (Set.add absPath visited) (depth + 1))
        (absPath, depth, false) :: childDeps
```

### 4.3 `--check` 플래그 구현

```fsharp
// Program.fs
| Check ->
    let file = args.GetResult File
    let prelude = loadPrelude()
    TypeCheck.currentTypeCheckingFile <- Path.GetFullPath(file)
    let ast = parseFile file
    match typeCheckModuleWithPrelude prelude ast with
    | Ok warnings ->
        warnings |> List.iter (fun w -> eprintfn "%s" (formatDiagnostic w))
        printfn "Type check passed (%d warnings)" (List.length warnings)
        0
    | Error err ->
        eprintfn "%s" (formatDiagnostic (typeErrorToDiagnostic err))
        1
```

### 4.4 `--prelude` 플래그 구현

```fsharp
// Cli.fs
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check
    | Deps
    | Prelude of path: string   // 추가
    | [<MainCommand; Last>] File of filename: string
```

```fsharp
// Prelude.fs — Prelude 경로 해결 순서 변경
let resolvePreludeDir (explicitPath: string option) =
    match explicitPath with
    | Some path -> path   // CLI --prelude 플래그
    | None ->
        match Environment.GetEnvironmentVariable("LANGTHREE_PRELUDE") with
        | null | "" ->
            // 기존 3-stage discovery
            findPreludeDir()
        | envPath -> envPath
```

### 4.5 선택적 `l3proj.toml` 지원 (Phase 2)

이후에 추가 가능:

```fsharp
// Program.fs — 시작 시 l3proj.toml 검색
let projectConfig =
    if File.Exists("l3proj.toml") then
        Some (parseToml (File.ReadAllText("l3proj.toml")))
    else
        None

// projectConfig에서 prelude 경로 등 읽기
let preludePath =
    match projectConfig with
    | Some config -> config.TryGet("project.prelude")
    | None -> None
```

### 4.6 LangBackend 통합

LangBackend가 동일한 모델을 사용하도록:

```bash
# LangBackend 현재 방식 (이미 동작)
LBC src/funlex/FunlexMain.fun -o build/funlex

# LangBackend 확장 (선택적 프로젝트 파일)
LBC --project l3proj.toml --target funlex -o build/funlex

# l3proj.toml이 있으면 자동 감지
cd funlexyacc/
LBC build funlex    # l3proj.toml에서 main과 prelude 경로 읽기
```

---

## 5. 증분 빌드 설계

### 5.1 파일 수준 캐싱 (단기)

```
.langthree-cache/
├── manifest.json          # 파일 경로 → (mtime, hash, 결과 참조)
├── src_common_ErrorInfo.tc    # TypeCheck 결과 직렬화
├── src_common_Cset.tc
└── ...
```

**manifest.json:**
```json
{
  "files": {
    "/abs/path/src/common/ErrorInfo.fun": {
      "mtime": "2026-03-31T10:00:00Z",
      "hash": "sha256:abc123...",
      "tc_cache": "src_common_ErrorInfo.tc"
    }
  }
}
```

**캐시 무효화 전략:**
1. mtime 비교 (빠름, 대부분 충분)
2. mtime 변경 시 해시 비교 (정확도)
3. 의존하는 파일이 변경되면 연쇄 무효화

### 5.2 모듈 수준 증분 빌드 (장기)

각 `.fun` 파일을 독립된 컴파일 단위로 처리:

```
                    ErrorInfo.fun
                        ↓
    ┌───────────────────┼───────────────────┐
    ↓                   ↓                   ↓
Cset.fun         Diagnostics.fun      Symtab.fun
    ↓                   ↓
LexSyntax.fun           ↓
    ↓                   ↓
LexParser.fun     FunlexMain.fun
    ↓                 ↑
Nfa.fun ──────────────┘
    ↓
Dfa.fun
    ↓
DfaMin.fun
    ↓
LexEmit.fun
```

각 노드에서:
1. **인터페이스 추출** — 파일의 public 바인딩 시그니처 (.l3i 파일)
2. **인터페이스 비교** — 시그니처가 변경되지 않았으면 의존자 재컴파일 불필요
3. **바디 컴파일** — 인터페이스 변경 시에만 의존자 재컴파일

이는 OCaml의 `.cmi`/`.cmo` 분리와 동일한 원리.

---

## 6. 구현 로드맵

### Phase 1: 캐싱 + CLI 확장 (v8.2 또는 v9.0)

- `--check` 플래그: 타입 체크만 (실행 없음)
- `--deps` 플래그: 의존성 트리 출력
- `--prelude` 플래그: Prelude 경로 명시
- `LANGTHREE_PRELUDE` 환경 변수
- 파일 임포트 캐싱 (동일 프로세스 내)

**변경 파일:** Cli.fs, Program.fs, Prelude.fs
**난이도:** LOW
**예상:** 1-2 phases

### Phase 2: 선택적 프로젝트 파일 (v9.x)

- `l3proj.toml` 파싱 (Tomlyn NuGet 또는 자체 TOML 파서)
- `langthree build`, `langthree test` 서브커맨드
- 다중 타겟 (executable, test)
- Prelude/출력 경로 설정

**변경 파일:** Cli.fs, Program.fs, 새로운 ProjectFile.fs
**난이도:** MEDIUM
**예상:** 2-3 phases

### Phase 3: 증분 빌드 (v10.x)

- 파일 수준 캐싱 (mtime + hash)
- `.langthree-cache/` 디렉토리
- 연쇄 무효화
- 인터페이스 추출 (장기)

**변경 파일:** Prelude.fs, 새로운 Cache.fs
**난이도:** HIGH
**예상:** 3-4 phases

---

## 7. 방안 비교 요약

| 기준 | A: l3proj.toml | B: l3proj.fun | C: CLI만 | D: open 강화 (권장) |
|------|---------------|---------------|---------|-------------------|
| 구현 난이도 | MEDIUM | LOW | LOW | LOW-MEDIUM |
| 기존 호환성 | 100% | 100% | 100% | 100% |
| 다중 타겟 | ✓ | ✓ | △ (스크립트) | △ → ✓ (Phase 2) |
| IDE 통합 | ✓ (단일 파일) | △ | ✗ | △ → ✓ (Phase 2) |
| 증분 빌드 | ✓ (확장 가능) | △ | ✗ | ✓ (Phase 3) |
| LangBackend 통합 | ✓ | △ | ✓ | ✓ |
| 학습 비용 | TOML 문법 | 자체 문법 | 없음 | CLI 플래그만 |
| FunLexYacc 적용 | 즉시 가능 | 즉시 가능 | 즉시 가능 | 즉시 가능 |

**권장: 방안 D** — Phase 1에서 CLI 확장으로 즉각적 가치를 제공하고, Phase 2에서 방안 A의 프로젝트 파일을 선택적으로 추가. FunLexYacc의 현재 Makefile 기반 빌드와 완전히 호환되면서도, 점진적으로 빌드 시스템을 강화할 수 있음.

---

## 8. FunLexYacc 적용 시나리오

### 현재 (Makefile)
```makefile
funlex: $(BUILD_DIR)
	$(LBC) src/funlex/FunlexMain.fun -o $(BUILD_DIR)/funlex
```

### Phase 1 후 (CLI 확장)
```bash
# 타입 체크만 (빠른 검증)
langthree --check --prelude ../FunLang/Prelude src/funlex/FunlexMain.fun

# 의존성 확인
langthree --deps src/funlex/FunlexMain.fun

# 실행
langthree --prelude ../FunLang/Prelude src/funlex/FunlexMain.fun -- input.funl -o output.fun
```

### Phase 2 후 (프로젝트 파일)
```toml
# l3proj.toml
[project]
name = "funlexyacc"
prelude = "../FunLang/Prelude"

[[executable]]
name = "funlex"
main = "src/funlex/FunlexMain.fun"

[[executable]]
name = "funyacc"
main = "src/funyacc/FunyaccMain.fun"

[[test]]
name = "test-cset"
main = "tests/common/test_cset.fun"
```

```bash
langthree build              # funlex + funyacc 빌드 (type-check)
langthree build funlex       # funlex만
langthree test               # 모든 테스트
langthree run funlex -- input.funl -o output.fun
```

---

*Generated: 2026-03-31 — FunLang v8.1 + LangBackend v9.0 + FunLexYacc 분석 기준*
