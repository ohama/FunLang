# FunLang Error Reference

FunLang 인터프리터가 보고하는 모든 에러 코드와 경고 코드의 레퍼런스.

## Error Format

```
error[E0301]: Type mismatch: expected int but got bool
 --> file.fun:3:5-10
    |
  3 |     x + true
    |         ^^^^
   = hint: Check that all branches of your expression return the same type
```

- `error[E0xxx]` / `warning[W0xxx]`: 코드와 심각도
- `-->`: 소스 위치 (file:line:column)
- 소스 코드 스니펫과 `^^^^` 밑줄
- `= hint:` 수정 가이드

---

## Type System Errors (E03xx)

### E0301 — Type Mismatch

타입이 일치하지 않을 때 발생. 가장 흔한 에러.

```
error[E0301]: Type mismatch: expected int but got bool
```

**발생 조건:**
- if/else 분기의 반환 타입이 다를 때
- 함수 인자 타입이 맞지 않을 때
- 타입 어노테이션과 실제 타입이 다를 때
- match 분기들의 반환 타입이 다를 때

**테스트:**
- `tests/flt/error/err-type-mismatch.flt`
- `tests/flt/error/err-type-emit.flt`
- `tests/flt/file/let/letrec-decl-param-annotation-error.flt`
- `tests/flt/file/let/letrec-mutual-expr-error.flt`
- `tests/flt/file/let/mut-type-mismatch-error.flt`
- `tests/flt/file/control/if-then-nonunit-error.flt`

---

### E0302 — Occurs Check

재귀적 타입을 구성할 수 없을 때 발생.

```
error[E0302]: Occurs check: cannot construct infinite type 'a = list 'a
```

**발생 조건:**
- 타입 변수가 자기 자신을 포함하는 타입에 통합될 때
- 예: `let f x = f` (f의 타입이 무한 확장)

**테스트:**
- `tests/flt/error/err-occurs-check.flt`

---

### E0303 — Unbound Variable

정의되지 않은 변수를 사용할 때 발생. "Did you mean?" 제안 포함.

```
error[E0303]: Unbound variable: prnt
   = hint: Did you mean 'print'?
```

**발생 조건:**
- 선언되지 않은 변수 참조
- 스코프 밖의 변수 접근
- 오타

**테스트:**
- `tests/flt/error/err-unbound-var.flt`

---

### E0304 — Not a Function

함수가 아닌 값을 함수처럼 호출할 때 발생.

```
error[E0304]: Type int is not a function and cannot be applied
```

**발생 조건:**
- 정수/문자열 등 비함수 값에 인자를 적용할 때
- 예: `let x = 42` 후 `x 10`

**테스트:**
- `tests/flt/error/err-not-a-function.flt`

---

### E0305 — Unbound Constructor

정의되지 않은 생성자를 사용할 때 발생. "Did you mean?" 제안 포함.

```
error[E0305]: Unbound constructor: Sone
   = hint: Did you mean 'Some'?
```

**발생 조건:**
- ADT 생성자 오타
- 스코프에 없는 타입의 생성자 사용

**테스트:**
- `tests/flt/error/err-unbound-ctor.flt`

---

### E0306 — Constructor Arity Mismatch

생성자에 잘못된 수의 인자를 전달할 때 발생.

```
error[E0306]: Constructor Some expects 1 argument(s) but was given 2
```

**발생 조건:**
- `Some(1, 2)` — Some은 인자 1개
- `Node(1)` — Node가 3개 인자를 기대하는 경우

**테스트:**
- `tests/flt/error/err-ctor-arity.flt`

---

### E0307 — Unbound Field

레코드에 존재하지 않는 필드를 접근할 때 발생.

```
error[E0307]: Record type Person has no field named 'age'
```

**테스트:**
- `tests/flt/error/err-unbound-field.flt`

---

### E0308 — Duplicate Field Name

레코드 식에서 같은 필드를 두 번 지정할 때 발생.

```
error[E0308]: Duplicate field name 'name' in record expression
```

**테스트:** 없음

---

### E0309 — Missing Fields

레코드 식에서 필수 필드가 누락되었을 때 발생.

