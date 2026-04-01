# FunLexYacc Type Annotation Incompatibility Report

**Date:** 2026-03-30
**Context:** Phase 8 (08-07) compilation verification — `make funlex` fails with "parse error"
**Root Cause:** LangBackend 문법이 함수 파라미터/반환값 타입 어노테이션을 지원하지 않음

---

## 1. 문제 요약

FunLexYacc 소스 코드는 F# 스타일 타입 어노테이션을 광범위하게 사용하지만,
LangBackend의 문법(AbstractGrammar.md)에서는 이를 지원하지 않는다.

**영향 범위:** src/ 내 18개 파일, 총 ~656개 어노테이션

---

## 2. 문법 근거 (AbstractGrammar.md 기준)

### 2.1 함수 파라미터 — 타입 어노테이션 불가

```
// AbstractGrammar.md §2 Declarations
decl ::= 'let' IDENT param+ '=' expr
param ::= IDENT                           // ← IDENT만 허용, 타입 어노테이션 없음
```

**FunLexYacc 현재 코드 (파싱 실패):**
```fsharp
let bind (r : Result<'a>) (f : 'a -> Result<'b>) : Result<'b> =
//        ^^^^^^^^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^^^^^  ^^^^^^^^^^^
//        param 타입 어노테이션    param 타입 어노테이션    반환 타입 어노테이션
```

**LangBackend 호환 코드:**
```fsharp
let bind r f =
```

### 2.2 반환 타입 어노테이션 — 문법에 없음

`let` 선언에서 `) : ReturnType =` 형식의 반환 타입 어노테이션은 문법에 정의되어 있지 않다.

### 2.3 람다 파라미터 — 타입 어노테이션 가능 (유일한 예외)

```
// AbstractGrammar.md §3 Expressions
expr ::= 'fun' '(' IDENT ':' type_expr ')' '->' expr    // ← 람다에서만 지원
```

### 2.4 식 수준 타입 어노테이션 — 가능하지만 무시됨

```
// AbstractGrammar.md §3.1 Atomic Expressions
atom ::= '(' expr ':' type_expr ')'     // 코드 생성 시 무시됨
```

### 2.5 제네릭 타입 구문

```
// AbstractGrammar.md §5 Type Expressions
type_expr ::= tuple_type '->' type_expr
atomic_type ::= atomic_type 'list'       // 후위: int list
              | atomic_type IDENT        // 후위 타입 적용: 'a option
              | TYPE_VAR                 // 'a, 'b, ...
```

- **후위(postfix) 스타일만 지원:** `int list`, `string option`
- **`<>` 앵글 브래킷 미지원:** `Result<'a>` ← 파싱 불가
- **올바른 구문:** type 선언에서 `type Result 'a = ...`, 사용 시 후위 스타일

---

## 3. 호환되지 않는 패턴 분류

### 패턴 A: 함수 파라미터 타입 어노테이션 (~437회)

```fsharp
// 현재 (파싱 실패)
let parseLexSpec (fileName : string) (input : string) : Result<LexSpec> =

// 수정 후
let parseLexSpec fileName input =
```

**세부 유형:**

| 유형 | 예시 | 출현 수 (추정) |
|------|------|---------------|
| 단순 타입 | `(c : int)`, `(s : string)` | ~120 |
| 리스트 타입 | `(items : string list)` | ~80 |
| 배열 타입 | `(argv : string array)` | ~30 |
| 옵션 타입 | `(opt : int option)` | ~40 |
| 레코드/ADT 타입 | `(nfa : Nfa)`, `(spec : LexSpec)` | ~90 |
| 함수 타입 | `(f : 'a -> Result<'b>)` | ~30 |
| 튜플 타입 | `(pair : int * string)` | ~25 |
| 제네릭 타입 | `(r : Result<'a>)` | ~22 |

### 패턴 B: 반환 타입 어노테이션 (~219회)

```fsharp
// 현재 (파싱 실패)
let isEmpty (cset : Cset) : bool =

// 수정 후
let isEmpty cset =
```

### 패턴 C: 앵글 브래킷 제네릭 구문 (~55회)

```fsharp
// 현재 (파싱 실패)
type Result<'a> =
let bind (r : Result<'a>) (f : 'a -> Result<'b>) : Result<'b> =

// 수정 후 — type 선언
type Result 'a =

// 수정 후 — 사용 시 (어노테이션 자체가 제거되므로 대부분 해당 없음)
```

---

## 4. 파일별 영향 분석

