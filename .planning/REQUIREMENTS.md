# Requirements: LangThree v8.0

**Defined:** 2026-03-30
**Core Value:** FunLexYacc 호환성을 위해 module-level 함수 선언에 F# 스타일 타입 어노테이션 지원

## v8.0 Requirements

### Parameter Type Annotations

- [ ] **PARAM-01**: `let` 선언에서 파라미터 타입 어노테이션 허용 — `let f (x : int) y (z : bool) = ...` (plain과 annotated 혼합 가능)
- [ ] **PARAM-02**: `let rec ... and ...` 상호 재귀 선언에서 파라미터 타입 어노테이션 허용

### Return Type Annotations

- [ ] **RET-01**: `let` 선언에서 반환 타입 어노테이션 허용 — `let f x : int = ...`
- [ ] **RET-02**: 파라미터 타입 + 반환 타입 동시 사용 — `let f (x : int) : bool = ...`

### Angle Bracket Generics

- [ ] **GEN-01**: `type` 선언에서 앵글 브래킷 사용 — `type Result<'a> = Ok of 'a | Error of string`
- [ ] **GEN-02**: 타입 표현식에서 앵글 브래킷 사용 — 람다/어노테이션에서 `Result<'a>` 타입 참조
- [ ] **GEN-03**: 기존 후위(postfix) 구문 호환 유지 — `'a option`, `int list` 등 기존 구문 정상 동작

## Future Requirements

- Named DU fields (`of loc: SrcLoc * msg: string`) — FunLexYacc ErrorInfo.fun에서만 사용
- `let private` 접근 제한자 — FunLexYacc에서 사용하나 v8.0 범위 밖

## Out of Scope

| Feature | Reason |
|---------|--------|
| 타입 어노테이션 타입 체크 강제 | 기존과 동일하게 파싱 후 런타임 소거 — 타입 추론으로 충분 |
| expression-level let 타입 어노테이션 | 이미 `(expr : type)` Annot 구문으로 지원됨 |
| Named DU fields | FunLexYacc 한 파일에서만 사용, 별도 마일스톤 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| PARAM-01 | — | Pending |
| PARAM-02 | — | Pending |
| RET-01 | — | Pending |
| RET-02 | — | Pending |
| GEN-01 | — | Pending |
| GEN-02 | — | Pending |
| GEN-03 | — | Pending |

**Coverage:**
- v8.0 requirements: 7 total
- Mapped to phases: 0
- Unmapped: 7

---
*Requirements defined: 2026-03-30*
*Last updated: 2026-03-30 after initial definition*
