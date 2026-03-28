# Requirements: LangThree v6.0

**Defined:** 2026-03-28
**Core Value:** 뉴라인 암묵적 시퀀싱, 컬렉션 for-in 루프, Option/Result 유틸리티로 실용적 프로그래밍 완성

## v6.0 Requirements

### Newline Implicit Sequencing

- [ ] **NLSEQ-01**: Multi-line expressions at the same indent level inside a block are automatically sequenced (no explicit `;` needed)
- [ ] **NLSEQ-02**: Multi-line function application (indented continuation) is NOT broken by sequencing
- [ ] **NLSEQ-03**: Structural terminators (`else`, `with`, `|`, `then`) are not preceded by spurious SEMICOLON
- [ ] **NLSEQ-04**: Existing explicit `;` sequencing continues to work unchanged
- [ ] **NLSEQ-05**: Module-level declarations are NOT affected (no sequencing between `let` decls)

### For-In Collection Loops

- [ ] **FORIN-01**: `for x in [1; 2; 3] do body` iterates over list elements
- [ ] **FORIN-02**: `for x in arr do body` iterates over array elements
- [ ] **FORIN-03**: Loop variable `x` is immutable (E0320 on assignment)
- [ ] **FORIN-04**: `for x in [] do body` executes body zero times, returns unit

### Option/Result Utilities

- [ ] **OPTRES-01**: `Option.map`, `Option.bind`, `Option.defaultValue` functions in Prelude
- [ ] **OPTRES-02**: `Option.iter`, `Option.filter`, `Option.isSome`, `Option.isNone` functions
- [ ] **OPTRES-03**: `Result.map`, `Result.bind`, `Result.mapError` functions in Prelude
- [ ] **OPTRES-04**: `Result.defaultValue`, `Result.toOption` functions

### Test Coverage

- [ ] **TST-33**: Newline implicit sequencing flt tests (positive + regression)
- [ ] **TST-34**: For-in loop flt tests
- [ ] **TST-35**: Option/Result utility function flt tests
- [ ] **TST-36**: Tutorial chapter 22 update for v6.0 features

## Out of Scope

| Feature | Reason |
|---------|--------|
| `for (a, b) in pairs do` tuple destructuring | Pattern matching in loop variable deferred — adds parser complexity |
| `Seq` / lazy sequences | Separate milestone — requires fundamentally different evaluation model |
| `Option.map` dot-notation (module method syntax) | LangThree modules don't support method-call syntax |
| Float type | Explicitly excluded from this milestone per user request |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| NLSEQ-01 | Pending | Pending |
| NLSEQ-02 | Pending | Pending |
| NLSEQ-03 | Pending | Pending |
| NLSEQ-04 | Pending | Pending |
| NLSEQ-05 | Pending | Pending |
| FORIN-01 | Pending | Pending |
| FORIN-02 | Pending | Pending |
| FORIN-03 | Pending | Pending |
| FORIN-04 | Pending | Pending |
| OPTRES-01 | Pending | Pending |
| OPTRES-02 | Pending | Pending |
| OPTRES-03 | Pending | Pending |
| OPTRES-04 | Pending | Pending |
| TST-33 | Pending | Pending |
| TST-34 | Pending | Pending |
| TST-35 | Pending | Pending |
| TST-36 | Pending | Pending |

**Coverage:**
- v6.0 requirements: 17 total
- Mapped to phases: 0
- Unmapped: 17 ⚠️

---
*Requirements defined: 2026-03-28*
*Last updated: 2026-03-28 after initial definition*
