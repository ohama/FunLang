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
