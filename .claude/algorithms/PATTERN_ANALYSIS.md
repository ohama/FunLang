# Pattern Matching Analysis (Maranget Algorithm)

> 이 문서는 Phase 9: Pattern Matching Improvements의 상세 알고리즘 설명입니다.
> 전체 구현 계획은 [PLAN.md](../PLAN.md)를 참조하세요.

## 개요

현재 FunLang의 pattern matching은 단순 순차 매칭 방식으로 구현되어 있음.
이를 함수형 언어 컴파일러 수준으로 개선하기 위한 알고리즘.

## 참고 논문

- [Compiling Pattern Matching to Good Decision Trees](http://moscova.inria.fr/~maranget/papers/ml05e-maranget.pdf) - Luc Maranget (2008) - **핵심 참고**
- [The Implementation of Functional Programming Languages, Ch.5](https://homepages.inf.ed.ac.uk/wadler/papers/pattern/pattern.pdf) - Philip Wadler (1987)
- [Warnings for Pattern Matching](http://moscova.inria.fr/~maranget/papers/warn/warn.pdf) - Maranget

---

## Phase 9.1: Exhaustiveness Check (완전성 검사)

**목표:** 누락된 패턴 경고 - "Non-exhaustive pattern match" 컴파일 타임 감지

**핵심 알고리즘:** Maranget의 Usefulness 알고리즘

```
패턴 행렬 P에 대해 벡터 v가 "useful" 하다는 것은
P의 어떤 행에도 매칭되지 않는 값이 v 형태로 존재한다는 의미
```

### 패턴 행렬 표현

```fsharp
type PatternMatrix = Pattern list list  // 각 행 = match case의 패턴
type PatternVector = Pattern list       // 테스트할 패턴 벡터
```

### Usefulness 함수

```fsharp
/// 패턴 벡터 q가 행렬 P에 대해 useful한지 검사
/// useful = P의 모든 행과 매칭되지 않는 값이 존재
let rec isUseful (matrix: PatternMatrix) (vector: PatternVector) : bool =
    match matrix, vector with
    | [], _ -> true  // 빈 행렬 = 어떤 값도 매칭 안됨 → useful
    | _, [] -> false // 빈 벡터 = 매칭할 것 없음
    | _, PWildcard :: _ ->
        // 와일드카드: 모든 생성자에 대해 specialization
        ...
    | _, (PConstructor (name, _)) :: _ ->
        // 생성자: 해당 생성자로 specialization
        ...
```

### Specialization 연산

```fsharp
/// 생성자 c에 대해 행렬을 특수화
/// - c와 매칭되는 행만 유지
/// - 생성자 인자를 컬럼으로 펼침
let specialize (ctor: string) (matrix: PatternMatrix) : PatternMatrix = ...

/// Default 연산: 해당 컬럼의 생성자가 아닌 행 유지
let default' (matrix: PatternMatrix) : PatternMatrix = ...
```

### Exhaustiveness 검사

```fsharp
/// 패턴 매칭이 완전한지 검사
let checkExhaustive (patterns: Pattern list) (scrutineeType: Type) : Pattern list option =
    let matrix = patterns |> List.map (fun p -> [p])
    let missingPattern = findMissingPattern matrix scrutineeType
    missingPattern
```

### 예시

```funlang
type Option 'a = None | Some of 'a

// 경고: Non-exhaustive pattern match
// Missing case: Some _
match x with
| None -> 0
```

---

## Phase 9.2: Redundancy Check (중복 패턴 검사)

**목표:** 도달 불가능한 패턴 경고 - "Redundant pattern" 컴파일 타임 감지

**핵심 아이디어:**
```
패턴 Pi가 redundant ⟺
  이전 패턴 P1...P(i-1)이 Pi가 매칭하는 모든 값을 이미 매칭함
```

### 구현

```fsharp
/// 패턴이 이전 패턴들에 의해 완전히 커버되는지 검사
let isRedundant (previousPatterns: Pattern list) (pattern: Pattern) : bool =
    // pattern이 useful하지 않으면 redundant
    let matrix = previousPatterns |> List.map (fun p -> [p])
    not (isUseful matrix [pattern])
```

### 예시

```funlang
match x with
| 0 -> "zero"
| 1 -> "one"
| 0 -> "zero again"  // 경고: Redundant pattern (never matched)
| _ -> "other"
```

---

## Phase 9.3: Decision Tree Compilation (선택적 최적화)

**목표:** 효율적인 매칭 코드 생성 - 각 값을 최대 1번만 테스트

### 두 가지 접근법

| 접근법 | 장점 | 단점 |
|--------|------|------|
| **Decision Tree** | 각 값 최대 1번 테스트 | 코드 크기 증가 가능 |
| **Backtracking Automata** | 선형 코드 크기 | 같은 값 여러 번 테스트 가능 |

### Decision Tree 접근법 (Maranget 알고리즘)

#### 핵심 데이터 구조

```fsharp
/// 컴파일된 매칭 트리
type DecisionTree =
    | Fail                              // 매칭 실패
    | Leaf of action: int               // 성공 (action = case 번호)
    | Switch of                         // 값 테스트
        occurrence: Occurrence *        // 테스트할 위치
        cases: (Constructor * DecisionTree) list *
        default: DecisionTree option

/// Occurrence: 값에 접근하는 경로
type Occurrence = int list  // [0; 1] = 첫번째 인자의 두번째 필드
```

#### 컴파일 알고리즘

```fsharp
/// 패턴 행렬을 decision tree로 컴파일
let rec compile (matrix: PatternMatrix) (actions: int list) (occs: Occurrence list) : DecisionTree =
    if List.isEmpty matrix then
        Fail
    elif isAllWildcard (List.head matrix) then
        Leaf (List.head actions)
    else
        // 테스트할 컬럼 선택 (heuristic)
        let col = selectColumn matrix
        let ctors = getConstructors matrix col
        let cases =
            ctors |> List.map (fun c ->
                let matrix' = specialize c col matrix
                let actions' = specializeActions c col actions matrix
                c, compile matrix' actions' (expandOcc col c occs))
        let default =
            let matrix' = default' col matrix
            if List.isEmpty matrix' then None
            else Some (compile matrix' ...)
        Switch (List.item col occs, cases, default)
```

#### Heuristic (컬럼 선택)

- **First column**: 단순하지만 비효율적일 수 있음
- **Most constructors**: 가장 많은 생성자가 있는 컬럼 선택
- **Necessity**: 모든 행에서 필요한 컬럼 우선
- **Left-to-right**: ML/Haskell 호환성

**현재 FunLang에서는 Phase 9.1, 9.2만 구현 권장**
- Decision Tree는 인터프리터에서 큰 이점 없음
- 컴파일러 백엔드 추가 시 Phase 9.3 구현

---

## Phase 9.4: Guard 지원 개선

**목표:** Guard가 있는 패턴에서도 정확한 분석

**문제:**
```funlang
match x with
| n when n > 0 -> "positive"  // Guard는 정적 분석 불가
| n when n < 0 -> "negative"
| 0 -> "zero"
// 경고해야 하나? n = 0일 때 첫 두 guard가 false라면?
```

**해결책:**
- Guard가 있으면 보수적으로 분석 (경고 억제 가능)
- Guard를 opaque로 처리 (패턴 자체만 분석)
- 사용자에게 Guard 경고 옵션 제공

---

## PatternAnalysis.fs

```fsharp
module FunLang.PatternAnalysis

open FunLang.Ast
open FunLang.Types

// =============================================================================
// Pattern Matrix Representation
// =============================================================================

type PatternRow = LPattern list
type PatternMatrix = PatternRow list
type PatternVector = LPattern list

// =============================================================================
// Constructor Signature
// =============================================================================

/// 타입의 모든 생성자 목록 조회
let getConstructors (typeName: string) (typeEnv: TypeDefEnv) : string list = ...

/// 생성자의 인자 수
let constructorArity (ctorName: string) (typeEnv: TypeDefEnv) : int = ...

// =============================================================================
// Matrix Operations
// =============================================================================

/// Specialize: 생성자 c에 대해 행렬 특수화
let specialize (ctor: string) (col: int) (matrix: PatternMatrix) : PatternMatrix = ...

/// Default: 해당 컬럼에 명시적 생성자가 없는 행 유지
let defaultMatrix (col: int) (matrix: PatternMatrix) : PatternMatrix = ...

// =============================================================================
// Usefulness Algorithm
// =============================================================================

/// 패턴 벡터가 행렬에 대해 useful한지 검사
let rec isUseful (matrix: PatternMatrix) (vector: PatternVector) (typeEnv: TypeDefEnv) : bool = ...

/// 누락된 패턴 찾기 (exhaustiveness)
let findMissingPatterns (patterns: LPattern list) (scrutineeType: Type) (typeEnv: TypeDefEnv) : LPattern list = ...

// =============================================================================
// Redundancy Check
// =============================================================================

/// 중복 패턴 검사 (각 패턴에 대해 이전 패턴들로 커버되는지)
let checkRedundancy (patterns: LPattern list) (typeEnv: TypeDefEnv) : (int * LPattern) list = ...

// =============================================================================
// Public API
// =============================================================================

type PatternWarning =
    | NonExhaustive of missing: LPattern list * position: Position
    | Redundant of pattern: LPattern * position: Position

/// match 표현식 분석
let analyzeMatch
    (scrutineeType: Type)
    (cases: (LPattern * LExpr option * LExpr) list)
    (typeEnv: TypeDefEnv)
    (pos: Position)
    : PatternWarning list = ...
```

---

## FsCheck 테스트

```fsharp
module FunLang.Tests.PatternAnalysisTests

open Expecto
open FsCheck
open FunLang.PatternAnalysis

let exhaustivenessTests = testList "Exhaustiveness" [
    test "bool patterns must cover true and false" {
        let patterns = [PLiteral (LBool true)]  // missing: false
        let warnings = analyzeMatch TBool [(noLoc patterns.[0], None, dummy)] Map.empty dummyPos
        Expect.isNonEmpty warnings "should warn about missing false"
    }

    test "wildcard covers all" {
        let patterns = [PWildcard]
        let warnings = analyzeMatch TInt [(noLoc patterns.[0], None, dummy)] Map.empty dummyPos
        Expect.isEmpty warnings "wildcard should cover all"
    }

    testProperty "n literal patterns for n-constructor type" <| fun (constructors: NonEmptyArray<string>) ->
        // n개 생성자 타입에서 n개 패턴이 있으면 exhaustive
        ...
]

let redundancyTests = testList "Redundancy" [
    test "duplicate literal is redundant" {
        let patterns = [PLiteral (LInt 0); PLiteral (LInt 0)]
        let redundant = checkRedundancy (List.map noLoc patterns) Map.empty
        Expect.equal (List.length redundant) 1 "second pattern should be redundant"
    }

    test "pattern after wildcard is redundant" {
        let patterns = [PWildcard; PLiteral (LInt 1)]
        let redundant = checkRedundancy (List.map noLoc patterns) Map.empty
        Expect.equal (List.length redundant) 1 "pattern after wildcard should be redundant"
    }
]

[<Tests>]
let allTests = testList "Pattern Analysis" [exhaustivenessTests; redundancyTests]
```

---

## 경고 메시지 예시

**Exhaustiveness:**
```
Warning at line 5, column 1: Non-exhaustive pattern match
  |
5 | match x with
  | ^^^^^
Missing cases:
  - Some _

Hint: Add a catch-all pattern `| _ -> ...` or handle missing cases
```

**Redundancy:**
```
Warning at line 8, column 3: Redundant pattern
  |
8 | | 0 -> "zero again"
  |   ^
This pattern will never be matched (covered by pattern at line 6)
```

---

## CLI 옵션

```bash
funlang --warn-incomplete     # 불완전 패턴 경고 (기본: on)
funlang --warn-redundant      # 중복 패턴 경고 (기본: on)
funlang --warn-all            # 모든 경고 활성화
funlang --warn-error          # 경고를 에러로 처리
```

---

## 구현 계획

| 단계 | 내용 | 예상 난이도 | 의존성 |
|------|------|-------------|--------|
| 9.1.1 | PatternMatrix 타입 정의 | 쉬움 | - |
| 9.1.2 | Specialize/Default 연산 | 중간 | 9.1.1 |
| 9.1.3 | Usefulness 알고리즘 | 어려움 | 9.1.2 |
| 9.1.4 | Exhaustiveness 검사 | 중간 | 9.1.3 |
| 9.1.5 | 경고 메시지 생성 | 쉬움 | 9.1.4 |
| 9.2.1 | Redundancy 검사 | 쉬움 | 9.1.3 |
| 9.2.2 | 경고 메시지 생성 | 쉬움 | 9.2.1 |
| 9.3.* | Decision Tree | 어려움 | 9.1, 9.2 |
