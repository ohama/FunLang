# Requirements: LangThree v9.1

**Defined:** 2026-03-31
**Core Value:** AST Span 위치 정보 정확성 — 에러 메시지에 실제 소스 위치 표시

## v9.1 Requirements

### Span 수정

- [ ] **SPAN-01**: PositionedToken 타입 — 각 토큰에 StartPos/EndPos 위치 정보 첨부
- [ ] **SPAN-02**: lexAndFilter가 `PositionedToken list` 반환 — 렉싱 시 위치 캡처
- [ ] **SPAN-03**: IndentFilter가 PositionedToken 보존 — 삽입 토큰에 직전 토큰 위치 복사
- [ ] **SPAN-04**: parseModuleFromString/parseExprFromString에서 lexbuf.StartPos/EndPos 업데이트 — 파서에 정확한 위치 전달
- [ ] **SPAN-05**: flt 테스트 — Span이 올바르게 전파되는지 검증 (에러 메시지에 실제 행/열 표시)

## Out of Scope

| Feature | Reason |
|---------|--------|
| LSP/IDE 위치 지원 | Span 수정 후 별도 마일스톤 |
| Source map 생성 | 컴파일러 단계에서 필요, 현재 인터프리터 범위 외 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SPAN-01 | Phase 69 | Pending |
| SPAN-02 | Phase 69 | Pending |
| SPAN-03 | Phase 69 | Pending |
| SPAN-04 | Phase 69 | Pending |
| SPAN-05 | Phase 69 | Pending |

**Coverage:**
- v9.1 requirements: 5 total
- Mapped to phases: 5
- Unmapped: 0

---
*Requirements defined: 2026-03-31*
