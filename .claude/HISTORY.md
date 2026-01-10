# FunLang Development History

## Current Status

- **Phase**: Phase 6 In Progress (Part 4)
- **Tests**: 287 passed
- **Last Session**: 2026-01-10

## Recent Sessions

### 2026-01-10 23:52 (Session: j0123456)

**주요 변경 사항:**
- Phase 6 Part 4: Constructor type inference 구현
- TypeInfer.fs: setConstructorEnv, lookupConstructor, inferTypeWithTypeDefEnv 추가
- ctorEnv를 ThreadLocal<TypeEnv>로 구현 (병렬 테스트 안전)
- TypeDefEnvBuilder: 음수 ID (-1, -2, ...) 사용하여 타입 변수 충돌 방지
- 5개 constructor type inference 테스트 추가 (287 total)

**시도한 실험:**
- mutable ctorEnv 사용 → 병렬 테스트에서 실패 (경쟁 조건)
- ThreadLocal<TypeEnv>로 변경 → 성공

**배운 점:**
- 타입 스킴 ID와 freshTypeVar() ID가 충돌하면 apply 함수에서 무한 루프
- 음수 ID로 분리하여 충돌 방지 (freshTypeVar는 양수만 생성)
- 병렬 테스트 환경에서 mutable state는 ThreadLocal 필수

**Key Decisions:**
- ctorEnv: ThreadLocal<TypeEnv> (병렬 테스트 안전)
- TypeDefEnvBuilder: 음수 ID 사용 (-1, -2, -3, ...)
- inferTypeWithTypeDefEnv: 생성자 환경 설정 후 타입 추론

**Resolved Issues:**
- issue-003: Infinite loop in type inference due to type variable ID collision

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 23:19 (Session: i9012345)

**주요 변경 사항:**
- Phase 6 Part 3: Type definition environment 구현
- TConstructor 타입 추가 (Types.fs)
- TypeDefEnvBuilder 모듈 구현
- Unification.fs에 TConstructor 통합 추가
- 6개 type definition environment 테스트 추가 (282 total)

**시도한 실험:**
- TypeDef에서 생성자 타입 스킴 자동 생성 → 성공

**배운 점:**
- 생성자 타입: nullary → `TypeName`, unary → `'a -> TypeName<'a>`
- TConstructor 통합: 같은 이름 + 같은 arity만 통합 가능
- TypeDefEnvBuilder로 TypeDef list → TypeEnv 변환

**Key Decisions:**
- TConstructor of string * Type list 형태
- TypeDefEnvBuilder.buildTypeDefEnv: TypeDef list → TypeEnv
- 생성자 스킴: `None : forall 'a. Option<'a>`, `Some : forall 'a. 'a -> Option<'a>`

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 23:08 (Session: h8901234)

**주요 변경 사항:**
- Phase 6 Part 2: Type Declaration 파싱 구현
- TypeDef, ConstructorDef, Program 타입 추가 (Ast.fs)
- Parser.fsy: type declaration grammar 추가
- ParserWrapper.fs: parseProgram, parseProgramString 함수 추가
- 4개 parser tests 활성화 및 통과 (276 total)

**시도한 실험:**
- type_def_list에 NEWLINE 요구 → EOF 전 NEWLINE 없을 때 파싱 실패
- type_def_or_list로 변경하여 optional trailing NEWLINE 처리

**배운 점:**
- FsYacc grammar에서 optional trailing delimiter 처리 방법
- Program vs Expr 분리로 backward compatibility 유지
- parseString은 main expression 추출, parseProgram은 전체 프로그램

**Key Decisions:**
- Program = { TypeDefs: TypeDef list; MainExpr: Expr option }
- prog 규칙이 Program 반환 (Expr 대신)
- parseString은 backward compatible (MainExpr 추출)
- Constructor expressions는 현재 EVariable/EApply로 파싱 (추후 개선)

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 22:58 (Session: g7890123)

**주요 변경 사항:**
- Phase 6 시작: User-Defined Types 구현 시작
- TYPEVAR 토큰 렉싱 ('a, 'b, etc.)
- EConstructor AST 노드, VConstructed 런타임 값 추가
- Interpreter: 생성자 평가, PConstructor 패턴 매칭 구현
- UserDefinedTypeTests.fs: 18개 테스트 추가 (272 total)
- Ast.Token 제거 (GeneratedParser.token 사용)

**시도한 실험:**
- Ast.fs에서 Token 타입 제거 → 컴파일 순서 문제 해결
- LexResult 타입 별칭을 ParserWrapper.fs로 이동

**배운 점:**
- F# 컴파일 순서: 타입은 정의된 후에만 참조 가능
- 생성된 파서 토큰과 중복 타입 정의 충돌 주의
- TDD로 EConstructor/VConstructed 구현 성공

**Key Decisions:**
- Ast.Token 제거, FunLang.Parser.Token 사용
- EConstructor: string * Expr option 형태
- VConstructed: string * Value option 형태
- TypeInfer는 플레이스홀더 (추후 완전 구현)

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 22:40 (Session: f6789012)

**주요 변경 사항:**
- Phase 5: Type System 완료 (254 tests, +48 type tests)
- Types.fs: Type, TypeScheme, Substitution 정의
- Unification.fs: Unification 알고리즘 (occurs check 포함)
- TypeInfer.fs: Algorithm W 구현 (모든 표현식 지원)
- TypeTests.fs: 48개 타입 추론 테스트