| 파일 | 파라미터 | 반환 | 합계 | 심각도 |
|------|---------|------|------|--------|
| funyacc/YaccEmit.fun | 70 | 32 | **102** | CRITICAL |
| funlex/LexParser.fun | 43 | 35 | **78** | CRITICAL |
| funyacc/GrammarParser.fun | 41 | 32 | **73** | CRITICAL |
| funlex/LexEmit.fun | 37 | 15 | **52** | HEAVY |
| funlex/Nfa.fun | 36 | 11 | **47** | HEAVY |
| funyacc/ParserTables.fun | 37 | 11 | **48** | HEAVY |
| common/Cset.fun | 27 | 14 | **41** | HEAVY |
| funyacc/Ielr.fun | 28 | 5 | **33** | HEAVY |
| funlex/Dfa.fun | 20 | 11 | **31** | HEAVY |
| funlex/DfaMin.fun | 18 | 7 | **25** | MODERATE |
| common/ErrorInfo.fun | 13 | 8 | **21** | MODERATE |
| funyacc/FirstFollow.fun | 13 | 8 | **21** | MODERATE |
| funyacc/Lalr.fun | 14 | 5 | **19** | MODERATE |
| funyacc/Lr0.fun | 11 | 7 | **18** | MODERATE |
| common/Symtab.fun | 8 | 6 | **14** | MODERATE |
| funyacc/FunyaccMain.fun | 9 | 4 | **13** | MODERATE |
| funlex/FunlexMain.fun | 8 | 4 | **12** | MODERATE |
| common/Diagnostics.fun | 4 | 4 | **8** | LOW |
| funlex/LexSyntax.fun | 0 | 0 | **0** | CLEAN |
| funyacc/GrammarSyntax.fun | 0 | 0 | **0** | CLEAN |
| **합계** | **~437** | **~219** | **~656** | |

상위 5개 파일이 전체의 53.7% (352/656)를 차지한다.

---

## 5. 마이그레이션 전략

### 5.1 필수 변환

| 변환 | Before | After | 비고 |
|------|--------|-------|------|
| 파라미터 타입 제거 | `let f (x : int) =` | `let f x =` | 437회 |
| 반환 타입 제거 | `let f x : int =` | `let f x =` | 219회 |
| 제네릭 `<>` → 공백 | `type Result<'a> =` | `type Result 'a =` | type 선언만 |
| named DU fields 제거 | `of loc: SrcLoc * msg: string` | `of SrcLoc * string` | ErrorInfo.fun |

### 5.2 자동화 가능성

- **파라미터 타입 어노테이션:** 정규식으로 `(IDENT : TYPE)` → `IDENT` 변환 가능하나, 중첩 괄호/함수 타입/튜플 타입 때문에 단순 정규식으로는 불완전
- **반환 타입 어노테이션:** `) : TYPE =` 패턴 탐지 — 타입 표현식이 복잡할 수 있어 수동 검증 필요
- **권장:** 파일별 수동 변환 + 컴파일 검증 반복

### 5.3 주의사항

1. **타입 어노테이션 제거 시 의미 변화 없음** — LangBackend는 타입 추론 기반이므로 어노테이션은 문서화 목적
2. **코드 가독성 저하** — 타입 정보가 사라지면 코드 이해가 어려워짐. 주석으로 보완 권장
3. **`private` 키워드** — AbstractGrammar.md에 `let private` 구문이 없으므로 이것도 제거 필요할 수 있음
4. **named DU fields** — `of loc: SrcLoc * msg: string` 형식은 ErrorInfo.fun에서만 사용, `of SrcLoc * string`으로 변환 필요

---

## 6. 추가 발견: `let private` 호환성

FunLexYacc에서 `let private` 사용 현황 확인 필요:

```
// F# 스타일
let private parseArgs (argv : string list) : Result<string * string> =

// LangBackend 문법에 'private'가 없다면:
let parseArgs argv =
```

---

## 7. 대안: LangBackend 파서 확장

타입 어노테이션 제거 대신 LangBackend 파서를 확장하는 방안:

```
// 현재
param ::= IDENT

// 확장안
param ::= IDENT
        | '(' IDENT ':' type_expr ')'
```

**장점:** FunLexYacc 소스 수정 불필요, F# 호환성 유지
**단점:** LangBackend 파서/AST 수정 필요, 타입 정보 처리 로직 추가

반환 타입 어노테이션도 유사하게 확장 가능:
```
decl ::= 'let' IDENT param+ ':' type_expr '=' expr    // 반환 타입 포함
```

---

*이 문서는 Phase 8 (08-07) 컴파일 검증 과정에서 발견된 문제를 기록한다.*
