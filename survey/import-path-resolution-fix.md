# Import 경로 해석 버그 수정 계획

## 발견 경위

LangBackend `.planning/` Phase 40 (Multi-file Import)과 LangThree의 기존 import 구현을 비교한 결과,
**경로 해석 전략이 불일치**하는 것을 확인했다.

## 현재 문제

### 주석과 구현의 불일치 (TypeCheck.fs:724-731)

```fsharp
/// Resolve an import path relative to the importing file's directory.  ← 주석: 파일 기준
let resolveImportPath (importPath: string) (_importingFile: string) : string =  ← 파라미터 무시
    if Path.IsPathRooted importPath then
        importPath
    else
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), importPath))  ← 실제: CWD 기준
```

- 주석은 "importing file's directory"라고 기술
- `_importingFile` 파라미터를 받지만 언더스코어로 무시
- 실제 구현은 `Directory.GetCurrentDirectory()` (CWD) 사용

### CWD 기준 해석의 문제점

1. **실행 디렉토리 의존**: 같은 프로그램을 다른 디렉토리에서 실행하면 결과가 달라짐
2. **재귀 import 실패**: A→B→C 체인에서 B와 C가 다른 디렉토리에 있으면 깨짐
3. **LangBackend와 불일치**: COMP-04 요구사항은 importing 파일 기준 해석을 명시

### 재현 시나리오

```
project/
├── main.fun          ← open "lib/utils.fun"
├── lib/
│   ├── utils.fun     ← open "helpers.fun"   (같은 lib/ 디렉토리의 파일을 의도)
│   └── helpers.fun
```

- `cd project && langthree main.fun` → utils.fun 로드 성공
- utils.fun 안의 `open "helpers.fun"` → **CWD인 project/에서 찾으므로 실패**
- 의도대로라면 `lib/helpers.fun`을 찾아야 함

---

## Rust의 경로 해석 방식 (참고)

### 핵심 원칙

Rust는 `mod foo;` 선언 시 **항상 선언 파일의 위치 기준**으로 해석한다. CWD와 무관하다.

### 해석 알고리즘

`<parent>/<name>.rs`에서 `mod child;` 선언 시:

1. `<parent>/<name>/child.rs` 확인
2. `<parent>/<name>/child/mod.rs` 확인
3. 둘 다 있으면 컴파일 에러 (모호성 금지)
4. 둘 다 없으면 컴파일 에러

### Edition 변화 (2015 → 2018+)

| 항목 | 2015 | 2018+ |
|------|------|-------|
| 하위 모듈 디렉토리 | `mod.rs` 필수 | 파일명으로 대체 가능 |
| `network/` 모듈 진입점 | `network/mod.rs` | `network.rs` 또는 `network/mod.rs` |
| 동시 존재 | N/A | 에러 |

### `#[path]` 속성

기본 규칙 우회 시에도 선언 파일 기준 상대 경로:

```rust
#[path = "../shared/common.rs"]
mod common;  // 선언 파일 디렉토리 기준 상대 경로
```

### 순환 방지

Rust는 `mod` 선언이 트리 구조(부모→자식 방향만 허용)이므로 **구조적으로 순환이 불가능**하다.
FunLexYacc는 임의 파일을 open할 수 있으므로 순환 감지 로직이 별도로 필요하다 (이미 구현됨).

### FunLexYacc와 비교

| 측면 | Rust | FunLexYacc |
|------|------|------------|
| 파일 로딩 | `mod foo;` (트리 강제) | `open "file.fun"` (임의 파일) |
| 이름 가져오기 | `use` (선택적) | 자동 전체 공개 |
| 경로 기준 | 선언 파일 디렉토리 | CWD (현재) → 선언 파일 기준 (수정 예정) |
| 순환 방지 | 구조적 불가능 | fileLoadingStack으로 감지 |
| 가시성 | `pub`/비공개 세밀 제어 | 전체 공개 |

---

## 수정 계획

### 변경 대상

**파일**: `src/LangThree/TypeCheck.fs` (727-731행)

### Before

```fsharp
let resolveImportPath (importPath: string) (_importingFile: string) : string =
    if Path.IsPathRooted importPath then
        importPath
    else
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), importPath))
```

### After

```fsharp
let resolveImportPath (importPath: string) (importingFile: string) : string =
    if Path.IsPathRooted importPath then
        importPath
    else
        let dir = Path.GetDirectoryName(Path.GetFullPath(importingFile))
        Path.GetFullPath(Path.Combine(dir, importPath))
```

### 변경 사항

1. `_importingFile` → `importingFile` (언더스코어 제거, 실제 사용)
2. `Directory.GetCurrentDirectory()` → `Path.GetDirectoryName(Path.GetFullPath(importingFile))`

### 이미 갖춰진 인프라

수정이 1줄로 끝나는 이유 — 호출 체인에 필요한 정보가 이미 흐르고 있다:

- `Prelude.fs:100` — `TypeCheck.currentTypeCheckingFile <- resolvedPath` (재귀 로딩 시 현재 파일 갱신)
- `Prelude.fs:113` — `TypeCheck.currentTypeCheckingFile <- prevFile` (finally 블록에서 복원)
- `Program.fs:106` — `TypeCheck.currentTypeCheckingFile <- Path.GetFullPath filename` (진입점에서 설정)
- `TypeCheck.fs:971` — `resolveImportPath path currentTypeCheckingFile` (호출 시 현재 파일 전달)

### 테스트 계획

기존 테스트 (`tests/flt/file/import/`)가 모두 같은 디렉토리 내 import이므로 CWD와 파일 기준의 차이가 없어 통과할 것이다.

추가 테스트 필요:

```
tests/flt/file/import/
├── nested-import.flt         ← open "subdir/middle.fun" 후 middle이 open "inner.fun"
├── subdir/
│   ├── middle.fun            ← open "inner.fun" (같은 subdir/ 기준)
│   └── inner.fun             ← let innerVal = 42
```

`nested-import.flt` 기대 결과: `innerVal`이 42로 접근 가능

### LangBackend와의 정합성

이 수정 후 두 프로젝트의 경로 해석 동작이 일치한다:

| | LangThree (수정 후) | LangBackend COMP-04 |
|---|---|---|
| 절대 경로 | 그대로 사용 | 그대로 사용 |
| 상대 경로 | importing 파일 디렉토리 기준 | importing 파일 디렉토리 기준 |

---

*작성일: 2026-03-30*
*관련: LangBackend `.planning/REQUIREMENTS.md` COMP-04*
