# FunLang Project File (funproj.toml)

Cargo 스타일 프로젝트 빌드 시스템. `funproj.toml`로 프로젝트 구성, 빌드/테스트 타겟을 관리한다.

---

## Quick Start

```toml
# funproj.toml
[project]
name = "myproject"
prelude = "Prelude"

[[executable]]
name = "myapp"
main = "src/main.fun"

[[test]]
name = "basic"
main = "tests/basic.fun"
```

```bash
fn build          # 모든 executable 타입 체크
fn test           # 모든 test 실행
```

---

## File Format

### [project] Section

프로젝트 메타데이터와 설정.

```toml
[project]
name = "myproject"
prelude = "lib/prelude"
```

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `name` | string | 선택 | 프로젝트 이름 |
| `prelude` | string | 선택 | Prelude 디렉토리 경로 (`funproj.toml` 기준 상대 경로) |

**Prelude 경로 우선순위:**

```
--prelude CLI 플래그  >  LANGTHREE_PRELUDE 환경 변수  >  funproj.toml prelude  >  자동 탐색
```

`prelude` 필드가 설정되면 해당 디렉토리의 `*.fun` 파일이 표준 라이브러리로 로드된다. 상대 경로는 `funproj.toml` 파일이 위치한 디렉토리 기준으로 해석된다.

### [[executable]] Section

빌드(타입 체크) 타겟 정의. 여러 개 선언 가능.

```toml
[[executable]]
name = "myapp"
main = "src/main.fun"

[[executable]]
name = "tool"
main = "src/tool.fun"
```

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `name` | string | 필수 | 타겟 이름 (`fn build <name>`으로 지정) |
| `main` | string | 필수 | 엔트리 포인트 파일 (`funproj.toml` 기준 상대 경로) |

### [[test]] Section

테스트 타겟 정의. 여러 개 선언 가능.

```toml
[[test]]
name = "unit"
main = "tests/unit.fun"

[[test]]
name = "integration"
main = "tests/integration.fun"
```

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `name` | string | 필수 | 테스트 이름 (`fn test <name>`으로 지정) |
| `main` | string | 필수 | 테스트 파일 (`funproj.toml` 기준 상대 경로) |

---

## CLI Commands

### fn build

```bash
fn build              # 모든 [[executable]] 타겟 타입 체크
fn build myapp        # 'myapp' 타겟만 타입 체크
```

**동작:**
1. CWD에서 `funproj.toml` 탐색
2. TOML 파싱, `[[executable]]` 타겟 추출
3. 각 타겟의 `main` 파일을 타입 체크 (실행하지 않음)
4. 결과 출력: `OK: myapp (0 warnings)` 또는 에러 메시지

**출력 예시:**

```
$ fn build
OK: myapp (0 warnings)
OK: tool (0 warnings)
```

타입 에러가 있는 경우:

```
$ fn build
Error in myapp: error[E0301]: Type mismatch: expected int but got string
```

**종료 코드:**
- `0`: 모든 타겟 성공
- `1`: 에러 발생 (`funproj.toml` 없음, 타겟 없음, 타입 에러)

### fn test

```bash
fn test               # 모든 [[test]] 타겟 실행
fn test unit          # 'unit' 테스트만 실행
```

**동작:**
1. CWD에서 `funproj.toml` 탐색
2. TOML 파싱, `[[test]]` 타겟 추출
3. 각 타겟의 `main` 파일을 **타입 체크 + 실행**
4. 결과 출력: `OK: unit (0 warnings)` 또는 에러 메시지

`build`와 달리 `test`는 파일을 실제로 실행한다. 테스트 파일에서 `println`이나 assertion을 사용하여 결과를 검증할 수 있다.

**종료 코드:**
- `0`: 모든 테스트 성공
- `1`: 에러 발생

---

## Path Resolution

모든 경로는 `funproj.toml` 파일이 위치한 디렉토리 기준 상대 경로로 해석된다.

```
myproject/
├── funproj.toml          # prelude = "lib/prelude"
├── lib/                  #   → /abs/path/myproject/lib/prelude
│   └── prelude/
│       └── MyLib.fun
├── src/
│   └── main.fun          # main = "src/main.fun"
└── tests/                #   → /abs/path/myproject/src/main.fun
    └── test.fun          # main = "tests/test.fun"
```

