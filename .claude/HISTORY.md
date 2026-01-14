# FunLang Development History

## Current Status

- **Version**: v0.6.0
- **Phase**: Phase 11 Complete (WASM Backend MVP)
- **Tests**: 660 passed, 0 failed
- **Last Session**: 2026-01-14

## Recent Sessions

### 2026-01-14 (Session: wasm-backend-001)

**주요 변경 사항:**
- WASM Backend Phase 1 MVP 완료
- v0.6.0 릴리스
- 새 파일 생성:
  - `docs/wasm-backend.md` (설계 문서)
  - `src/FunLang/WasmTypes.fs` (WASM IR 타입)
  - `src/FunLang/WasmCompiler.fs` (AST → WASM IR)
  - `src/FunLang/WasmEmitter.fs` (WASM IR → Binary)
- CLI 옵션 추가: `--target wasm|wat`, `--output <path>`
- Logging.fs: `CompileTarget` 타입, `Compile` phase 추가
- Options.fs: Target, Output 옵션 추가
- Program.fs: `runCompile` 함수 추가

**작동하는 기능:**
```bash
# WAT 텍스트 포맷 생성
funlang --target wat --output hello.wat -e "1 + 2 * 3"

# WASM 바이너리 생성
funlang --target wasm --output hello.wasm -e "1 + 2 * 3"

# wasmtime으로 실행
wasmtime --invoke main hello.wasm  # => 7
```

**지원 기능 (MVP):**
- 정수 리터럴, 불리언 리터럴
- 산술 연산: +, -, *, /, %
- 비교 연산: <, >, <=, >=, =, <>
- 논리 연산: and, or, not
- let 바인딩 (지역 변수로 컴파일)
- if/then/else (WASM if 블록으로 컴파일)

**미지원 기능:**
- Lambda/클로저 (메모리 관리 필요)
- 재귀 함수
- 리스트, 튜플 (메모리 관리 필요)
- 패턴 매칭
- 사용자 정의 타입
- 문자열
- 모듈 시스템

**배운 점:**
- WASM 바이너리 포맷은 섹션 기반 (Type, Function, Export, Code)
- LEB128 인코딩으로 가변 길이 정수 표현
- 스택 기반 실행 모델 (1 + 2 → const 1; const 2; add)
- let 바인딩은 WASM local 변수로 변환

**Key Decisions:**
- 의존성 없이 직접 WASM 바이너리 생성
- MVP는 정수/불리언/기본 연산만 지원
- WAT 텍스트 포맷도 디버깅용으로 지원

**Unresolved Issues:**
- (없음)

---

### 2026-01-13 17:11 (Session: q6789012)

**주요 변경 사항:**
- Module System Phase 1 MVP 완료
- v0.5.0 릴리스
- 새 파일 생성:
  - `docs/module-system-design.md` (903줄 설계 문서)
  - `src/FunLang/NameResolution.fs` (모듈 이름 해석)
- AST 확장: ModuleDecl, ImportDecl, QualifiedPath, ExportItem
- Lexer 토큰: module, export, import, open, qualified, as, hiding, DOT
- Parser 문법: module_decl, export_list, qualified_path
- TypeInfer: moduleEnv (ThreadLocal), qualified name lookup
- Interpreter: moduleValueEnv, qualified name evaluation
- README.md 모듈 시스템 섹션 추가

**작동하는 기능:**
```funlang
module Math =
  export add, multiply
  let add = fun x -> fun y -> x + y
  let multiply = fun x -> fun y -> x * y

Math.multiply (Math.add 2 3) 4  // => 20
```

**현재 제한사항:**
- 모듈 내 자기 참조 미지원 (Math.foo가 Math.bar 호출 불가)
- 중첩 모듈 미지원
- 다중 파일 모듈 미지원
- open/import 파싱만 되고 실행 미구현

**배운 점:**
- ThreadLocal로 병렬 테스트 안전성 확보
- Qualified name: moduleEnv[moduleName][valueName] 구조
- 모듈 값은 Map.empty 환경에서 평가

**Key Decisions:**
- Haskell-inspired + F#-style 모듈 문법 채택
- Phase 1 MVP: 단일 파일 내 모듈만 지원
- Phase 2+: 중첩 모듈, open/import, 다중 파일

**Unresolved Issues:**
- (없음)

---

### 2026-01-13 14:52 (Session: p5678901)

**주요 변경 사항:**
- Phase 9.3 완료: TypeInfer 통합 & Program.fs 경고 출력
- v0.3.0 릴리스 (패턴 분석 경고 포함)
- v0.4.0 릴리스 (--emit formatter with comment preservation)
- PLAN.md에서 알고리즘 문서 분리
- warning-tests/ 디렉토리 추가 (7개 테스트)

**배운 점:**
- 패턴 분석기가 TVar 받으면 infinite domain으로 처리
- List 패턴: `| [] -> ... | _ -> ...` (wildcard fallback)
- Bool 패턴: `| true -> ... | false -> ...` (명시적)

---

### 2026-01-12 06:25 (Session: n3456789)

**주요 변경 사항:**
- issue-009 발견: match INDENT after else 문제
- Sorting test 파일에서 `in` 키워드 제거

---

### 2026-01-12 06:10 (Session: m2345678)

**주요 변경 사항:**
- 주석 기능 구현 (`// ...` 한 줄 주석)
- Expecto 필터 옵션 문서화

---

### 2026-01-12 05:53 (Session: l1234567)

**주요 변경 사항:**
- Multiline type definition 문법 지원

---

## Accumulated Knowledge

### WASM Backend (Phase 11) - 2026-01-14
- CLI: `--target wasm|wat`, `--output <path>`
- Pipeline: AST → WasmIR → Binary (직접 생성)
- LEB128: 가변 길이 정수 인코딩
- WASM 섹션: Type(0x01), Function(0x03), Export(0x07), Code(0x0a)
- let binding → local variable (LocalSet/LocalGet)
- if/then/else → WASM If block
- MVP 지원: int, bool, 산술/비교/논리, let, if

### Module System (Phase 10) - 2026-01-13
- 모듈 선언: `module Name = export ... let ...`
- Qualified access: `Module.function`
- ThreadLocal로 moduleEnv 관리 (병렬 테스트 안전)
- 자기 참조 미지원 (chicken-and-egg problem)

### Pattern Analysis (Phase 9) - 2026-01-13
- Maranget's Usefulness algorithm 구현
- Non-exhaustive: 누락된 패턴 경고
- Redundant: 도달 불가 패턴 경고
- TVar 타입은 infinite domain으로 처리됨

### Comment Syntax (2026-01-12)
- 한 줄 주석: `// comment text`
- Lexer.fsl의 `lineComment` 룰로 처리

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수

### Phase Completion History
- Phase 0~7: COMPLETE
- Phase 8.1~8.5: COMPLETE
- Phase 8.6: ON HOLD (REPL & Color Integration)
- Phase 9.0~9.3: COMPLETE (Pattern Analysis)
- Phase 9.4: PLANNED (Guard 지원 개선)
- Phase 10.1: COMPLETE (Module System MVP)
- Phase 10.2~10.3: PLANNED (중첩 모듈, 다중 파일)
- Phase 11: COMPLETE (WASM Backend MVP)
- Phase 11.2+: PLANNED (Functions, Closures, Memory)
