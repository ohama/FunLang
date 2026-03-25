# Requirements: LangThree v2.1

**Defined:** 2026-03-25
**Core Value:** v2.0 기능의 정확성과 테스트 커버리지 확보

## v2.1 Requirements

### Runtime Bug Fixes

- [ ] **FIX-01**: `let rec` 함수가 1M iteration에서 stack overflow 없이 동작 (TCO trampoline 정상 작동)
- [ ] **FIX-02**: `let rec ... and ...` 상호 재귀 함수의 TCO 정상 작동 (LetRecDecl)
- [ ] **FIX-03**: 로컬 `let rec` (expression-level)의 TCO 정상 작동 (LetRec)

### Test Isolation

- [ ] **ISO-01**: F# 단위 테스트 214개 전체 통과 (비결정적 실패 제거)
- [ ] **ISO-02**: `typeCheckModule` 호출 간 전역 상태 격리 보장

### Test Coverage — Char (Phase 29)

- [ ] **TST-01**: char 리터럴 (`'a'`, `'Z'`, `'0'`) flt 테스트
- [ ] **TST-02**: char_to_int / int_to_char 변환 flt 테스트
- [ ] **TST-03**: char 비교 연산자 (`<`, `>`, `=`) flt 테스트

### Test Coverage — Parser Improvements (Phase 30)

- [ ] **TST-04**: multi-param `let rec` flt 테스트
- [ ] **TST-05**: unit param shorthand `let f () = body` flt 테스트
- [ ] **TST-06**: top-level `let ... in` 표현식 flt 테스트

### Test Coverage — File Import (Phase 31)

- [ ] **TST-07**: `open "lib.fun"` 기본 파일 임포트 flt 테스트
- [ ] **TST-08**: 임포트된 모듈의 qualified access flt 테스트

### Test Coverage — File I/O & System Builtins (Phase 32)

- [ ] **TST-09**: read_file / write_file flt 테스트
- [ ] **TST-10**: file_exists / append_file flt 테스트
- [ ] **TST-11**: read_lines / write_lines flt 테스트
- [ ] **TST-12**: get_args / get_env / get_cwd flt 테스트
- [ ] **TST-13**: eprint / path_combine / dir_files flt 테스트

### Compile Warnings

- [ ] **WARN-01**: IntegrationTests.fs FS0025 incomplete pattern match 경고 제거
- [ ] **WARN-02**: RecordTests.fs FS0025 경고 제거
- [ ] **WARN-03**: `dotnet test` 실행 시 0 warnings 달성

## Out of Scope

| Feature | Reason |
|---------|--------|
| 새 언어 기능 추가 | 버그 수정 마일스톤 — 기능은 v3.0에서 |
| 네이티브 컴파일 | 별도 마일스톤으로 진행 예정 |
| 성능 최적화 (TCO 외) | TCO 정상 작동만 — 바이트코드 VM 등은 별도 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FIX-01 | TBD | Pending |
| FIX-02 | TBD | Pending |
| FIX-03 | TBD | Pending |
| ISO-01 | TBD | Pending |
| ISO-02 | TBD | Pending |
| TST-01 | TBD | Pending |
| TST-02 | TBD | Pending |
| TST-03 | TBD | Pending |
| TST-04 | TBD | Pending |
| TST-05 | TBD | Pending |
| TST-06 | TBD | Pending |
| TST-07 | TBD | Pending |
| TST-08 | TBD | Pending |
| TST-09 | TBD | Pending |
| TST-10 | TBD | Pending |
| TST-11 | TBD | Pending |
| TST-12 | TBD | Pending |
| TST-13 | TBD | Pending |
| WARN-01 | TBD | Pending |
| WARN-02 | TBD | Pending |
| WARN-03 | TBD | Pending |

**Coverage:**
- v2.1 requirements: 21 total
- Mapped to phases: 0
- Unmapped: 21 ⚠️

---
*Requirements defined: 2026-03-25*
*Last updated: 2026-03-25 after initial definition*