**시도한 실험:**
- inferMatch: List.map → fold로 변경 (substitution threading)
- TypeHelpers.counter: mutable → ThreadLocal로 변경

**배운 점:**
- inferMatch에서 패턴 케이스 순차 처리 필요 (substitution 공유)
- 병렬 테스트 시 mutable counter는 ThreadLocal 사용 필수
- Let-polymorphism: generalize → instantiate 패턴

**Key Decisions:**
- ThreadLocal<int>로 타입 변수 카운터 관리 (병렬 테스트 안전)
- inferMatch에서 fold로 substitution threading
- 2개의 버그 해결 후 문서화 (docs/issues/resolved/)

**Resolved Issues:**
- issue-001: OccursCheck error in recursive list function
- issue-002: Infinite loop in parallel test execution

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 20:05 (Session: c3d4e5f6)

**주요 변경 사항:**
- Type System Algorithm 문서화 (docs/TYPE_SYSTEM_ALGORITHM.md)
- Phase 5 상세 구현 계획 작성 (docs/PHASE5_TYPE_SYSTEM_PLAN.md)
- Hindley-Milner 타입 추론 알고리즘 정리

**배운 점:**
- Algorithm W의 각 표현식별 추론 규칙
- Unification과 Occurs Check의 중요성
- Let-polymorphism이 일반화/인스턴스화로 구현됨

**Key Decisions:**
- Types.fs → Unification.fs → TypeInfer.fs 순서로 구현
- TDD: 테스트 먼저 작성 후 구현
- Property-based testing으로 타입 시스템 검증

---

### 2026-01-10 19:47 (Session: b2c3d4e5)

**주요 변경 사항:**
- Context Handoff 시스템 구현 (HISTORY.md ↔ session commands)
- startsession: HISTORY.md에서 컨텍스트 복원
- endsession: HISTORY.md에 컨텍스트 저장

**배운 점:**
- AI 컨텍스트는 대화 단위로 관리됨 (새 대화 = 리셋)
- HISTORY.md를 통한 세션 간 핸드오프가 효과적

---

### 2026-01-10 19:22 (Session: a1b2c3d4)

**주요 변경 사항:**
- Phase 4: Pattern Matching 완료 (206 tests)
- Issue 관리 시스템 구현 (/issue command, docs/issues/ 저장)

**배운 점:**
- Pattern guard 실패 시 다음 케이스로 이동 (에러 아님)

---

## Accumulated Knowledge

### Phase 6: User-Defined Types
- TYPEVAR 렉싱: `'\'' ['a'-'z']+` 규칙
- EConstructor: 생성자 표현식 (name, optional arg)
- VConstructed: 런타임 값 (name, optional value)
- PConstructor: 패턴 매칭에서 생성자 분해
- 컴파일 순서: 타입 별칭은 정의된 후 참조 가능
- TypeDef: { Name; TypeParams; Constructors }
- Program: { TypeDefs; MainExpr option }
- type declaration syntax: `type Option 'a = None | Some of 'a`
- parseProgram: 전체 프로그램 파싱, parseString: main expression만 추출
- TConstructor of string * Type list: 사용자 정의 타입 (Option int 등)
- TypeDefEnvBuilder: TypeDef list → TypeEnv (생성자 타입 스킴 변환)
- 생성자 스킴: `None : forall 'a. Option<'a>`, `Some : forall 'a. 'a -> Option<'a>`
- ctorEnv: ThreadLocal<TypeEnv>로 병렬 테스트 안전하게 구현
- 타입 변수 ID 충돌 방지: TypeDefEnvBuilder는 음수 ID (-1, -2, ...) 사용
- inferTypeWithTypeDefEnv: 생성자 환경 설정 후 타입 추론 수행

### Type System Implementation
- Algorithm W: 표현식별 추론 → substitution 합성 → 최종 타입
- inferMatch: 패턴 케이스 순차 처리로 substitution threading 필수
- TypeHelpers.counter: ThreadLocal 사용 (병렬 테스트 안전)
- Let-polymorphism: generalize(env, τ) → instantiate(scheme)

### Session & Context Management
- AI 컨텍스트는 대화 단위로 관리됨
- `/endsession` → 새 대화 시작 → `/startsession` 워크플로우
- HISTORY.md에 세션 간 컨텍스트 핸드오프 저장

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함
- Multiline: nl_opt rule for optional NEWLINE
- Ast.Token 중복 제거 → GeneratedParser.token 사용

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- "(fun x -> x) -1" → 뺄셈으로 해석됨

### Architecture Decisions
- Post-lexer indentation processing (Python 스타일)
- EBlock이 마지막 표현식 값 반환
- 괄호 내 들여쓰기 무시 (Python처럼)

### Phase Completion History
- Phase 0: Infrastructure Setup - COMPLETE
- Phase 1: Core Expressions - COMPLETE
- Phase 1.2: Indentation-Based Syntax - COMPLETE
- Phase 2 + 3: Functions & Data Structures - COMPLETE
- Phase 4: Pattern Matching - COMPLETE
- Phase 5: Type System - COMPLETE
- Phase 6: User-Defined Types - IN PROGRESS (Part 3 done: type definition environment)
