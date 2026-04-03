# Requirements: v11.0 Typed AST Export

## Motivation

FunLangCompiler는 현재 FunLang의 타입 추론 결과를 사용하지 않고, ~250줄의 heuristic 코드(6개 추적 집합 + 8개 추측 함수)로 타입을 추측합니다. v11.0은 FunLang의 HM 타입 추론 결과를 export하여 컴파일러가 정확한 타입 정보를 사용할 수 있는 기반을 제공합니다.

**References:** ohama/FunLang#3, ohama/FunLang#4

## v11.0 Requirements

### Type Annotation Infrastructure

- [ ] **TA-01**: TypeAnnotationMap 모듈 — `Dictionary<Span, Type>`로 per-expression 타입 기록 구조 정의
- [ ] **TA-02**: Bidir.synth에서 모든 Expr 노드의 추론된 타입을 annotation map에 기록 (substitution 적용 후)

### Type Environment Export

- [ ] **TE-01**: 바인딩 타입 환경 export — top-level let 바인딩의 이름 → TypeScheme 매핑
- [ ] **TE-02**: Builtin/Prelude 타입 스킴을 export에 포함

### Export API

- [ ] **API-01**: ExportApi.fs — `typeCheckFile` 진입점 (TypedModule record 반환)
- [ ] **API-02**: TypedModule에 annotation map + binding env + builtin schemes 번들링

### CLI Integration

- [ ] **CLI-01**: `--emit-typed-ast` 플래그 — JSON 형식으로 타입 정보 출력

## Future Requirements

- FunLangCompiler에서 heuristic 제거 (별도 마일스톤)
- Full TypedExpr DU (현재는 span-keyed map으로 충분)
- 파일 기반 직렬화 (현재는 in-process API로 충분)

## Out of Scope

- FunLangCompiler 코드 변경 — 별도 repo, 별도 마일스톤
- TypedExpr 병렬 DU — over-engineering, span-keyed map이 consumer query model에 적합
- IDE/LSP 통합 — 언어 기능 완성 후

## Traceability

| Requirement | Phase | Verified |
|-------------|-------|----------|
| TA-01       |       |          |
| TA-02       |       |          |
| TE-01       |       |          |
| TE-02       |       |          |
| API-01      |       |          |
| API-02      |       |          |
| CLI-01      |       |          |
