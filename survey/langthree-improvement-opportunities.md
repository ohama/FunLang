# FunLang 개선 기회 분석

**Date:** 2026-03-31
**Baseline:** FunLang v8.1 (66 phases, 14,122 LOC F#, 652 flt + 224 unit tests)
**Purpose:** 현재 언어에서 개선 가능한 영역을 체계적으로 분류하고, 우선순위와 난이도를 평가

---

## 개요

FunLang v8.1은 ML 스타일 함수형 언어로서 핵심 기능(ADT/GADT, Records, Modules, 예외, 패턴 매칭 컴파일, TCO, 가변 데이터, 루프, 네이티브 컬렉션, 타입 어노테이션, 상호 재귀)을 갖춤. 이하 개선 기회를 4개 카테고리로 분류:

1. **타입 시스템 확장** — 표현력과 안전성 강화
2. **문법/구문 개선** — 개발자 편의성과 F# 호환성
3. **런타임/인터프리터** — 성능과 실용성
4. **표준 라이브러리/에코시스템** — 실용 프로그래밍 지원

각 항목은 **영향도** (언어 사용자에 대한 가치), **난이도** (구현 복잡도), **의존성** (선행 작업)으로 평가.

---

## 1. 타입 시스템 확장

### 1.1 타입 클래스 / 인터페이스

**현재:** 다형적 함수는 parametric polymorphism만 지원. `to_string`은 하드코딩된 builtin.
**개선:** Haskell 스타일 타입 클래스 또는 F# 스타일 인터페이스로 ad-hoc polymorphism 지원.

```fsharp
// 예시: Show 타입 클래스
typeclass Show 'a =
    show : 'a -> string

instance Show int =
    let show x = to_string x

instance Show (Option 'a) where Show 'a =
    let show x = match x with None -> "None" | Some v -> "Some(" ^^ show v ^^ ")"
```

- **영향도:** HIGH — to_string, 비교 연산자, 수학 연산의 다형성 문제 근본 해결
- **난이도:** HIGH — 딕셔너리 전달 방식 구현, 인스턴스 해결 알고리즘, 타입 추론과의 상호작용
- **의존성:** 없음 (독립적이나 기존 타입 추론과의 통합이 핵심)
- **참고:** Haskell 타입 클래스 vs F# SRTP vs Rust traits — 어떤 모델을 채택할지 설계 결정 필요

### 1.2 레코드 필드명 스코핑

**현재:** 레코드 필드명이 전역적으로 유일해야 함. 두 레코드가 같은 이름의 필드를 가지면 충돌.
**개선:** 모듈 스코프 또는 타입 어노테이션으로 필드 해결.

```fsharp
// 현재: 충돌!
type Point = { x: int; y: int }
type Size = { x: int; y: int }      // 에러: x, y 이미 정의됨

// 개선: 타입 어노테이션으로 구별
let p : Point = { x = 1; y = 2 }
let s : Size = { x = 10; y = 20 }
```

- **영향도:** MEDIUM — `langthree-constraints.md` §2.4에서 FunLexYacc가 모든 필드에 접두사 필요
- **난이도:** MEDIUM — 타입 추론 중 후보 레코드 타입 목록에서 disambiguation
- **의존성:** 없음

### 1.3 Higher-Kinded Types / Computation Expressions

**현재:** 모나딕 코드는 수동 `bind`/`map` 호출 필요.
**개선:** F# 스타일 computation expressions 또는 `do` 표기법.

```fsharp
// 현재
let result =
    optionBind (safeDivide 10 2) (fun x ->
        optionBind (safeDivide x 3) (fun y ->
            Some (x + y)))

// 개선: computation expression
let result = option {
    let! x = safeDivide 10 2
    let! y = safeDivide x 3
    return x + y
}
```

- **영향도:** MEDIUM — Option/Result 체인 코드가 크게 간결해짐
- **난이도:** HIGH — 빌더 패턴, `Bind`/`Return`/`Zero` 메서드 해결, 파서 확장
- **의존성:** 1.1 (타입 클래스가 있으면 빌더를 인스턴스로 정의 가능) 또는 독립 구현 가능

### 1.4 `do` 바인딩

**현재:** `let _ = expr`으로 부수효과 표현. 타입 체커가 반환값이 unit인지 검증 안 함.
**개선:** `do expr`로 unit 반환 강제.

```fsharp
// 현재
let _ = println "hello"     // 반환값이 int여도 경고 없음

// 개선
do println "hello"          // 반환값이 unit이 아니면 경고
```

- **영향도:** LOW-MEDIUM — 코드 의도 명시, 실수 방지
- **난이도:** LOW — 파서에 `do` 키워드 추가, Bidir.fs에서 unit 타입 체크
- **의존성:** 없음

### 1.5 Seq 타입 (Lazy Sequences)

**현재:** 모든 컬렉션이 eager. `[1..1000000]`은 즉시 전체 리스트 생성.
**개선:** lazy sequence 타입으로 지연 평가.

```fsharp
let naturals = seq { 0.. }
let first10 = Seq.take 10 naturals
```

- **영향도:** MEDIUM — 대용량 데이터 처리, 무한 시퀀스
- **난이도:** HIGH — lazy evaluation thunk, 새로운 Value 타입, 이터레이터 프로토콜
- **의존성:** 없음 (독립적이나 computation expression과 시너지)

---

## 2. 문법/구문 개선

### 2.1 `and` 키워드의 IndentFilter 처리

**현재:** Expression-level `let rec ... and ...`에서 `and`를 별도 줄에 작성하면 IndentFilter가 오작동.
**개선:** IndentFilter에 `and` 키워드 특수 처리 추가.

```fsharp
// 현재: 한 줄에 작성해야 함
let rec even n = if n = 0 then true else odd (n - 1) and odd n = if n = 0 then false else even (n - 1) in even 10

// 개선: 여러 줄로 분리 가능
let rec even n =
    if n = 0 then true
    else odd (n - 1)
and odd n =
    if n = 0 then false
    else even (n - 1)
in even 10
```

- **영향도:** HIGH — 상호 재귀 코드의 가독성이 극적으로 개선됨
- **난이도:** MEDIUM — IndentFilter에 `InLetRecDecl` 컨텍스트 추가, `and` 키워드를 offside column에 정렬
- **의존성:** v8.1 완료 (LetRec bindings list 이미 구현)

### 2.2 `match` 함수 축약 (`function` 키워드)

**현재:** 패턴 매칭 함수에 항상 `fun x -> match x with` 패턴 필요.
**개선:** F#/OCaml의 `function` 키워드.

```fsharp
// 현재
let describe = fun x -> match x with
    | 0 -> "zero"
    | _ -> "other"

// 개선
let describe = function
    | 0 -> "zero"
    | _ -> "other"
```

- **영향도:** MEDIUM — 패턴 매칭 함수의 관용적 표현
- **난이도:** LOW — 파서에 `function` → `fun __x -> match __x with` 디슈거링
- **의존성:** 없음

### 2.3 Guard 패턴의 `when` 완전성 참여

**현재:** `when` 가드가 있는 패턴은 완전성 검사에서 제외됨.
**개선:** 간단한 `when` 가드 (상수 비교)는 완전성 검사에 참여.

- **영향도:** LOW — 불필요한 W0001 경고 감소
- **난이도:** HIGH — exhaustiveness checker에 가드 분석 추가 (일반적으로 undecidable)
- **의존성:** 없음

### 2.4 문자열 보간 (String Interpolation)

**현재:** `sprintf "%d items" count` 또는 `to_string count ^^ " items"`.
**개선:** F# 스타일 `$"..."` 문자열 보간.

```fsharp
// 현재
let msg = sprintf "Found %d items in %s" count name

// 개선
let msg = $"Found {count} items in {name}"
```

- **영향도:** MEDIUM — 문자열 조합 코드가 간결해짐
- **난이도:** MEDIUM — 렉서에서 `$"..."` 파싱, `{expr}` 내부를 별도 토큰으로 분리, 디슈거
- **의존성:** 없음

### 2.5 Active Patterns

**현재:** 패턴은 리터럴, 생성자, 변수만 가능.
**개선:** F# 스타일 active patterns으로 커스텀 패턴 디컴포지션.

```fsharp
let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd

let describe n =
    match n with
    | Even -> "even"
    | Odd -> "odd"
```

- **영향도:** MEDIUM — 패턴 매칭의 표현력 확대
- **난이도:** HIGH — 패턴 컴파일러에 active pattern 런타임 호출 삽입
- **의존성:** 없음

### 2.6 `sprintf` 포맷 강화 (패딩, 16진수)

**현재:** `%d`, `%s`, `%b`, `%%`만 지원. 패딩(`%8s`), 16진수(`%02x`) 미지원.
**개선:** C printf 호환 포맷 지원.

```fsharp
let hex = sprintf "%02x" 255       // "ff"
let padded = sprintf "%8s" "hello"  // "   hello"
```

- **영향도:** HIGH — FunLexYacc 컴파일 블로커 (`funlexyacc-gap-status-v9.md` §2 블로커 1)
- **난이도:** MEDIUM — Eval.fs의 printf 핸들러 확장
- **의존성:** 없음

---

## 3. 런타임/인터프리터 개선

### 3.1 에러 메시지 위치 정보 개선

**현재:** 타입 에러에 위치(span) 정보가 있지만, 런타임 에러 (match failure, index out of bounds 등)는 위치가 불완전.
**개선:** 모든 에러에 정확한 소스 위치(파일명:줄:열) 포함.

- **영향도:** HIGH — 디버깅 효율 대폭 향상
- **난이도:** MEDIUM — Eval.fs에서 span 전파, 예외에 span 첨부
- **의존성:** 없음

### 3.2 REPL 개선

**현재:** 기본 REPL. 히스토리, 자동완성, 멀티라인 편집 미지원.
**개선:** readline 라이브러리 활용, 멀티라인 입력, 타입 표시.

```
> let f x = x + 1
val f : int -> int

> f 42
val it : int = 43
```

- **영향도:** MEDIUM — 인터랙티브 개발 경험 향상
- **난이도:** MEDIUM — readline 바인딩, 타입 추론 결과 표시, `:type` 명령
- **의존성:** 없음

### 3.3 꼬리 호출 최적화 범위 확장

**현재:** 직접 자기 재귀만 TCO. 상호 재귀 TCO 미지원.
**개선:** 상호 재귀 함수 간 꼬리 호출도 스택 안전하게.

```fsharp
// 현재: 큰 n에서 stack overflow
let rec even n = if n = 0 then true else odd (n - 1)
and odd n = if n = 0 then false else even (n - 1)
```

- **영향도:** MEDIUM — 상호 재귀가 실용적으로 사용 가능
- **난이도:** MEDIUM — Eval.fs trampoline을 모든 상호 재귀 바인딩으로 확장
- **의존성:** v8.1 완료 (expression-level mutual rec)

### 3.4 바이트코드 컴파일러 / JIT

**현재:** AST 직접 해석(tree-walking interpreter). 대규모 프로그램에서 느림.
**개선:** 바이트코드 VM 또는 .NET Expression Tree 기반 JIT.

- **영향도:** HIGH — 10-100x 성능 향상 가능
- **난이도:** VERY HIGH — 완전히 새로운 컴파일러 백엔드
- **의존성:** 없음 (독립적이나 LangBackend AOT 컴파일러가 별도 존재)

### 3.5 멀티파일 빌드 시스템

**현재:** `open "file.fun"`으로 단일 파일 임포트. 프로젝트 전체 빌드 시스템 없음.
**개선:** `open` 체인 기반 자동 의존성 해결 강화 + 선택적 프로젝트 파일 (`l3proj.toml`).

- **영향도:** HIGH — FunLexYacc 컴파일 블로커 (`funlexyacc-gap-status-v9.md` §2 블로커 2)
- **난이도:** MEDIUM — Phase 1 (CLI 확장) LOW, Phase 2 (프로젝트 파일) MEDIUM, Phase 3 (증분 빌드) HIGH
- **의존성:** 없음
- **상세 설계:** [`survey/project-build-system-design.md`](project-build-system-design.md) 참조 — 4개 방안 비교, 구현 세부 설계, 증분 빌드 전략, FunLexYacc 적용 시나리오 포함

---

## 4. 표준 라이브러리 / 에코시스템

### 4.1 Map / Set 모듈

**현재:** Hashtable (mutable)만 있음. 불변 맵/셋 없음.
**개선:** 함수형 스타일의 불변 Map, Set 추가.

```fsharp
let m = Map.ofList [("a", 1); ("b", 2)]
let m2 = Map.add "c" 3 m
let v = Map.find "a" m    // 1
```

- **영향도:** MEDIUM — 함수형 프로그래밍에서 불변 맵은 핵심 자료구조
- **난이도:** HIGH — balanced BST 구현 (red-black tree 또는 AVL), Value 타입 추가
- **의존성:** 없음 (Prelude .fun 파일만으로는 성능이 나쁨 — 네이티브 구현 필요)

### 4.2 정규식 (Regex)

**현재:** `string_contains`만 지원. 패턴 매칭 검색 불가.
**개선:** 정규식 매칭/치환 내장 함수.

```fsharp
let matched = Regex.isMatch "[0-9]+" "abc123"    // true
let groups = Regex.match "([a-z]+)([0-9]+)" "abc123"
```

- **영향도:** LOW-MEDIUM — 텍스트 처리 프로그램에 유용
- **난이도:** HIGH — .NET Regex 래핑 또는 자체 구현
- **의존성:** 없음

### 4.3 JSON/데이터 파싱

**현재:** 구조화된 데이터 포맷 처리 방법 없음.
**개선:** JSON 파서 내장 또는 라이브러리.

- **영향도:** MEDIUM — 실용 프로그래밍에서 데이터 교환 필수
- **난이도:** MEDIUM — Prelude .fun으로 구현 가능 (파서 컴비네이터)
- **의존성:** 없음

### 4.4 Prelude 확장: String 모듈

**현재:** String 모듈에 6개 함수만. `split`, `replace`, `toUpper`, `toLower`, `indexOf` 등 미지원.
**개선:** 문자열 조작 함수 확장.

```fsharp
let parts = String.split "," "a,b,c"        // ["a"; "b"; "c"]
let upper = String.toUpper "hello"           // "HELLO"
let idx = String.indexOf "world" "hello world" // 6
```

- **영향도:** MEDIUM — 문자열 처리 코드 간소화
- **난이도:** LOW — 각 함수를 Eval.fs builtin으로 추가
- **의존성:** 없음

### 4.5 Prelude 확장: List 모듈

**현재:** 28개 함수. `groupBy`, `collect`, `scan`, `unfold`, `partition`, `pairwise` 등 미지원.
**개선:** F# List 모듈 수준의 함수 커버리지.

- **영향도:** MEDIUM — 복잡한 리스트 처리 패턴 지원
- **난이도:** LOW — 대부분 .fun 파일로 구현 가능
- **의존성:** 없음

---

## 우선순위 매트릭스

### Tier 1: 높은 영향 + 낮은 난이도 (Quick Wins)

| # | 항목 | 영향도 | 난이도 | 비고 |
|---|------|--------|--------|------|
| 2.1 | `and` IndentFilter 처리 | HIGH | MEDIUM | v8.1 cosmetic 제한 해결 |
| 2.6 | sprintf 포맷 강화 | HIGH | MEDIUM | FunLexYacc 블로커 |
| 1.4 | `do` 바인딩 | MEDIUM | LOW | 파서 + 타입 체크 소폭 변경 |
| 2.2 | `function` 키워드 | MEDIUM | LOW | 파서 디슈거링만 |
| 4.4 | String 모듈 확장 | MEDIUM | LOW | builtin 추가 |
| 4.5 | List 모듈 확장 | MEDIUM | LOW | .fun 파일 추가 |

### Tier 2: 높은 영향 + 중간 난이도 (Strategic)

| # | 항목 | 영향도 | 난이도 | 비고 |
|---|------|--------|--------|------|
| 3.1 | 에러 메시지 위치 정보 | HIGH | MEDIUM | 디버깅 경험 |
| 3.5 | 멀티파일 빌드 | HIGH | MEDIUM | FunLexYacc 블로커 |
| 3.3 | 상호 재귀 TCO | MEDIUM | MEDIUM | v8.1 위에 구축 |
| 1.2 | 레코드 필드명 스코핑 | MEDIUM | MEDIUM | FunLexYacc constraint |
| 2.4 | 문자열 보간 | MEDIUM | MEDIUM | 개발자 편의성 |

### Tier 3: 높은 영향 + 높은 난이도 (Major Investment)

| # | 항목 | 영향도 | 난이도 | 비고 |
|---|------|--------|--------|------|
| 1.1 | 타입 클래스 | HIGH | HIGH | 언어 표현력 근본 개선 |
| 1.3 | Computation expressions | MEDIUM | HIGH | 모나딕 코드 간소화 |
| 3.4 | 바이트코드/JIT | HIGH | VERY HIGH | 성능 10-100x |
| 4.1 | Map/Set 모듈 | MEDIUM | HIGH | 네이티브 구현 필요 |
| 1.5 | Seq 타입 | MEDIUM | HIGH | lazy evaluation |

### Tier 4: FunLexYacc 컴파일 블로커

FunLexYacc를 LangBackend로 컴파일하기 위해 반드시 필요한 항목:

| # | 항목 | FunLang | LangBackend | 상태 |
|---|------|-----------|-------------|------|
| 2.6 | sprintf 패딩/hex | 미지원 | 미지원 | Tier 1 |
| 3.5 | 멀티파일 빌드 | `open "file.fun"` | 미지원 | Tier 2 |
| — | `get_args ()` | 지원 | 미지원 | LangBackend만 |

---

## 제안 로드맵

### v8.2 — Quick Wins + FunLexYacc 블로커

1. `and` IndentFilter 처리 (2.1)
2. sprintf 포맷 강화: `%02x`, `%8s`, `%3d` (2.6)
3. `function` 키워드 (2.2)
4. `do` 바인딩 (1.4)
5. String/List 모듈 확장 (4.4, 4.5)

### v9.0 — Practical Programming II

1. 에러 메시지 위치 정보 개선 (3.1)
2. 레코드 필드명 스코핑 (1.2)
3. 문자열 보간 (2.4)
4. 상호 재귀 TCO (3.3)
5. 멀티파일 빌드 시스템 (3.5)

### v10.0 — Type System Evolution

1. 타입 클래스 / 인터페이스 (1.1)
2. Computation expressions (1.3)
3. Map/Set 모듈 (4.1)
4. Seq 타입 (1.5)

---

*Generated: 2026-03-31 — FunLang v8.1 (66 phases, 142 plans) 기준*
