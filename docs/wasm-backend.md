# FunLang WASM Backend Design

FunLang을 WebAssembly(WASM)로 컴파일하기 위한 백엔드 설계 문서입니다.

## Table of Contents

- [1. Overview](#1-overview)
- [2. Architecture](#2-architecture)
- [3. WASM IR Design](#3-wasm-ir-design)
- [4. Compilation Rules](#4-compilation-rules)
- [5. Binary Format](#5-binary-format)
- [6. Limitations (MVP)](#6-limitations-mvp)
- [7. Future Work](#7-future-work)

---

## 1. Overview

### 1.1 목표

FunLang 소스 코드를 WebAssembly 바이너리로 컴파일하여 다양한 WASM 런타임(wasmtime, Node.js, 브라우저)에서 실행할 수 있도록 합니다.

```bash
# 목표 사용 예시
funlang --target wasm -o output.wasm -e "1 + 2 * 3"
wasmtime output.wasm --invoke main
# => 7
```

### 1.2 WASM 타겟 선택 이유

| 장점 | 설명 |
|------|------|
| **이식성** | 모든 주요 플랫폼에서 실행 가능 (브라우저, 서버, 임베디드) |
| **성능** | 네이티브에 근접한 실행 속도 |
| **보안** | 샌드박스 환경에서 안전하게 실행 |
| **표준화** | W3C 표준으로 안정적인 스펙 |

### 1.3 MVP 범위

**포함:**
- 정수 연산 (사칙연산, 비교, 논리)
- Let 바인딩
- If/then/else 조건문

**제외 (향후 구현):**
- 함수/클로저
- 리스트/튜플
- 패턴 매칭
- 사용자 정의 타입

---

## 2. Architecture

### 2.1 컴파일 파이프라인

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  FunLang    │───▶│   Parse     │───▶│  Type Check │───▶│  Compile    │
│  Source     │    │   (AST)     │    │  (Types.fs) │    │  (WasmIR)   │
└─────────────┘    └─────────────┘    └─────────────┘    └──────┬──────┘
                                                                │
                                                                ▼
                   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
                   │  .wasm      │◀───│   Emit      │◀───│  WasmModule │
                   │  binary     │    │  (Binary)   │    │  (IR)       │
                   └─────────────┘    └─────────────┘    └─────────────┘
```

### 2.2 모듈 구조

```
src/FunLang/
├── WasmTypes.fs      # WASM IR 타입 정의
├── WasmCompiler.fs   # AST → WASM IR 변환
└── WasmEmitter.fs    # WASM IR → 바이너리 출력
```

### 2.3 기존 파이프라인과의 통합

```fsharp
// Program.fs
match opts.Target with
| Interpret -> eval env ast        // 기존: 인터프리터 실행
| Wasm      -> compileToWasm ast   // 신규: WASM 컴파일
| Wat       -> compileToWat ast    // 신규: WAT 텍스트 출력
```

---

## 3. WASM IR Design

### 3.1 Value Types

```fsharp
/// WASM 값 타입
type WasmValType =
    | I32    // 32-bit 정수 (int, bool)
    | I64    // 64-bit 정수 (향후)
    | F32    // 32-bit 부동소수점 (향후)
    | F64    // 64-bit 부동소수점 (향후)
```

**FunLang → WASM 타입 매핑:**

| FunLang Type | WASM Type | 표현 |
|--------------|-----------|------|
| `int` | `I32` | 그대로 |
| `bool` | `I32` | `true` = 1, `false` = 0 |
| `unit` | (없음) | 값 없음 |

### 3.2 Instructions (MVP)

```fsharp
/// WASM 명령어 (MVP 서브셋)
type WasmInstr =
    // ===== Constants =====
    | I32Const of int           // i32.const n

    // ===== Arithmetic =====
    | I32Add                    // i32.add
    | I32Sub                    // i32.sub
    | I32Mul                    // i32.mul
    | I32DivS                   // i32.div_s (signed)
    | I32RemS                   // i32.rem_s (signed modulo)

    // ===== Comparison =====
    | I32Eqz                    // i32.eqz (== 0, for 'not')
    | I32Eq                     // i32.eq
    | I32Ne                     // i32.ne
    | I32LtS                    // i32.lt_s (signed)
    | I32GtS                    // i32.gt_s (signed)
    | I32LeS                    // i32.le_s (signed)
    | I32GeS                    // i32.ge_s (signed)

    // ===== Logical (bitwise) =====
    | I32And                    // i32.and
    | I32Or                     // i32.or

    // ===== Control Flow =====
    | If of result: WasmValType option
         * thenBlock: WasmInstr list
         * elseBlock: WasmInstr list
    | End                       // end (block terminator)

    // ===== Local Variables =====
    | LocalGet of idx: int      // local.get $idx
    | LocalSet of idx: int      // local.set $idx
    | LocalTee of idx: int      // local.tee $idx (set and return)
```

### 3.3 Function Structure

```fsharp
/// WASM 함수 정의
type WasmFunc = {
    Name: string                        // 함수 이름 (디버깅용)
    Params: (string * WasmValType) list // 파라미터 (이름, 타입)
    Results: WasmValType list           // 반환 타입 (0개 또는 1개)
    Locals: (string * WasmValType) list // 로컬 변수
    Body: WasmInstr list                // 함수 본문
}
```

### 3.4 Module Structure

```fsharp
/// WASM 모듈
type WasmModule = {
    Functions: WasmFunc list            // 함수 목록
    Exports: (string * int) list        // (export명, 함수인덱스)
}
```

---

## 4. Compilation Rules

### 4.1 Literals

| FunLang | WASM Instructions | 스택 효과 |
|---------|-------------------|-----------|
| `42` | `i32.const 42` | [] → [i32] |
| `true` | `i32.const 1` | [] → [i32] |
| `false` | `i32.const 0` | [] → [i32] |

### 4.2 Binary Operators

| FunLang | WASM Instructions | 스택 효과 |
|---------|-------------------|-----------|
| `a + b` | `compile(a); compile(b); i32.add` | [i32, i32] → [i32] |
| `a - b` | `compile(a); compile(b); i32.sub` | [i32, i32] → [i32] |
| `a * b` | `compile(a); compile(b); i32.mul` | [i32, i32] → [i32] |
| `a / b` | `compile(a); compile(b); i32.div_s` | [i32, i32] → [i32] |
| `a % b` | `compile(a); compile(b); i32.rem_s` | [i32, i32] → [i32] |

### 4.3 Comparison Operators

| FunLang | WASM Instructions | 스택 효과 |
|---------|-------------------|-----------|
| `a = b` | `compile(a); compile(b); i32.eq` | [i32, i32] → [i32] |
| `a <> b` | `compile(a); compile(b); i32.ne` | [i32, i32] → [i32] |
| `a < b` | `compile(a); compile(b); i32.lt_s` | [i32, i32] → [i32] |
| `a > b` | `compile(a); compile(b); i32.gt_s` | [i32, i32] → [i32] |
| `a <= b` | `compile(a); compile(b); i32.le_s` | [i32, i32] → [i32] |
| `a >= b` | `compile(a); compile(b); i32.ge_s` | [i32, i32] → [i32] |

### 4.4 Logical Operators

| FunLang | WASM Instructions | 스택 효과 |
|---------|-------------------|-----------|
| `a and b` | `compile(a); compile(b); i32.and` | [i32, i32] → [i32] |
| `a or b` | `compile(a); compile(b); i32.or` | [i32, i32] → [i32] |
| `not a` | `compile(a); i32.eqz` | [i32] → [i32] |

### 4.5 Unary Operators

| FunLang | WASM Instructions | 스택 효과 |
|---------|-------------------|-----------|
| `-a` | `i32.const 0; compile(a); i32.sub` | [] → [i32] |
| `not a` | `compile(a); i32.eqz` | [i32] → [i32] |

### 4.6 Let Bindings

```
let x = e1 in e2
```

**컴파일 전략:**
1. `e1`을 컴파일하여 스택에 값 생성
2. `local.set $x`로 로컬 변수에 저장
3. `e2`를 컴파일 (변수 `x` 참조 시 `local.get $x`)

**예시:**
```funlang
let x = 10 in x + 1
```

```wat
(local $x i32)
i32.const 10
local.set $x
local.get $x
i32.const 1
i32.add
```

### 4.7 Variable Reference

| FunLang | WASM Instructions | 설명 |
|---------|-------------------|------|
| `x` | `local.get $idx` | 로컬 변수 참조 |

**환경 관리:**
```fsharp
type CompileEnv = {
    Locals: Map<string, int>   // 변수명 → 로컬 인덱스
    NextLocalIdx: int          // 다음 사용 가능한 인덱스
}
```

### 4.8 If/Then/Else

```
if cond then e1 else e2
```

**컴파일 전략:**
```wat
compile(cond)           ;; 조건을 스택에
(if (result i32)        ;; 결과 타입 선언
  (then
    compile(e1)         ;; then 브랜치
  )
  (else
    compile(e2)         ;; else 브랜치
  )
)
```

**예시:**
```funlang
if x > 0 then x else 0
```

```wat
local.get $x
i32.const 0
i32.gt_s
(if (result i32)
  (then local.get $x)
  (else i32.const 0)
)
```

---

## 5. Binary Format

### 5.1 WASM 모듈 구조

```
┌─────────────────────────────────────────────┐
│  Magic Number: 0x00 0x61 0x73 0x6D (\0asm)  │
│  Version:      0x01 0x00 0x00 0x00 (1)      │
├─────────────────────────────────────────────┤
│  Section 1: Type Section (0x01)             │
│    - 함수 시그니처 정의                       │
├─────────────────────────────────────────────┤
│  Section 3: Function Section (0x03)         │
│    - 함수 → 타입 인덱스 매핑                  │
├─────────────────────────────────────────────┤
│  Section 7: Export Section (0x07)           │
│    - 외부로 노출할 함수                       │
├─────────────────────────────────────────────┤
│  Section 10: Code Section (0x0A)            │
│    - 함수 본문 (로컬 변수 + 명령어)           │
└─────────────────────────────────────────────┘
```

### 5.2 LEB128 인코딩

WASM은 가변 길이 정수 인코딩(LEB128)을 사용합니다.

**Unsigned LEB128:**
```fsharp
let encodeULEB128 (value: int) : byte list =
    let rec loop n acc =
        let byte = n &&& 0x7F
        let remaining = n >>> 7
        if remaining = 0 then
            List.rev (byte :: acc)
        else
            loop remaining ((byte ||| 0x80) :: acc)
    loop value [] |> List.map byte
```

**Signed LEB128 (for i32.const):**
```fsharp
let encodeSLEB128 (value: int) : byte list =
    let rec loop n acc =
        let byte = n &&& 0x7F
        let remaining = n >>> 7
        let signBit = (byte &&& 0x40) <> 0
        if (remaining = 0 && not signBit) || (remaining = -1 && signBit) then
            List.rev (byte :: acc)
        else
            loop remaining ((byte ||| 0x80) :: acc)
    loop value [] |> List.map byte
```

### 5.3 Type Section 인코딩

```
Section ID: 0x01
Section Size: (LEB128)
Num Types: (LEB128)
For each type:
  0x60              // func type marker
  Num Params: (LEB128)
  Param Types: [0x7F = i32, 0x7E = i64, ...]
  Num Results: (LEB128)
  Result Types: [...]
```

**예시 (main: () → i32):**
```
01              ; section id: type
05              ; section size: 5 bytes
01              ; 1 type
60              ; func type
00              ; 0 params
01              ; 1 result
7F              ; result type: i32
```

### 5.4 Function Section 인코딩

```
Section ID: 0x03
Section Size: (LEB128)
Num Functions: (LEB128)
For each function:
  Type Index: (LEB128)
```

### 5.5 Export Section 인코딩

```
Section ID: 0x07
Section Size: (LEB128)
Num Exports: (LEB128)
For each export:
  Name Length: (LEB128)
  Name: (UTF-8 bytes)
  Export Kind: 0x00 (func), 0x01 (table), 0x02 (memory), 0x03 (global)
  Index: (LEB128)
```

### 5.6 Code Section 인코딩

```
Section ID: 0x0A
Section Size: (LEB128)
Num Functions: (LEB128)
For each function:
  Body Size: (LEB128)
  Num Local Decls: (LEB128)
  For each local decl:
    Count: (LEB128)
    Type: (valtype)
  Instructions: [...]
  0x0B            ; end opcode
```

### 5.7 Instruction Opcodes

| Instruction | Opcode | Operands |
|-------------|--------|----------|
| `i32.const` | 0x41 | value (sleb128) |
| `i32.add` | 0x6A | - |
| `i32.sub` | 0x6B | - |
| `i32.mul` | 0x6C | - |
| `i32.div_s` | 0x6D | - |
| `i32.rem_s` | 0x6F | - |
| `i32.eq` | 0x46 | - |
| `i32.ne` | 0x47 | - |
| `i32.lt_s` | 0x48 | - |
| `i32.gt_s` | 0x4A | - |
| `i32.le_s` | 0x4C | - |
| `i32.ge_s` | 0x4E | - |
| `i32.eqz` | 0x45 | - |
| `i32.and` | 0x71 | - |
| `i32.or` | 0x72 | - |
| `local.get` | 0x20 | idx (uleb128) |
| `local.set` | 0x21 | idx (uleb128) |
| `local.tee` | 0x22 | idx (uleb128) |
| `if` | 0x04 | blocktype |
| `else` | 0x05 | - |
| `end` | 0x0B | - |

---

## 6. Limitations (MVP)

### 6.1 지원하는 기능

| 기능 | 상태 | 설명 |
|------|------|------|
| 정수 리터럴 | ✅ | `42`, `-10` |
| 불리언 리터럴 | ✅ | `true`, `false` |
| 산술 연산 | ✅ | `+`, `-`, `*`, `/`, `%` |
| 비교 연산 | ✅ | `<`, `>`, `<=`, `>=`, `=`, `<>` |
| 논리 연산 | ✅ | `and`, `or`, `not` |
| Let 바인딩 | ✅ | `let x = 1 in x + 1` |
| If/then/else | ✅ | `if x > 0 then x else 0` |

### 6.2 지원하지 않는 기능

| 기능 | 이유 | 향후 계획 |
|------|------|----------|
| 함수/람다 | 클로저 → 힙 할당 필요 | Phase 2 |
| 재귀 | 함수 호출 필요 | Phase 2 |
| 리스트 | 메모리 관리 필요 | Phase 3 |
| 튜플 | 메모리 관리 필요 | Phase 3 |
| 문자열 | 메모리 + 데이터 섹션 | Phase 3 |
| 패턴 매칭 | 복잡한 제어 흐름 | Phase 4 |
| 사용자 정의 타입 | GC 또는 RC 필요 | Phase 4 |
| 모듈 시스템 | 다중 함수 지원 필요 | Phase 5 |

### 6.3 에러 처리

MVP에서는 컴파일 타임 에러만 처리합니다:

```fsharp
type WasmCompileError =
    | UnsupportedExpression of Expr * string
    | UnsupportedType of Type
    | UnboundVariable of string * Position
```

런타임 에러 (예: 0으로 나누기)는 WASM 런타임에서 trap으로 처리됩니다.

---

## 7. Future Work

### Phase 2: Functions & Closures

- 최상위 함수 정의
- 함수 호출 (`call` 명령어)
- 재귀 함수
- 단순 클로저 (환경 캡처 없음)

### Phase 3: Memory Management

- Linear memory 사용
- 힙 할당자 (bump allocator)
- 리스트, 튜플 지원
- 문자열 지원 (data section)

### Phase 4: Advanced Features

- 패턴 매칭 (decision tree → branches)
- 사용자 정의 타입 (tagged unions)
- 가비지 컬렉션 (mark-sweep 또는 reference counting)

### Phase 5: Full Language Support

- 모듈 시스템
- 다중 함수 export
- WASI 통합 (파일 I/O, etc.)

---

## References

- [WebAssembly Specification](https://webassembly.github.io/spec/core/)
- [WebAssembly Binary Format](https://webassembly.github.io/spec/core/binary/index.html)
- [WASM Instruction Reference](https://webassembly.github.io/spec/core/appendix/index-instructions.html)
- [LEB128 Encoding](https://en.wikipedia.org/wiki/LEB128)
