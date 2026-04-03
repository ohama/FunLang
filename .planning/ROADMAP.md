# Roadmap: FunLang

## Milestones

- ✅ **v1.0–v11.0** - Phases 1-82 (shipped 2026-04-03)
- 🚧 **v11.1 Builtin Compatibility** - Phase 83 (in progress)

## Phases

<details>
<summary>✅ v1.0–v11.0 (Phases 1-82) - SHIPPED 2026-04-03</summary>

Phases 1-82 delivered the complete FunLang interpreter with typed AST export.

See milestone archive for details.

</details>

### 🚧 v11.1 Builtin Compatibility (In Progress)

**Milestone Goal:** FunLangCompiler Prelude 호환을 위한 누락 빌트인 8개 추가 (타입 시그니처 + 런타임)

#### Phase 83: Builtin Compatibility

**Goal**: hashtable_*_str 7개 + dbg 1개 빌트인의 타입 시그니처와 런타임 추가
**Depends on**: Phase 82 (v11.0 complete)
**Requirements**: BT-01, BT-02, BR-01, BR-02, VR-01
**Success Criteria** (what must be TRUE):
  1. `hashtable_*_str` 7개 빌트인이 TypeCheck.fs `initialTypeEnv`에 등록됨
  2. `dbg` 빌트인이 `'a -> 'a` 타입으로 등록됨
  3. 모든 8개 빌트인이 Eval.fs에서 런타임 동작 (hashtable_*_str은 string-key hashtable 조작, dbg는 stderr 출력 + identity)
  4. FunLangCompiler Prelude 코드가 `--emit-typed-ast`로 에러 없이 JSON 출력
  5. 기존 테스트 전부 통과 (regression 없음)

Plans:
- [ ] 83-01: Add builtin type signatures and runtime (BT-01, BT-02, BR-01, BR-02, VR-01)

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 83. Builtin Compatibility | v11.1 | 0/1 | Not started | - |
