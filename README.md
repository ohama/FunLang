# LangThree

FunLang을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어.

F# 스타일의 문법과 현대적인 타입 시스템(ADT, GADT, Records)을 갖춘 언어를 목표로 합니다.

## Features (Planned)

| Feature | Description | Status |
|---------|-------------|--------|
| **Indentation Syntax** | F# 스타일 들여쓰기 기반 파싱 | Planned |
| **Algebraic Data Types** | Sum types, pattern matching, exhaustiveness | Planned |
| **GADT** | Type refinement, indexed type families | Planned |
| **Records** | Named fields, copy-and-update syntax | Planned |
| **Modules** | F# 스타일 namespace, open, qualified names | Planned |
| **Exceptions** | try...with expressions, pattern matching | Planned |

## Based On

[FunLang](../LangTutorial) v6.0:
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플
- Prelude (map, filter, fold 등)

## Tech Stack

- **F#** (.NET 10)
- **FsLexYacc** — 렉서/파서 생성기
- **Expecto** — 단위 테스트

## Project Structure

```
LangThree/
├── .planning/           # GSD planning documents
│   ├── PROJECT.md       # Project context
│   ├── REQUIREMENTS.md  # v1 requirements (33)
│   ├── ROADMAP.md       # 6 phases
│   └── research/        # Domain research
├── src/                 # Source code (TBD)
└── tests/               # Tests (TBD)
```

## Roadmap

### v1.0 — Type System Extension

| Phase | Goal |
|-------|------|
| 1 | Indentation-Based Syntax |
| 2 | Algebraic Data Types |
| 3 | Records |
| 4 | GADT |
| 5 | Module System |
| 6 | Exceptions |

### Future

- IO / 파일 시스템 / 네트워크
- 확장된 표준 라이브러리
- 더 나은 에러 메시지

## Example (Target Syntax)

```fsharp
// ADT with GADT
type _ expr =
    | Int : int -> int expr
    | Bool : bool -> bool expr
    | Add : int expr * int expr -> int expr
    | If : bool expr * 'a expr * 'a expr -> 'a expr

// Records
type Point = { x: float; y: float }

let origin = { x = 0.0; y = 0.0 }
let moved = { origin with x = 1.0 }

// Modules
module Math =
    let add x y = x + y
    let square x = x * x

open Math

// Exceptions
exception DivisionByZero of string

let divide x y =
    if y = 0 then
        raise (DivisionByZero "cannot divide by zero")
    else
        x / y

let result =
    try
        divide 10 0
    with
    | DivisionByZero msg -> 0
```

## References

### Pattern Matching Compilation

Jules Jacobs, "How to compile pattern matching" (2021)
- PDF: https://julesjacobs.com/notes/patternmatching/patternmatching.pdf
- Scala impl: https://julesjacobs.com/notes/patternmatching/pmatch.sc
- Based on: Maranget 2008 "Compiling pattern matching to good decision trees"

ML 스타일 패턴 매칭을 decision tree로 컴파일하는 알고리즘. 불필요한 테스트를 절대 생성하지 않으면서 코드 중복을 최소화하는 휴리스틱 사용.

**알고리즘 요약:**

1. 변수 패턴(`a is y`)을 RHS로 이동 (`let y = a in e`)
2. 첫 번째 clause에서 테스트할 생성자 패턴 선택 (휴리스틱: 다른 clause에 가장 많이 등장하는 것)
3. 이진 match 생성: `match# a with | C(a1,...,an) => [A] | _ => [B]`
4. 각 clause를 A/B로 분류:
   - (a) `a is C(...)` → 확장하여 A에 추가
   - (b) `a is D(...)` (D≠C) → B에 추가
   - (c) `a` 테스트 없음 → A와 B 모두에 추가
5. [A]와 [B]를 재귀적으로 처리

**종료 조건:** clause 리스트 비었으면 에러(비완전), 첫 clause가 비었으면 매칭 성공.

**휴리스틱:** case (c)에서 clause가 A/B 양쪽에 복제되므로, 가장 많은 clause에 등장하는 테스트를 선택하여 중복 최소화.

**타입 활용:** 가능한 생성자 집합 추적 → exhaustiveness/redundancy 검사에 활용 가능.

> LangThree에서 패턴 매칭 변환/최적화 시 이 알고리즘을 적용할 것.

## Development

```bash
# Build (after setup)
dotnet build

# Test
dotnet test

# Run
dotnet run --project LangThree -- --expr "1 + 2"
```

## License

MIT
