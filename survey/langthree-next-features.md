# LangThree Next Features Survey

현재 LangThree는 v13.0까지 95 phase를 거쳐 성숙한 ML 계열 인터프리터가 되었다.
이 문서는 개발자 경험(DX) 관점에서 추가할 수 있는 모든 기능을 조사하고 우선순위를 매긴다.

**최종 업데이트:** 2026-04-01 (v13.0 Standard Library Extension 이후)

---

## 최근 완료된 기능 (이 survey 이후 구현됨)

| 기능 | 버전 | 상태 |
|------|------|------|
| 소스 코드 스니펫 (`^^^` 밑줄) | v11.0 | ✓ 완료 |
| "Did you mean?" 제안 | v11.0 | ✓ 완료 |
| 파서 에러 위치 + 토큰 정보 | v11.0 | ✓ 완료 |
| NoInstance에 가용 인스턴스 목록 | v11.0 | ✓ 완료 |
| 다중 에러 타입 시그니처 | v11.0 | ✓ 완료 |
| **Poison Type 다중 에러 보고** | **v11.1** | **✓ 완료** |
| 모듈 내 타입클래스/인스턴스 export | v10.1 | ✓ 완료 |
| ADT 인스턴스 지원 | v10.1 | ✓ 완료 |
| 모듈 에러 메시지 개선 | v10.2 | ✓ 완료 |
| 들여쓰기 블록 빈 줄 허용 | v10.3 | ✓ 완료 |
| `--emit-filtered-tokens` CLI | v10.3 | ✓ 완료 |
| **제약 조건부 인스턴스** (`Show 'a => Show ('a list)`) | **v12.0** | **✓ 완료** |
| **슈퍼클래스 제약** (`typeclass Eq 'a => Ord 'a`) | **v12.0** | **✓ 완료** |
| **자동 인스턴스 도출** (`deriving Show Color`) | **v12.0** | **✓ 완료** |
| **String.split/indexOf/replace/toUpper/toLower** | **v13.0** | **✓ 완료** |
| **List 17개 함수** (init/find/partition/groupBy/scan/sum...) | **v13.0** | **✓ 완료** |
| **다중 파라미터 람다** (`fun x y z -> body`) | **v13.0** | **✓ 완료** |

---

## 1. REPL 개선