절대 경로도 사용 가능하지만, 이식성을 위해 상대 경로를 권장한다.

---

## Error Messages

| 상황 | 메시지 |
|------|--------|
| `funproj.toml` 없음 | `Error: funproj.toml not found in current directory` |
| TOML 파싱 실패 | `Failed to parse funproj.toml: ...` |
| 타겟 파일 없음 | `Error: target file not found: path/to/file.fun` |
| 존재하지 않는 타겟 이름 | `Error: no executable target named 'foo'` |
| 타겟 미정의 | `No executable targets defined in funproj.toml` |
| 타입 에러 | `Error in targetname: error[E0xxx]: ...` |

---

## Complete Example

### Project Structure

```
calculator/
├── funproj.toml
├── Prelude/
│   └── MathLib.fun
├── src/
│   └── calc.fun
└── tests/
    └── test-calc.fun
```

### funproj.toml

```toml
[project]
name = "calculator"
prelude = "Prelude"

[[executable]]
name = "calc"
main = "src/calc.fun"

[[test]]
name = "test-calc"
main = "tests/test-calc.fun"
```

### Prelude/MathLib.fun

```fsharp
module MathLib =
    let square x = x * x
    let cube x = x * x * x
```

### src/calc.fun

```fsharp
open MathLib
let result = square 5 + cube 2
let _ = printfn "result = %d" result
```

### tests/test-calc.fun

```fsharp
open MathLib

let assert_eq name expected actual =
    if expected = actual then
        printfn "PASS: %s" name
    else
        printfn "FAIL: %s (expected %d, got %d)" name expected actual

let _ = assert_eq "square 5" 25 (square 5)
let _ = assert_eq "cube 3" 27 (cube 3)
```

### 실행

```bash
$ cd calculator

$ fn build
OK: calc (0 warnings)

$ fn test
PASS: square 5
PASS: cube 3
OK: test-calc (0 warnings)

$ fn src/calc.fun
result = 33
```

---

## Custom Prelude

`prelude` 필드로 프로젝트별 커스텀 Prelude를 지정할 수 있다.

```toml
[project]
name = "myproject"
prelude = "../shared/prelude"
```

커스텀 Prelude 디렉토리에 `*.fun` 파일을 넣으면:
- 해당 디렉토리의 모든 `.fun` 파일이 자동 로드
- 생성자 의존성 기반 위상 정렬로 로드 순서 결정
- 기본 Prelude(`Core`, `List`, `Option` 등) 대신 사용

커스텀 Prelude 없이 기본 Prelude만 사용하려면 `prelude` 필드를 생략한다.

---

## Implementation Notes

- **TOML 파서:** Tomlyn 2.3.0 (TOML 1.1 준수)
- **POCO 매핑:** `[<CLIMutable>]` 어트리뷰트로 reflection 기반 자동 역직렬화
- **CLI 프레임워크:** Argu — `[<CliPrefix(CliPrefix.None)>]`로 `build`/`test` 서브커맨드 구현
- **Prelude 통합:** `config.PreludePath`가 `loadPrelude`에 전달되어 우선순위 체인에 참여

### Source Files

| 파일 | 역할 |
|------|------|
| `src/FunLang/ProjectFile.fs` | TOML 파싱, 경로 해석, `FunProjConfig` 생성 |
| `src/FunLang/Cli.fs` | CLI 인자 정의 (Argu) |
| `src/FunLang/Program.fs:114-210` | build/test 서브커맨드 실행 로직 |

### Test Coverage

| 테스트 | 위치 |
|--------|------|
| build 타겟 타입 체크 | `tests/flt/file/cli/cli-build.flt` |
| test 타겟 실행 | `tests/flt/file/cli/cli-test.flt` |
| Prelude 경로 설정 | `tests/flt/file/cli/cli-project-prelude.flt` |

---

*Source: `src/FunLang/ProjectFile.fs`, `src/FunLang/Cli.fs`, `src/FunLang/Program.fs`*
*Last updated: 2026-04-01*
