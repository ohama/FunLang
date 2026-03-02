# Requirements: LangThree

**Defined:** 2026-02-25
**Core Value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Indentation Syntax

- [ ] **INDENT-01**: Offside rule for blocks (let, match, if-then-else 등)
- [ ] **INDENT-02**: Let-bindings 들여쓰기 연속
- [ ] **INDENT-03**: Match expressions 패턴 정렬
- [ ] **INDENT-04**: Function application 다중 라인
- [ ] **INDENT-05**: Module-level declarations
- [ ] **INDENT-06**: Spaces-only 강제 (탭 문자 거부)
- [ ] **INDENT-07**: Smart error recovery (들여쓰기 오류 시 명확한 메시지)
- [ ] **INDENT-08**: Configurable indent width (4칸 기본, 설정 가능)

### Algebraic Data Types

- [ ] **ADT-01**: Sum types with multiple constructors (`type Option = None | Some of 'a`)
- [ ] **ADT-02**: Constructor syntax & pattern matching
- [ ] **ADT-03**: Exhaustiveness checking (missing pattern 경고)
- [ ] **ADT-04**: Redundancy checking (unreachable pattern 경고)
- [ ] **ADT-05**: Type parameters (`'a`, `'b` 등)
- [ ] **ADT-06**: Recursive type definitions (자기 참조)
- [ ] **ADT-07**: Mutually recursive types (`type ... and ...`)

### Generalized Algebraic Data Types

- [ ] **GADT-01**: Explicit constructor return types (`Int : int -> int expr`)
- [ ] **GADT-02**: Type refinement in pattern matching
- [ ] **GADT-03**: Existential types in constructors
- [ ] **GADT-04**: Local type constraints

### Records

- [ ] **REC-01**: Record type declarations (`type Point = { x: float; y: float }`)
- [ ] **REC-02**: Record expressions (`{ x = 1.0; y = 2.0 }`)
- [ ] **REC-03**: Field access via dot notation (`point.x`)
- [ ] **REC-04**: Copy-and-update syntax (`{ point with y = 3.0 }`)
- [ ] **REC-05**: Pattern matching on records
- [ ] **REC-06**: Structural equality (같은 타입 내)
- [ ] **REC-07**: Mutable fields (`mutable` keyword)

### Modules

- [ ] **MOD-01**: Top-level module declarations
- [ ] **MOD-02**: Namespace declarations
- [ ] **MOD-03**: Nested modules (indentation 기반)
- [ ] **MOD-04**: `open` keyword for imports
- [ ] **MOD-05**: Qualified name access (`Module.function`)
- [ ] **MOD-06**: Implicit module from filename

### Exceptions

- [ ] **EXC-01**: Exception declarations (`exception MyError of string`)
- [ ] **EXC-02**: `raise` function
- [ ] **EXC-03**: `try...with` expressions
- [ ] **EXC-04**: Pattern matching on exception types
- [ ] **EXC-05**: `when` guards in exception handlers

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Advanced Features

- **GADT-V2-01**: Type equality witnesses
- **GADT-V2-02**: Phantom types (explicit support)
- **REC-V2-01**: Anonymous records (`{| x = 1 |}`)
- **MOD-V2-01**: Module aliases (`module M = Long.Path.Module`)
- **MOD-V2-02**: Recursive modules (`module rec M = ...`)
- **MOD-V2-03**: Module signatures (`.fsi` files)
- **EXC-V2-01**: `try...finally` (resource cleanup)
- **EXC-V2-02**: `failwith` / `failwithf` convenience functions
- **EXC-V2-03**: Reraise functionality

### Practical Features (Future Milestones)

- **IO-01**: File I/O
- **IO-02**: Console I/O
- **IO-03**: Network I/O
- **STDLIB-01**: Expanded standard library

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| OCaml-style functors | F# 스타일 선택, 복잡도 대비 실용성 낮음 |
| First-class modules | Runtime overhead, F#도 지원 안 함 |
| Checked exceptions | ML 커뮤니티가 거부, 동적 예외 사용 |
| Tab indentation | 버그의 원인, spaces-only 강제 |
| Anonymous sum types | ML family에 확립된 문법 없음 |
| Extensible variants | OCaml 특화, 복잡 |
| Row polymorphism | Records는 nominal typing으로 |
| Full dependent types | 범위 초과, indexed types만 |
| Native compilation | 인터프리터 유지 |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| INDENT-01 | Phase 1 | Complete |
| INDENT-02 | Phase 1 | Complete |
| INDENT-03 | Phase 1 | Complete |
| INDENT-04 | Phase 1 | Complete |
| INDENT-05 | Phase 1 | Complete |
| INDENT-06 | Phase 1 | Complete |
| INDENT-07 | Phase 1 | Complete |
| INDENT-08 | Phase 1 | Complete |
| ADT-01 | Phase 2 | Pending |
| ADT-02 | Phase 2 | Pending |
| ADT-03 | Phase 2 | Pending |
| ADT-04 | Phase 2 | Pending |
| ADT-05 | Phase 2 | Pending |
| ADT-06 | Phase 2 | Pending |
| ADT-07 | Phase 2 | Pending |
| GADT-01 | Phase 4 | Pending |
| GADT-02 | Phase 4 | Pending |
| GADT-03 | Phase 4 | Pending |
| GADT-04 | Phase 4 | Pending |
| REC-01 | Phase 3 | Pending |
| REC-02 | Phase 3 | Pending |
| REC-03 | Phase 3 | Pending |
| REC-04 | Phase 3 | Pending |
| REC-05 | Phase 3 | Pending |
| REC-06 | Phase 3 | Pending |
| REC-07 | Phase 3 | Pending |
| MOD-01 | Phase 5 | Pending |
| MOD-02 | Phase 5 | Pending |
| MOD-03 | Phase 5 | Pending |
| MOD-04 | Phase 5 | Pending |
| MOD-05 | Phase 5 | Pending |
| MOD-06 | Phase 5 | Pending |
| EXC-01 | Phase 6 | Pending |
| EXC-02 | Phase 6 | Pending |
| EXC-03 | Phase 6 | Pending |
| EXC-04 | Phase 6 | Pending |
| EXC-05 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 33 total
- Mapped to phases: 33
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-25*
*Last updated: 2026-02-25 after roadmap creation*
