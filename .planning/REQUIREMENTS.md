# Requirements: v11.1 Builtin Compatibility

## Motivation

FunLangCompiler가 v11.0 Typed AST를 통합했으나, 컴파일러 Prelude에 type checker가 모르는 빌트인이 있어 `typeCheckFile`이 실패합니다. 누락된 8개 빌트인의 타입 시그니처와 런타임을 추가하여 호환성을 확보합니다.

**References:** ohama/FunLang#5

## v11.1 Requirements

### Builtin Type Signatures

- [ ] **BT-01**: `hashtable_*_str` 7개 빌트인 타입 시그니처를 `TypeCheck.fs` `initialTypeEnv`에 추가
- [ ] **BT-02**: `dbg` 빌트인 타입 시그니처 (`'a -> 'a`) 추가

### Builtin Runtime

- [ ] **BR-01**: `hashtable_*_str` 7개 런타임 구현을 `Eval.fs`에 추가
- [ ] **BR-02**: `dbg` 런타임 구현 (stderr 출력 + identity 반환)

### Verification

- [ ] **VR-01**: FunLangCompiler Prelude 코드가 `--emit-typed-ast`로 에러 없이 타입 체크

## Out of Scope

- FunLangCompiler 측 heuristic 제거 — 별도 repo
- 기존 hashtable_* 빌트인 변경 — 호환성 유지

## Traceability

| Requirement | Phase | Verified |
|-------------|-------|----------|
| BT-01       |       |          |
| BT-02       |       |          |
| BR-01       |       |          |
| BR-02       |       |          |
| VR-01       |       |          |
