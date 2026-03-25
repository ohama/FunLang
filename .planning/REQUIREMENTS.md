# Requirements: LangThree v2.2

**Defined:** 2026-03-25
**Core Value:** qualified module access와 누락 테스트 커버리지 확보

## v2.2 Requirements

### Module Access Bug Fixes

- [ ] **MOD-01**: 임포트된 파일의 모듈 qualified access 동작 (`open "file.fun"` 후 `Module.func` 호출 시 E0313 해결)
- [ ] **MOD-02**: Prelude qualified access 동작 (`List.length`, `List.map`, `List.head` 등 E0313 해결)

### Parser Bug Fix

- [ ] **PAR-01**: failwith 인라인 try-with 파싱 동작 (`try failwith "boom" with e -> "caught"` parse error 해결)

### Test Coverage

- [ ] **TST-14**: failwith flt 테스트 (기본 동작 + try-with 캐치)
- [ ] **TST-15**: LetPatDecl `let (a, b) = (1, 2)` 모듈 레벨 flt 테스트
- [ ] **TST-16**: 임포트 모듈 qualified access flt 테스트 (`open "file.fun"` 후 `Module.func`)
- [ ] **TST-17**: Prelude qualified access flt 테스트 (`List.length`, `List.map`)

## Out of Scope

| Feature | Reason |
|---------|--------|
| 새 언어 기능 | 버그 수정 마일스톤 |
| 네이티브 컴파일 | 별도 마일스톤 |
| Prelude를 module List로 감싸기 | MOD-02 수정이 이 방식이 아닐 수 있음 — 구현 시 결정 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| MOD-01 | Phase 36 | Pending |
| MOD-02 | Phase 36 | Pending |
| PAR-01 | Phase 36 | Pending |
| TST-14 | Phase 37 | Pending |
| TST-15 | Phase 37 | Pending |
| TST-16 | Phase 37 | Pending |
| TST-17 | Phase 37 | Pending |

**Coverage:**
- v2.2 requirements: 7 total
- Mapped to phases: 7
- Unmapped: 0

---
*Requirements defined: 2026-03-25*
*Traceability updated: 2026-03-25 (v2.2 roadmap — Phases 36-37)*
