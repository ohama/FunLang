# Requirements: LangThree v9.0

**Defined:** 2026-03-31
**Core Value:** funproj.toml 기반 Cargo 스타일 빌드 시스템으로 멀티파일 프로젝트 체계적 관리

## v9.0 Requirements

### CLI 확장

- [x] **CLI-01**: `--check` 플래그 -- 타입 체크만 수행 (실행 없음), 모든 `open "file.fun"` 임포트 파일 포함, 결과를 stderr에 출력
- [x] **CLI-02**: `--deps` 플래그 -- 진입점 파일의 `open "file.fun"` 체인을 재귀적으로 추적하여 의존성 트리 출력
- [x] **CLI-03**: `--prelude` 플래그 -- Prelude 디렉토리 경로 명시적 지정 (기존 자동 탐색 대신)
- [x] **CLI-04**: `LANGTHREE_PRELUDE` 환경 변수 -- `--prelude` 미지정 시 환경 변수에서 Prelude 경로 읽기
- [x] **CLI-05**: 파일 임포트 캐싱 -- 동일 프로세스 내에서 이미 로드한 파일의 환경을 캐시하여 중복 파싱/타입 체크 방지

### 프로젝트 파일

- [x] **PROJ-01**: `funproj.toml` 파싱 -- TOML 포맷의 프로젝트 파일을 읽어 `[project]`, `[[executable]]`, `[[test]]` 섹션 해석
- [x] **PROJ-02**: `langthree build` 서브커맨드 -- `funproj.toml`의 모든 `[[executable]]` 타겟을 타입 체크
- [x] **PROJ-03**: `langthree build <name>` -- 특정 executable 타겟만 타입 체크
- [x] **PROJ-04**: `langthree test` 서브커맨드 -- `funproj.toml`의 모든 `[[test]]` 타겟을 실행
- [x] **PROJ-05**: `langthree test <name>` -- 특정 test 타겟만 실행
- [x] **PROJ-06**: `[project].prelude` 설정 -- `funproj.toml`에서 Prelude 경로 지정 (CLI/환경 변수보다 우선순위 낮음)
- [x] **PROJ-07**: flt 테스트 -- CLI 확장 + 프로젝트 파일 기능의 통합 테스트

## Out of Scope

| Feature | Reason |
|---------|--------|
| 증분 빌드 (mtime/hash 캐시) | 복잡도 HIGH -- v10.x 이후 별도 마일스톤 |
| `langthree run <name>` 서브커맨드 | build/test만 우선, run은 추후 |
| 패키지 매니저 / 외부 의존성 | 현재 프로젝트 규모에 과도 |
| IDE/LSP 통합 | 프로젝트 파일 기반이 먼저 필요 |
| TOML 이외 프로젝트 파일 포맷 | TOML 단일 포맷으로 통일 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CLI-01 | Phase 67 | Complete |
| CLI-02 | Phase 67 | Complete |
| CLI-03 | Phase 67 | Complete |
| CLI-04 | Phase 67 | Complete |
| CLI-05 | Phase 67 | Complete |
| PROJ-01 | Phase 68 | Complete |
| PROJ-02 | Phase 68 | Complete |
| PROJ-03 | Phase 68 | Complete |
| PROJ-04 | Phase 68 | Complete |
| PROJ-05 | Phase 68 | Complete |
| PROJ-06 | Phase 68 | Complete |
| PROJ-07 | Phase 68 | Complete |

**Coverage:**
- v9.0 requirements: 12 total
- Mapped to phases: 12
- Unmapped: 0

---
*Requirements defined: 2026-03-31*