현재 REPL은 최소한의 기능만 갖추고 있다 (한 줄 입력, 히스토리 없음, #quit만).

| 기능 | 설명 | Effort | Impact |
|------|------|--------|--------|
| **Readline/히스토리** | 위/아래 키 히스토리, 줄 편집. ReadLine NuGet 패키지 활용 | Low | High |
| **`:type` 명령** | `:type expr`로 타입만 출력. Bidir.synth 인프라 이미 존재 | Low | High |
| **`:load` 파일** | `:load path.l3`로 파일 로드, 환경에 병합 | Medium | High |
| **멀티라인 입력** | 미완성 표현식 자동 감지 (열린 괄호, 뒤따르는 `=`, `->`) | Medium | High |
| **영속적 바인딩** | `let x = 5` 다음 줄에서 `x` 사용 가능 (현재는 `let ... in` 필요) | Medium | High |
| **결과 타입 표시** | `val it : int = 42` 형식으로 타입+값 함께 표시 | Low | Medium |

### 구현 예시: `:type` 명령

```fsharp
// Repl.fs에서
| line when line.StartsWith(":type ") ->
    let expr = line.Substring(6)
    let ast = parse expr
    let _, ty = Bidir.synth ctorEnv recEnv [] env ast
    printfn "%s" (Type.formatTypeNormalized ty)
```

---

## 2. 언어 서버 (LSP)

IDE 지원의 핵심. LangThree는 이미 LSP에 필요한 대부분의 정보를 갖추고 있다.

### 이미 있는 것 (LSP 피드로 활용 가능)
- 모든 AST 노드에 Span (파일/줄/컬럼)
- Bidir.synth로 모든 서브표현식의 타입 추론
- TypeEnv/ConstructorEnv/RecordEnv로 스코프 내 모든 바인딩
- Diagnostic 시스템 (에러 코드, 스팬, 힌트, "Did you mean?", 소스 스니펫)
- **다중 에러 보고 (v11.1)** — 파일 전체의 에러를 한번에 LSP로 전달 가능

### 구현 단계

| 단계 | 기능 | Effort | 설명 |
|------|------|--------|------|
| 1 | **Diagnostics** | Low-Medium | 파일 저장 시 파싱+타입체크, 에러를 LSP diagnostic으로 변환 |
| 2 | **Hover** | Medium | 커서 위치의 AST 노드 찾기 → 타입 표시 |
| 3 | **Go-to-definition** | Medium | 변수 → 바인딩 위치, 생성자 → 타입 선언 위치 |
| 4 | **Autocomplete** | High | 불완전 입력 파싱, 스코프 기반 제안 목록 |

### 기술 선택
- **Ionide.LanguageServerProtocol** (F# 라이브러리): JSON-RPC 전송 + 프로토콜 타입 제공. Ionide 자체가 사용.
- 참고 구현: Gleam LSP (~1500줄), elm-language-server (~3000줄)

---

## 3. 코드 포매터

들여쓰기 기반 언어에서도 가능 — Fantomas(F#), Black(Python), elm-format이 선례.

| 접근법 | 장점 | 단점 |
|--------|------|------|
| **Opinionated** (Black/Ormolu 스타일) | 구현 간단, 논쟁 없음 | 유연성 없음 |
| **Configurable** (ocamlformat 스타일) | 팀별 맞춤 | 구현 복잡, 설정 논쟁 |

### 핵심 과제
- **주석 보존**: 현재 Lexer가 주석을 버림. 포매터는 주석을 AST에 붙여야 함
- **Pretty-printer 대수**: Wadler-Lindig 또는 PPrint 스타일
- Effort: **High** (~2-3주)

---

## 4. 새로운 타입: Float

현재 숫자 타입은 `int`만 존재. 과학 계산, 그래픽, 게임 등에서 필수적.

### 구현 범위

```
type Type = ... | TFloat                // 새 타입 추가
type Value = ... | FloatValue of float  // 새 값 추가
```

| 항목 | 설명 | Effort |
|------|------|--------|
| Lexer: float 리터럴 | `3.14`, `1.0e-5`, `.5` | Low |
| Parser: float 파싱 | `FLOAT` 토큰 | Low |
| Type.fs: `TFloat` | `int`와 동일한 구조 (`TError` 패턴 참고) | Low |
| Eval.fs: `FloatValue` | 산술 연산자 float 지원 | Low |
| Bidir.fs: 타입 추론 | float 리터럴 → `TFloat`, 연산자 오버로딩 | Medium |
| Prelude/Math.fun | `sqrt`, `sin`, `cos`, `exp`, `log`, `pi` 등 | Low |
| **총 Effort** | | **Medium** (~3-4일) |

### 연산자 오버로딩 문제
- `+`가 `int`와 `float` 모두에 동작해야 함
- 방법 1: `Num` 타입 클래스 (Haskell 스타일) — 깔끔하지만 대규모
- 방법 2: 별도 연산자 (`+.`, `-.`, `*.`, `/.`) — OCaml 스타일, 간단
- 방법 3: 자동 승격 (int → float) — 실용적이지만 타입 순수성 저하
- **추천: 방법 2** (OCaml 스타일) — 최소 변경, 명확한 의미

---

## 5. 문자열 보간 (String Interpolation)

모든 현대 언어가 갖춘 필수 기능.

**현재:**
```
let msg = sprintf "Hello %s, you are %d years old" name age
```

**개선 후:**
```
let msg = $"Hello {name}, you are {age} years old"
```

### 구현 방법
- Lexer에서 `$"..."` 안의 `{...}`를 감지, 토큰 스트림으로 분할
- Parser에서 `StringInterp(parts)` AST 노드로 조합
- 디슈거: `string_concat` + `to_string` 호출 체인으로 변환
- Effort: **Medium** (~3일, Lexer 컨텍스트 모드 전환이 가장 복잡)

---

## 6. 타입 클래스 확장 — ✓ v12.0에서 전부 완료

### 6.1 제약 조건부 인스턴스 (Constrained Instances)

```
instance Show 'a => Show (list 'a) =
    let show xs = "[" + String.concat "; " (map show xs) + "]"
```

현재 `show [1; 2; 3]`은 E0701 에러 (인스턴스 목록이 표시됨 — v11.0). 이 기능이 있으면 자동으로 동작.

- Effort: **High** — 인스턴스 해결 시 재귀적 제약 체크 필요
- 전제: 유니피케이션 기반 인스턴스 매칭 (현재는 직접 비교)

### 6.2 슈퍼클래스 제약

```
typeclass Eq 'a => Ord 'a =
    | compare : 'a -> 'a -> int
```

`Ord`를 사용하면 자동으로 `Eq`도 사용 가능.

- Effort: **Medium** — ClassInfo에 superclass 목록 추가, 인스턴스 해결 시 전이적 탐색

### 6.3 자동 인스턴스 도출 (Derive)

```
type Color = | Red | Green | Blue
    deriving (Show, Eq)
```

- Effort: **High** — ADT 구조에서 Show/Eq 함수 자동 생성
- 전제: Constrained instances (리스트/튜플 내부 요소에 대해)

---

## 7. 디버깅 도구

### 7.1 `dbg` 내장 함수

Rust의 `dbg!()` 매크로에 해당. 가장 빠르게 구현 가능한 디버깅 도구.

```
let x = dbg (expensive_computation 42)
// 출력: [file.l3:1] expensive_computation 42 = 1764
// x = 1764 (값은 그대로 반환)
```

- Effort: **Low** (~1일)
- 구현: Eval.fs에서 `dbg` 내장 함수가 인자를 평가하고, 현재 Span + 값을 stderr에 출력, 값을 반환

### 7.2 프로파일링

```
$ langthree --profile program.l3
Function          Calls    Total (ms)
fibonacci         177      12.3
map               50       2.1
fold              25       1.0
```

- Effort: **Low-Medium** (~2일)
- 구현: Eval.fs의 App 처리에서 함수 이름+시간 축적, 종료 시 보고

### 7.3 스텝 디버거

트리-워킹 인터프리터이므로 컴파일 언어보다 디버거 구현이 훨씬 쉬움.

- Eval 호출 전에 브레이크포인트 체크
- 일시 정지 시 디버그 REPL 진입 (로컬 환경 검사, 타입 확인, step/continue)
- Effort: **Medium-High** (~1주)

### 7.4 에러 시 콜 스택

```
Error: Division by zero
  at divide (math.l3:5:14)
  at process (main.l3:12:8)
  at main (main.l3:20:0)
```

- Effort: **Medium** — Eval에서 함수 호출 스택 유지, 에러 시 출력

---

## 8. 표준 라이브러리 확장 — 부분 완료 (v13.0: String + List)

### 현재 부족한 핵심 함수

| 모듈 | 부족한 함수 | 사용 빈도 | Effort |
|------|------------|----------|--------|
| **String** | `split`, `indexOf`, `replace`, `toUpper`, `toLower` | **매우 높음** | Low |
| **List** | `init`, `find`, `findIndex`, `groupBy`, `partition`, `scan` | 높음 | Low |
| **Math** | `sqrt`, `sin`, `cos`, `exp`, `log`, `pi`, `pow` | 높음 (float 전제) | Low |
| **Map** (불변) | 전체 (현재 mutable Hashtable만) | 높음 | Medium |
| **Set** (불변) | 전체 (현재 mutable HashSet만) | 중간 | Medium |
| **Seq** (지연) | `map`, `filter`, `take`, `fold` 등 | 중간 | High |
| **Regex** | `match`, `replace`, `split` | 중간 | Medium |
| **Array** | `zip`, `unzip`, `find`, `findIndex` | 중간 | Low |

### 가장 시급한 것: `String.split`

텍스트 처리의 80%에 필요. 현재 없어서 수동 재귀 필요.

```fsharp
// Eval.fs에 추가
"string_split", Scheme([0], [], TArrow(TString, TArrow(TString, TList TString)))
// string_split "a,b,c" "," → ["a"; "b"; "c"]
```

---

## 9. 구문 개선 (Syntax)

### 9.1 As-패턴

```
match expr with
| (Some x) as opt -> use opt and x
```

현재 불가능. 패턴 전체를 변수에 바인딩하려면 두 번 매칭해야 함.
- Effort: **Medium** — AST에 `AsPat` 추가, 패턴 매칭 컴파일 수정

### 9.2 연산자 섹션 (Operator Sections)

```
map (+1) [1; 2; 3]     // 현재: map (fun x -> x + 1) [1; 2; 3]
filter (> 0) [-1; 0; 1] // 현재: filter (fun x -> x > 0) [-1; 0; 1]
```

- Effort: **Medium** — Parser에서 `(op expr)` / `(expr op)`를 Lambda로 디슈거

### 9.3 숫자 리터럴 확장

```
let hex = 0xFF
let bin = 0b1010
let big = 1_000_000
```

- Effort: **Low** — Lexer 수정만으로 가능

### 9.4 레코드 필드 펀닝 (Field Punning)

```
let x = 1
let y = 2
let point = { x; y }       // { x = x; y = y } 대신
```

- Effort: **Low** — Parser에서 `{ x }` → `{ x = x }` 디슈거

### 9.5 backward 파이프 (`<|`)

```
println <| to_string <| 1 + 2    // println (to_string (1 + 2))
```

현재 `|>` (forward)만 존재.
- Effort: **Low** — Parser에 `<|` 토큰 + AST 노드 추가

---

## 10. 빌드 시스템 / 패키지 관리

### 현재: `funproj.toml`
- `[project]` name, prelude
- `[[executable]]` name, main
- `[[test]]` name, main

### 부족한 것

| 기능 | 설명 | Effort |
|------|------|--------|
| `version` 필드 | 프로젝트 버전 관리 | Low |
| `[[library]]` 타겟 | 모듈만 노출 (main 없음) | Low |
| `[dependencies]` | 로컬 경로 의존성 (`path = "../lib"`) | Medium |
| `funproj.lock` | 재현 가능한 빌드 | Medium |
| 증분 빌드 | mtime/hash 기반 캐싱 | High |
| 패키지 레지스트리 | 중앙 저장소 | Very High |

---

## 11. 테스트 프레임워크

### 내장 테스트 러너

```
test "addition works" =
    assert (1 + 1 = 2)

test "list map" =
    assert (map (fun x -> x * 2) [1; 2; 3] = [2; 4; 6])
```

`funproj.toml`의 `[[test]]` 타겟과 연동.
- Effort: **Medium** — `test` 키워드 + `assert` 내장 함수 + 결과 보고

### 프로퍼티 기반 테스트

```
property "reverse reverse is identity" =
    forall (fun xs -> reverse [] (reverse [] xs) = xs)
```

- Effort: **High** — 랜덤 값 생성기, shrinking

---

## 12. 문서 생성

### 최소 기능

```
/// 두 정수를 더합니다
let add x y = x + y
```

`///` 주석 → `--emit-doc` 플래그 → Markdown/HTML 출력

| 단계 | 설명 | Effort |
|------|------|--------|
| Lexer: `///` 보존 | 주석을 AST에 연결 | Low |
| 타입 시그니처 추출 | `--emit-type` 이미 존재 | Low |
| Markdown 생성 | 모듈별 함수 목록 + 주석 + 타입 | Medium |
| HTML 생성 | 템플릿 기반 | Medium |
| 타입 기반 검색 | "list 'a → int 타입의 함수 찾기" | High |

---

## 우선순위 매트릭스

### Tier 1: Quick Wins (1-3일, 높은 효과)

| # | 기능 | Effort | Impact | LOC |
|---|------|--------|--------|-----|
| 1 | REPL readline/히스토리 | Low | High | ~50 |
| 2 | REPL `:type` 명령 | Low | High | ~20 |
| 3 | `dbg` 내장 함수 | Low | High | ~30 |
| ~~4~~ | ~~`String.split` + 추가 문자열 함수~~ | ~~Low~~ | ~~High~~ | **✓ v13.0에서 완료** |
| 5 | 숫자 리터럴 확장 (hex, bin, underscore) | Low | Medium | ~30 |
| 6 | 레코드 필드 펀닝 | Low | Medium | ~20 |
| 7 | backward 파이프 `<|` | Low | Medium | ~15 |

### Tier 2: Medium Investments (1-2주)

| # | 기능 | Effort | Impact | LOC |
|---|------|--------|--------|-----|
| 8 | Float 타입 + Math 모듈 | Medium | High | ~200 |
| 9 | 문자열 보간 $"..." | Medium | High | ~150 |
| 10 | LSP (diagnostics + hover) | Medium | High | ~300 |
| 11 | 프로파일링 (--profile) | Medium | Medium | ~80 |
| 12 | As-패턴 | Medium | Medium | ~100 |
| 13 | 연산자 섹션 | Medium | Medium | ~60 |
| ~~14~~ | ~~슈퍼클래스 제약~~ | ~~Medium~~ | ~~Medium~~ | **✓ v12.0에서 완료** |
| ~~15~~ | ~~List.init/find/groupBy/partition~~ | ~~Low~~ | ~~High~~ | **✓ v13.0에서 완료** |
| 16 | 불변 Map 모듈 | Medium | High | ~150 |

### Tier 3: Major Features (2-4주)

| # | 기능 | Effort | Impact | LOC |
|---|------|--------|--------|-----|
| ~~17~~ | ~~제약 조건부 인스턴스~~ | ~~High~~ | ~~High~~ | **✓ v12.0에서 완료** |
| 18 | Computation expressions / do 표기법 | High | High | ~300 |
| 19 | 코드 포매터 | High | Medium | ~500+ |
| 20 | 스텝 디버거 | High | Medium | ~300 |
| ~~21~~ | ~~다중 에러 (Poison Type)~~ | ~~High~~ | ~~Medium~~ | **✓ v11.1에서 완료** |
| 22 | 내장 테스트 프레임워크 | Medium | Medium | ~150 |
| 23 | 문서 생성 (///→Markdown) | Medium | Medium | ~200 |

### Tier 4: Long-term (1개월+)

| # | 기능 | Effort | Impact |
|---|------|--------|--------|
| 24 | Full LSP (autocomplete, refactoring) | Very High | High |
| 25 | Higher-kinded types (Functor, Monad) | Very High | High |
| 26 | 증분 빌드 | High | Medium |
| 27 | Seq/lazy sequences | High | Medium |
| 28 | 패키지 레지스트리 | Very High | High |
| 29 | Effect system | Very High | Medium |
| 30 | JIT/바이트코드 컴파일 | Very High | Medium |

---

## 추천 로드맵

### 다음 마일스톤 후보

**Option A: 개발자 도구 집중**
- REPL 개선 (readline, :type, :load, 영속 바인딩)
- `dbg` 함수 + 프로파일링
- LSP (diagnostics + hover)

**Option B: 언어 기능 집중**
- Float 타입 + Math 모듈
- 문자열 보간
- String.split + List 확장
- 레코드 펀닝, 숫자 리터럴, `<|`

**Option C: 타입 시스템 집중**
- 제약 조건부 인스턴스
- 슈퍼클래스 제약
- 자동 인스턴스 도출 (derive)

**Option D: 균형 잡힌 접근** (추천)
- Tier 1 Quick Wins 전부 (#1-7)
- Float 타입 (#8)
- 문자열 보간 (#9)
- LSP diagnostics (#10)
- List/String 확장 (#15)