```
error[E0309]: Record type Person is missing fields: age, email
```

**테스트:** 없음

---

### E0310 — Immutable Field Assignment

불변 레코드 필드에 할당을 시도할 때 발생.

```
error[E0310]: Field 'name' of record type Person is immutable and cannot be assigned
```

**발생 조건:**
- `mutable` 키워드 없이 선언된 레코드 필드에 `<-` 할당

**테스트:**
- `tests/flt/error/err-immutable-field.flt`

---

### E0311 — Duplicate Record Field

같은 이름의 필드가 여러 레코드 타입에 존재해 모호할 때 발생.

```
error[E0311]: Field 'name' is defined in both record types Person and Company
```

**테스트:**
- `tests/flt/error/err-duplicate-record-field.flt`

---

### E0312 — Not a Record

레코드 구문을 비레코드 타입에 사용할 때 발생.

```
error[E0312]: 'int' is not a record type
```

**테스트:** 없음

---

### E0313 — Field Access on Non-Record

비레코드 타입에서 필드 접근을 시도할 때 발생.

```
error[E0313]: Cannot access field on non-record type int
```

**테스트:**
- `tests/flt/error/err-field-access-non-record.flt`

---

### E0320 — Immutable Variable Assignment

불변 변수에 재할당을 시도할 때 발생.

```
error[E0320]: Cannot assign to immutable variable 'x'. Use 'let mut' to declare mutable variables.
```

**발생 조건:**
- `let x = 1` 후 `x <- 2` 시도
- for 루프 변수에 재할당 시도
- for-in 루프 변수에 재할당 시도
- 함수 파라미터에 재할당 시도

**테스트:**
- `tests/flt/file/let/mut-immutable-assign-error.flt`
- `tests/flt/file/let/mut-param-immutable-error.flt`
- `tests/flt/file/control/loop-for-immutable-error.flt`
- `tests/flt/file/control/loop-for-in-immutable-error.flt`

---

## GADT Errors (E04xx)

### E0401 — GADT Annotation Required

GADT match에서 타입 어노테이션이 필요할 때 발생. (v1.8에서 대부분 불필요해짐)

```
error[E0401]: GADT match requires type annotation on scrutinee of type Expr
```

**테스트:**
- `tests/FunLang.Tests/GadtTests.fs` (E0401이 발생하지 않는지 검증)

---

### E0402 — Existential Type Escape

GADT 패턴 매칭의 존재 타입 변수가 스코프를 벗어날 때 발생.

```
error[E0402]: Existential type variable 'a escapes its scope
```

**테스트:** 없음

---

### E0403 — GADT Return Type Mismatch

GADT 생성자의 반환 타입이 선언과 일치하지 않을 때 발생.

```
error[E0403]: GADT constructor IntLit return type mismatch: expected bool Expr but got int Expr
```

**테스트:** 없음

---

## Indexing Errors (E04xx)

### E0471 — Index on Non-Collection

배열/해시테이블이 아닌 값에 인덱싱을 시도할 때 발생.

```
error[E0471]: Cannot index into value of type int; expected array or hashtable
```

**테스트:**
- `tests/flt/file/array/index-type-error.flt`

---

## Module Errors (E05xx)

### E0501 — Circular Module Dependency

모듈 간 순환 의존이 있을 때 발생.

```
error[E0501]: Circular module dependency: A -> B -> A
```

**테스트:**
- `tests/flt/error/err-circular-module.flt`

---

### E0502 — Unresolved Module

존재하지 않는 모듈을 참조할 때 발생. "Did you mean?" 제안 포함.

```
error[E0502]: Unresolved module: Mth
   = hint: Did you mean 'Math'?
```

**테스트:**
- `tests/flt/file/module/module-error-unresolved.flt`
- `tests/flt/file/module/module-error-nested-unresolved.flt`
- `tests/flt/file/module/module-error-open-nonexistent.flt`

---

### E0503 — Duplicate Module Name

같은 이름의 모듈이 두 번 선언되었을 때 발생.

```
error[E0503]: Duplicate module name: Math
```

