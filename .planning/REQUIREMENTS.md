# Requirements: v12.0 Infix Operator Reform

## Motivation

|>, >>, << 가 특수 AST 노드(PipeRight/ComposeRight/ComposeLeft)로 하드코딩되어 있어 확장이 불가능합니다. #[left N] / #[right N] attribute로 연산자 우선순위를 지정하고, 이 세 연산자를 Prelude에서 일반 함수로 정의하도록 변경합니다.

**References:** ohama/FunLang#6, ohama/FunLang#7

## v12.0 Requirements

### Attribute Infrastructure

- [ ] **ATTR-01**: `#[left N]` / `#[right N]` attribute 파싱 — Lexer에 `#[` 토큰 추가, Parser에 attribute 규칙 추가
- [ ] **ATTR-02**: Attribute를 AST LetDecl에 연결 — 연산자 정의 시 우선순위/결합성 지정

### Fixity System

- [ ] **FIX-01**: FixityEnv 모듈 — `Map<string, (Assoc * int)>` 우선순위 테이블, Prelude 로딩 시 구축
- [ ] **FIX-02**: Pratt post-processor — 연산자 체인을 flat list로 수집 후 우선순위 기반 tree로 변환
- [ ] **FIX-03**: 기존 INFIXOP0-4 backward compatibility — attribute 없으면 첫 문자 규칙 그대로 적용

### Operator Migration

- [ ] **MIG-01**: `|>`, `>>`, `<<` 를 Prelude/Core.fun에 `#[...]` attribute와 함께 정의
- [ ] **MIG-02**: PipeRight/ComposeRight/ComposeLeft AST 노드 제거 (Ast, Eval, Bidir, Infer, TypeCheck, Format 등)
- [ ] **MIG-03**: Lexer/IndentFilter에서 PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT 토큰 제거

### Verification

- [ ] **VER-01**: 기존 714 flt 테스트 전부 통과 (regression 없음)
- [ ] **VER-02**: TCO가 `|>` 체인에서 유지됨 (deep pipe chain 테스트)

## Future Requirements

- `#[infix N]` non-associative 연산자
- `#[inline]`, `#[deprecated]`, `#[test]` 등 attribute 확장
- 9 이상의 precedence level 지원

## Out of Scope

- fsyacc 교체 — 기존 LALR(1) 유지
- FunLangCompiler 측 PipeRight 핸들러 제거 — 별도 repo
- 임의 정수 precedence (0-9 범위로 충분)

## Traceability

| Requirement | Phase | Verified |
|-------------|-------|----------|
| ATTR-01     | 84    |          |
| ATTR-02     | 84    |          |
| FIX-01      | 85    |          |
| FIX-02      | 85    |          |
| FIX-03      | 85    |          |
| MIG-01      | 86    |          |
| MIG-02      | 86    |          |
| MIG-03      | 86    |          |
| VER-01      | 87    |          |
| VER-02      | 87    |          |
