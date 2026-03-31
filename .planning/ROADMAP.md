# Roadmap: LangThree v9.1 Span Fix

## Overview

v9.1 fixes the AST Span zeroing bug where all position information is lost through the IndentFilter pipeline. The fix embeds position data in each token, preserves it through filtering, and updates lexbuf positions in the custom tokenizer so the FsLexYacc parser records correct source locations.

## Milestones

<details>
<summary>v1.0-v9.0 (Phases 1-68) -- SHIPPED 2026-03-31</summary>

149 plans across 68 phases. See milestone-archive.md for details.

</details>

### v9.1 Span Fix (In Progress)

**Milestone Goal:** AST Span 위치 정보 정확성 — 에러 메시지에 실제 소스 위치 표시

## Phases

- [ ] **Phase 69: Span Position Fix** - PositionedToken + IndentFilter 위치 보존 + lexbuf 업데이트

## Phase Details

### Phase 69: Span Position Fix
**Goal**: All AST nodes carry correct source positions (line/column) through the IndentFilter pipeline
**Depends on**: Phase 68 (v9.0 complete)
**Requirements**: SPAN-01, SPAN-02, SPAN-03, SPAN-04, SPAN-05
**Success Criteria** (what must be TRUE):
  1. PositionedToken type exists with Token, StartPos, EndPos fields
  2. lexAndFilter returns PositionedToken list with correct positions from lexbuf
  3. IndentFilter processes PositionedToken list, preserving positions and assigning positions to inserted tokens
  4. parseModuleFromString updates lexbuf.StartPos/EndPos before returning each token to the parser
  5. Error messages from type checker show correct file:line:column (not :0:0:)
**Plans:** 1 plans
Plans:
- [ ] 69-01-PLAN.md — PositionedToken pipeline: type definition, IndentFilter update, lexbuf position propagation

## Progress

**Execution Order:** Phase 69

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 69. Span Position Fix | 0/1 | Not started | - |