**테스트:**
- `tests/FunLang.Tests/ModuleTests.fs` ("duplicate module name produces E0503")

---

### E0504 — Forward Module Reference

정의 전에 모듈을 참조할 때 발생.

```
error[E0504]: Forward reference to module: Utils
```

**발생 조건:**
- 모듈은 위에서 아래로(top-to-bottom) 정의 순서를 따라야 함

**테스트:**
- `tests/FunLang.Tests/ModuleTests.fs` ("forward module reference produces E0504", "forward reference in nested module produces E0504")

---

## Exception Errors (E06xx)

### E0601 — Undefined Exception Constructor

선언되지 않은 예외를 사용할 때 발생.

```
error[E0601]: Undefined exception constructor: NotFound
```

**테스트:** 없음

---

### E0602 — Exception Arity Mismatch

예외 생성자에 잘못된 수의 인자를 전달할 때 발생.

```
error[E0602]: Exception constructor FileError expects 1 argument(s) but was given 2
```

**테스트:** 없음

---

### E0603 — Raise Not Exception

예외가 아닌 타입을 raise할 때 발생.

```
error[E0603]: Cannot raise non-exception type int
```

**테스트:** 없음

---

### E0604 — When Guard Not Bool

패턴 매칭의 when 가드가 bool이 아닐 때 발생.

```
error[E0604]: When guard must be bool but got int
```

**테스트:** 없음

---

## Type Class Errors (E07xx)

### E0701 — No Instance

필요한 타입 클래스 인스턴스가 없을 때 발생.

```
error[E0701]: No instance of Show for Foo
   = hint: Add an instance declaration for this type (Available instances: int, bool, string, char)
```

**테스트:**
- `tests/flt/file/typeclass/typeclass-builtin-eq-error.flt`
- `tests/flt/file/typeclass/typeclass-e0701-span.flt`
- `tests/flt/file/typeclass/typeclass-infer-errors.flt`

---

### E0702 — Duplicate Instance

같은 타입 클래스-타입 조합의 인스턴스가 중복 선언될 때 발생.

```
error[E0702]: Duplicate instance declaration: Show int
```

**테스트:**
- `tests/flt/file/typeclass/typeclass-infer-poly.flt`

---

### E0703 — Unknown Type Class

선언되지 않은 타입 클래스를 사용할 때 발생. "Did you mean?" 제안 포함.

```
error[E0703]: Unknown type class: Shw
   = hint: Did you mean 'Show'?
```

**테스트:**
- `tests/flt/file/typeclass/typeclass-e0703-annotation.flt`

---

### E0704 — Method Type Mismatch

인스턴스 메서드의 타입이 클래스 선언과 다를 때 발생.

```
error[E0704]: Method 'show' in instance Show has type int -> int but class declares 'a -> string
```

**Known Issue:** 현재 E0704 대신 E0301이 발생하는 경우가 있음 (기능적으로 정상).

**테스트:** 없음 (Bug 10 — deferred)

---

### E0705 — Missing Method

인스턴스에 필수 메서드 구현이 누락되었을 때 발생.

```
error[E0705]: Instance missing required method: show
```

**테스트:**
- `tests/flt/error/err-missing-method.flt`

---

### E0706 — Extra Method

인스턴스에 클래스에 선언되지 않은 메서드가 있을 때 발생.

```
error[E0706]: Instance declares unknown method 'display' for class Show
```

**테스트:**
- `tests/flt/error/err-extra-method.flt`

---

## Warnings (W0xxx)

### W0001 — Non-Exhaustive Match

패턴 매칭이 모든 경우를 커버하지 않을 때 발생.

```
warning[W0001]: Incomplete pattern match. Missing cases: None
   = hint: Add the missing cases or a wildcard pattern '_' to cover all values
```

**테스트:**
- `tests/FunLang.Tests/IntegrationTests.fs` (W0001 검증)
- `tests/FunLang.Tests/GadtTests.fs` (GADT exhaustiveness 검증)

---

### W0002 — Redundant Pattern

도달할 수 없는 패턴이 있을 때 발생.

```
warning[W0002]: Redundant pattern in clause 3. This case will never be reached.
```

