# Roadmap: LangThree v1.6

## Current Milestone: v1.6 F#-Style Offside Rule

### Phase 23: IndentFilter Offside Rule Refactoring (COMPLETE)

**Goal:** F# LexFilter 방식의 CtxtLetDecl + offside column 기반 implicit `in` 구현
**Depends on:** v1.5 (complete)
**Requirements:** CTX-01~03, TOK-01~03, COV-01~06
**Status:** COMPLETE — CtxtLetDecl + InExprBlock + InModule contexts, 619 tests passing

Plans:
- [x] 23-01: F#-Style Offside Rule (context stack, offside IN, 5 tests)

**Success Criteria:**
1. `let x = 1 / let y = 2 / x + y` — 단순 let 체인 (in 없이)
2. `let rec f = fun xs -> match ... / let rec g = ... / body` — multiline fun -> 뒤 체인
3. `let b = (let inner = 5 / inner * 2) / a + b` — 중첩 let 블록
4. 모듈 안 let 선언은 영향 없음
5. 기존 explicit `in` 호환
6. 기존 테스트 전부 통과 (614+)

**Key Technical Approach:**
- IndentFilter에 `CtxtLetDecl of blockLet: bool * offsideCol: int` 컨텍스트 추가
- `LET` 토큰 시: 현재 컨텍스트가 expression block이면 blockLet=true
- `=` 토큰 시: RHS용 SeqBlock 컨텍스트 푸시
- offside column 도달 시: `IN` 토큰 삽입 (ODECLEND 역할)
- 모듈 컨텍스트에서는 blockLet=false → `IN` 삽입하지 않음

### Phase 24: List Separator Semicolon (COMPLETE)

**Goal:** 리스트 구분자를 콤마(`,`)에서 세미콜론(`;`)으로 변경. `[1, 2, 3]` → `[1; 2; 3]`. tests/, tutorial/ 전체 수정 포함.
**Depends on:** Phase 23
**Status:** COMPLETE — SemiExprList rule, 439 fslit + 196 F# tests passing

Plans:
- [x] 24-01: Core language changes (Parser.fsy SemiExprList rule, Format.fs, Eval.fs)
- [x] 24-02: Update test files to semicolon list syntax (439 tests pass)
- [x] 24-03: Update tutorial files to semicolon list syntax (16 .md files)

---
