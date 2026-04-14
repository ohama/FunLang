# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-09)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v15.0 Type Class Maturity

## Current Position

Milestone: v15.0 Type Class Maturity
Phase: 103 — Fix Bidir.fs annotationMap for LambdaAnnot (complete)
Plan: 02 of 02
Status: Phase 103 fully complete (Issues #18 and #19 closed with regression tests); ready for Phase 96
Last activity: 2026-04-10 — Completed 103-02-PLAN.md (TA-09/TA-09b unit tests + flt regression test)

Progress: [██........] 25% (2/8 phases complete)

## Phase Summary

| Phase | Goal | Status |
|-------|------|--------|
| 96 — Correctness Foundations | Reliable type class infrastructure (TC-01–04) | Pending |
| 97 — Superclass Entailment | Superclass chain resolution (SC-01–03) | Pending |
| 98 — Constrained Instance Runtime Dispatch | Constrained instances evaluate correctly (CD-01–02) | Pending |
| 99 — Num Type Class | Num/Eq Prelude additions (NUM-01–03) | Pending |
| 100 — Derive for Parameterized ADTs | derive works on parameterized types (DRV-01–02) | Pending |
| 101 — Error Message Polish | Actionable type class errors (ERR-01–03) | Pending |
| 102 — Fix LambdaAnnot Span Collision | Unique spans for nested LambdaAnnot (Issue #18) | Complete |
| 103 — Fix Bidir.fs annotationMap for LambdaAnnot | annotationMap populated with per-param span (Issue #19) | Complete |
| 104 — Remove DuplicateRecordField(E0311) Check | Allow same field name across record types (Issue #21) | Complete |
| 105 — Fix TEName Elaboration to Resolve Named Types | `(p : SrcLoc) → p.field` annotation이 fresh TVar 대신 TData로 resolve (Issue #22) | Complete |
| 106 — Revert s.[i] : int to s.[i] : char | char 리터럴과 string indexing 결과 타입 통일 (Issue #23) | Complete |
| 107 — RecordExpr disambiguation via outer annotation | 동일 필드 집합의 여러 record 타입을 outer expected type으로 구분 (Issue #25) | Complete |
| 108 — Imported file spans in AnnotationMap | Prelude parser position tracking 수정 (Issue #26) | Complete |

## Performance Metrics

**Velocity:**
- Total plans completed: 175+
- v14.0: 5 phases in 1 day (2026-04-08)
- v12.0: 4 phases, 4 plans in 1 day

**Test baseline (start of v15.0):**
- 724 flt tests passing
- 244 F# unit tests passing

**Current test baseline (after Phase 103):**
- 727 flt tests passing
- 247 F# unit tests passing

## Accumulated Context

### Decisions

From v14.0 (Phase 91-95):
- Prelude 함수에서 `fun x ->` 패턴을 직접 인자로 펼침 완료
- 모든 Prelude 함수에 타입 어노테이션 추가
- OccursCheck 에러 메시지에 formatTypeNormalized 적용

From v10.0-v10.1 (Type Classes):
- typeclass/instance 선언, 제약 추론, 딕셔너리 elaboration
- Show/Eq 내장 인스턴스 (int/bool/string/char)
- ClassEnv/InstanceEnv export, instance method 승격

From v15.0 research (2026-04-08):
- Constrained instance runtime dispatch is broken: Elaborate.fs flattens all instance methods to literal names, so multiple instances of the same class collide. Name mangling needed for InstanceVars != [] instances only.
- Phase 98 (name mangling) is highest-risk: monomorphic instances must keep literal names.
- +/-/* operator dispatch must NOT be migrated to Num in v15.0 — 724-test regression risk.
- Exact mangling scheme must be finalized before Phase 98 implementation begins (e.g., `show__Show_list`).

### Pending Todos

3 low-severity bugs deferred from v10.1 (now addressed in v15.0 roadmap):
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude — keep as-is)
- Bug 9: E0701 shows internal type variable — addressed in TC-04 (Phase 96) and ERR-01 (Phase 101)
- Bug 10: E0704 never fires — addressed in ERR-02 (Phase 101)

### Roadmap Evolution

- Phase 102 added (2026-04-10): Fix LambdaAnnot span collision — desugarAnnotParams assigns unique spans per param (Issue #18)
- Phase 102 complete (2026-04-10): Per-param span injection in AnnotParam/MixedParam grammar rules; all 33+ callsites updated; 725 flt + 244 unit tests pass
- Phase 102-02 complete (2026-04-10): TA-08 unit test + flt regression test added; parseModuleWithPositions helper added to TypeAnnotationTests.fs for span-aware testing; 726 flt + 245 unit tests pass
- Phase 103 added (2026-04-10): Fix Bidir.fs annotationMap population — type checker must record arrow type using each LambdaAnnot's own span (Issue #19)
- Phase 103-01 complete (2026-04-10): 6-tuple LetRec/LetRecDecl binding with Span option; Parser.fsy captures LambdaAnnot paramSp; Bidir.fs/TypeCheck.fs record TArrow at firstSpOpt; 726 flt + 245 unit tests pass
- Phase 103-02 complete (2026-04-10): TA-09/TA-09b regression tests + letrec-annot-first-param-map.flt added; 727 flt + 247 unit tests pass; Phase 103 fully complete
- Phase 104 added (2026-04-13): Remove DuplicateRecordField(E0311) check — 동일 필드명을 가진 record 타입들을 허용하여 FunLang#20 수정 + FunLangCompiler#24 해결 경로 확보 (Issue #21)
- Phase 104 complete (2026-04-13): validateUniqueRecordFields 함수 + DuplicateRecordField error kind 제거, err-duplicate-record-field.flt 및 해당 unit test 삭제; 726 flt + 247 unit 테스트 통과; Issue #21 재현 케이스 정상 동작 확인
- Phase 105 added (2026-04-13): Issue #22 분석 결과 실제 원인은 import chain이 아니라 `Elaborate.fs:56-64`의 `TEName name → fresh TVar` 처리 버그. `let f (p : SrcLoc) = p.field` 같은 annotated parameter + field access 패턴이 단일 파일에서도 실패. `substTypeExprWithMap`은 올바르게 `TData(n, [])`로 처리하므로 `elaborateWithVars`도 동일하게 수정 필요.
- Phase 105 complete (2026-04-13): 근본적 해결 — `AliasInfo`/`AliasEnv` 타입 도입, `Elaborate.currentAliasEnv` mutable state 추가, `elaborateAliasDecl` 헬퍼 추가, `typeCheckDecls` first-pass에서 `TypeAliasDecl` 등록 (지금까지 no-op였음), `elaborateWithVars`의 `TEName`/`TEData` 분기에서 alias expansion 후 `TData` 생성. TA-11/12/13 unit test + record-annotated-param.flt + alias-annotated-param.flt 추가. 728 flt + 247 unit 통과. Issue #22의 FunLexYacc-style import chain + 단일 파일 annotated record param 재현 케이스 모두 정상 동작. 타입 alias가 드디어 실제로 구현됨.
- Phase 106 complete (2026-04-13): Issue #15의 `s.[i] : int` 결정을 반전 — `s.[i] : char`로 변경 (Bidir.fs:883-887 TInt→TChar, Eval.fs:1200 IntValue→CharValue). char 리터럴(' ', '\t' 등)과 string indexing 결과가 동일한 char 타입이 되어 `c = ' '` 같은 자연스러운 비교가 동작. string-index-get.flt 테스트 갱신 (char_to_int 사용), char-index-compare.flt 회귀 테스트 추가. 728 flt + 250 unit 통과.
- Phase 107 complete (2026-04-14): Issue #25 해결 — RecordExpr이 동일 필드 집합의 여러 record 타입에서 ambiguous할 때 `ctx` 스택의 `InCheckMode(TData, ...)`로 disambiguate. `check` fall-through에서 `InCheckMode` push하도록 개선하여 `Annot`뿐 아니라 함수 파라미터 등 모든 check 경로에서 outer expected type이 synth로 전달됨. record-ambiguous-disambiguation.flt 회귀 테스트 추가. 729 flt + 250 unit 통과.
- Phase 108 complete (2026-04-14): Issue #26 근본 해결 — `Prelude.parseModuleFromString`가 position-tracking 없는 plain tokenizer를 사용하여 import된 파일의 AST span이 모두 초기 위치(1:0)에 머물러 annotationMap에서 충돌/overwrite되던 문제. `Program.parseModuleFromString` 패턴을 차용해 `PositionedToken`으로 parse 시 `lb.StartPos`/`lb.EndPos`를 per-token 갱신. FunLangCompiler의 strict field disambiguation이 import된 라이브러리 함수 내부의 FieldAccess에서도 정상 동작. TA-14 unit test 추가, 730 flt + 251 unit 통과.

### Blockers/Concerns

- Phase 98 design gap: exact Eval.fs dispatch mechanism for mangled names at call sites needs a concrete decision before Phase 98 planning. The TypeAnnotationMap approach (Elaborate.fs rewriting call sites before evaluation) is the proposed direction.
- Phase 100 gap: verify whether `TypeDecl.deriving` (already parsed) is wired to the same code generation as `DerivingDecl`. Low-effort investigation needed before Phase 100 planning.

## Session Continuity

Last session: 2026-04-10
Stopped at: Completed 103-02-PLAN.md — TA-09/TA-09b regression tests for annotationMap let rec fix
Resume file: None
Next action: `/gsd:plan-phase 96`