**테스트:**
- `tests/FunLang.Tests/IntegrationTests.fs` (W0002 검증)

---

### W0003 — Non-Exhaustive Exception Handler

try-with 블록이 모든 예외를 처리하지 않을 때 발생.

```
warning[W0003]: Non-exhaustive exception handler: ...
```

**테스트:** 없음

---

## Runtime Errors

컴파일 타임이 아닌 실행 시 발생하는 에러들.

### Division by Zero

```
Error: Attempted to divide by zero.
```

**테스트:** `tests/flt/error/err-div-zero.flt`

### Array/MutableList Index Out of Bounds

```
Error: FunLangException (StringValue "Array index 5 out of bounds (length 3)")
Error: FunLangException (StringValue "MutableList index 5 out of bounds (length 1)")
```

**테스트:**
- `tests/flt/file/array/index-out-of-bounds.flt`
- `tests/flt/file/collection/mutablelist-bounds-error.flt`

### FunLangException (failwith / raise)

```
Error: FunLangException (StringValue "something went wrong")
Error: FunLangException (DataValue ("NotFound", None))
```

**테스트:**
- `tests/flt/file/exception/failwith-basic.flt`
- `tests/flt/file/exception/exception-basic.flt`

### Parse Error

```
Error: parse error: unexpected AND_KW at file.fun:3:4
    |
  3 |     and odd n = ...
    |     ^
```

**테스트:** 파서 에러는 정적 진단 코드 없이 위치 정보와 함께 출력됨.

---

## Test Coverage Summary

| Code | Category | flt Tests | Unit Tests | Total |
|------|----------|-----------|------------|-------|
| E0301 | Type Mismatch | 6 | - | 6 |
| E0302 | Occurs Check | 1 | - | 1 |
| E0303 | Unbound Variable | 1 | - | 1 |
| E0304 | Not a Function | 1 | - | 1 |
| E0305 | Unbound Constructor | 1 | - | 1 |
| E0306 | Constructor Arity | 1 | - | 1 |
| E0307 | Unbound Field | 1 | - | 1 |
| E0308 | Duplicate Field | - | - | 0 * |
| E0309 | Missing Fields | - | - | 0 * |
| E0310 | Immutable Field | 1 | - | 1 |
| E0311 | Duplicate Record Field | 1 | - | 1 |
| E0312 | Not a Record | - | - | 0 * |
| E0313 | Field Access Non-Record | 1 | - | 1 |
| E0320 | Immutable Assignment | 4 | - | 4 |
| E0401 | GADT Annotation | - | 1 | 1 |
| E0402 | Existential Escape | - | - | 0 * |
| E0403 | GADT Return Mismatch | - | - | 0 * |
| E0471 | Index Non-Collection | 1 | - | 1 |
| E0501 | Circular Dependency | 1 | - | 1 |
| E0502 | Unresolved Module | 3 | - | 3 |
| E0503 | Duplicate Module | - | 1 | 1 |
| E0504 | Forward Reference | - | 2 | 2 |
| E0601 | Undefined Exception | - | - | 0 * |
| E0602 | Exception Arity | - | - | 0 * |
| E0603 | Raise Not Exception | - | - | 0 * |
| E0604 | When Guard Not Bool | - | - | 0 * |
| E0701 | No Instance | 3 | - | 3 |
| E0702 | Duplicate Instance | 1 | - | 1 |
| E0703 | Unknown Type Class | 1 | - | 1 |
| E0704 | Method Type Mismatch | - | - | 0 * |
| E0705 | Missing Method | 1 | - | 1 |
| E0706 | Extra Method | 1 | - | 1 |
| W0001 | Non-Exhaustive Match | - | 4 | 4 |
| W0002 | Redundant Pattern | - | 1 | 1 |
| W0003 | Non-Exhaustive Handler | - | - | 0 |

**Coverage: 25/32 codes tested (78%)** — 7 codes untestable (marked `*`: fires as different error code or unreachable).

---

*Source: `src/FunLang/Diagnostic.fs` (error definitions)*
*Last updated: 2026-04-01*
