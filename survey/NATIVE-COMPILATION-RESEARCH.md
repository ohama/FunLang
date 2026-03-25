# LangThree Native Compilation Research

> LangThree 인터프리터에 gcc/clang 기반 네이티브 컴파일 백엔드를 추가하는 것에 대한 타당성 조사

## 1. 현재 아키텍처

```
Source (.fun) → Lexer → IndentFilter → Parser → AST → TypeCheck → Eval (tree-walking)
```

- **구현 언어**: F# (.NET 10), ~10,600 LOC
- **실행 방식**: AST 직접 순회 (tree-walking), 바이트코드 없음
- **기존 최적화**: 패턴매칭 decision tree 컴파일, trampoline TCO
- **타입 시스템**: Hindley-Milner + let-polymorphism + GADT

---

## 2. 컴파일 전략 비교

### Option A: LangThree → C → gcc/clang

| 항목 | 설명 |
|------|------|
| **핵심 아이디어** | AST (또는 새 IR)에서 C 소스코드를 생성, gcc/clang으로 컴파일 |
| **선례** | Chicken Scheme, Gambit Scheme, OCaml (C 중간 출력), Haskell (GHC C backend) |
| **장점** | gcc/clang의 성숙한 최적화 활용, 크로스플랫폼, 디버깅 용이 |
| **단점** | C의 제약 (TCO 보장 없음, GC 없음, 클로저 직접 지원 없음) |

### Option B: LangThree → LLVM IR → binary

| 항목 | 설명 |
|------|------|
| **핵심 아이디어** | LLVM IR을 직접 생성, LLVM이 최적화 + 네이티브 코드 생성 |
| **선례** | Rust, Swift, Julia, Haskell (GHC LLVM backend) |
| **장점** | `musttail`로 TCO 보장, 더 정밀한 최적화, 레지스터 할당 우수 |
| **단점** | LLVM 의존성 크고 복잡, F#에서 LLVM 바인딩 부족 |

### Option C: 바이트코드 VM (중간 단계)

| 항목 | 설명 |
|------|------|
| **핵심 아이디어** | 커스텀 바이트코드로 컴파일 → 스택 머신에서 실행 |
| **선례** | Lua, Python, Erlang BEAM |
| **장점** | 구현 난이도 낮음, 단계적 접근 가능 |
| **단점** | 네이티브만큼 빠르지 않음 |

---

## 3. C 코드 생성 시 해결해야 할 과제

### 3.1. 클로저 (Closures)

```
// LangThree
let adder x = fun y -> x + y
```

```c
// 생성될 C 코드
typedef struct { int x; } adder_env_t;

Value adder_inner(void* env, Value y) {
    adder_env_t* e = (adder_env_t*)env;
    return int_value(e->x + as_int(y));
}

Value adder(Value x) {
    adder_env_t* env = gc_alloc(sizeof(adder_env_t));
    env->x = as_int(x);
    return closure_value(adder_inner, env);
}
```

**필요 작업**: Closure conversion pass — 자유 변수를 명시적 환경 구조체로 변환

### 3.2. ADT (Algebraic Data Types)

```
// LangThree
type Shape = Circle of int | Rect of int * int
```

```c
// C tagged union
typedef struct {
    int tag;  // 0=Circle, 1=Rect
    union {
        int circle_radius;
        struct { int w; int h; } rect;
    };
} Shape;
```

비교적 직관적. 패턴매칭 decision tree가 이미 있어서 C의 `switch`/`if`로 직접 변환 가능.

### 3.3. 가비지 컬렉션

C에는 GC가 없으므로 별도 전략이 필요:

| 방식 | 설명 | 적합성 |
|------|------|--------|
| **Boehm GC** | 보수적 GC, 라이브러리 링크만으로 사용 가능 | **권장** |
| **Reference counting** | 순환 참조 문제 | 함수형 언어에 부적합 |
| **Arena/Region** | 제한적이지만 특정 패턴에 매우 빠름 | 보조적 활용 가능 |

**권장**: Boehm GC (`GC_malloc` 사용). Chicken Scheme, OCaml C backend 등이 이 방식 사용.

### 3.4. Tail Call Optimization

C 컴파일러는 TCO를 보장하지 않음. 해결책:

| 방식 | 설명 |
|------|------|
| **Trampoline** | 현재 인터프리터와 동일 방식. 안전하지만 간접호출 오버헤드 |
| **Cheney on the MTA** | Chicken Scheme 방식. 스택을 힙처럼 사용, `longjmp`로 리셋 |
| **`musttail` attribute** | GCC 14+, Clang 지원. 컴파일러가 TCO 보장 |
| **goto 기반 CPS** | 모든 함수를 하나의 거대한 함수 + `goto`로 변환 |

### 3.5. 다형성 (Polymorphism)

| 전략 | 설명 | 트레이드오프 |
|------|------|------------|
| **Boxing (uniform representation)** | 모든 값을 `Value*`로 통일 | 구현 간단, unboxing 오버헤드 |
| **Monomorphization** | 타입별 특화 코드 생성 | 빠르지만 코드 크기 증가, 재귀적 타입에 제한 |

**권장**: Boxing 기본 + 핫패스에 monomorphization (MLton 방식)

### 3.6. 예외 처리

```c
#include <setjmp.h>
jmp_buf exception_handler;
// try-with → setjmp/longjmp
```

---

## 4. 예상 속도 향상

### 4.1. Tree-walking 인터프리터의 병목

| 병목 요인 | 설명 | CPU 시간 비율 |
|-----------|------|--------------|
| **AST 노드 디스패치** | 매 실행 단계마다 DU match | ~30-40% |
| **환경 룩업** | `Map<string, Value>` 탐색 | ~20-30% |
| **값 박싱/언박싱** | 모든 값이 DU wrapping | ~15-20% |
| **메모리 할당** | 중간값, 클로저, 리스트 노드 | ~10-20% |

### 4.2. 전환 경로별 속도 향상 (학술 문헌 + 유사 프로젝트 기반)

| 전환 경로 | 산술/루프 | 클로저/HOF | 리스트/ADT | 전체 평균 |
|-----------|----------|-----------|-----------|----------|
| Tree-walking → **바이트코드 VM** | 5-15x | 3-8x | 3-10x | **5-10x** |
| Tree-walking → **C (boxed)** | 20-50x | 10-30x | 8-20x | **10-30x** |
| Tree-walking → **C (monomorphized)** | 50-200x | 15-40x | 10-30x | **20-50x** |
| Tree-walking → **LLVM** | 80-300x | 20-50x | 15-40x | **30-100x** |

### 4.3. 참고 사례

| 프로젝트 | 방식 | 인터프리터 대비 속도 향상 |
|----------|------|------------------------|
| **Chicken Scheme** | Scheme → C → gcc | ~20-50x |
| **MLton** | SML → C | ~50-100x (GHC급) |
| **Gambit Scheme** | Scheme → C | ~10-30x |

### 4.4. LangThree 특화 예상

함수형 언어 특성 (클로저, ADT, 리스트 처리)을 고려한 워크로드별 예상:

| 워크로드 | 예상 속도 향상 |
|----------|---------------|
| 피보나치 재귀 | 30-80x |
| 리스트 map/filter 체인 | 10-25x |
| 패턴매칭 집중 코드 | 15-40x |
| 문자열 처리 | 5-15x |
| 파일 I/O 집중 | 2-5x (I/O bound) |
| **종합 평균** | **15-40x** |

---

## 5. 구현 로드맵 (권장)

단계적 접근을 권장:

### Phase 1: IR 도입 (필수 선행)

```
AST → [새로운 중간 표현] → Eval (기존 인터프리터)
                          → CCodeGen (새 백엔드)
```

- ANF (A-Normal Form) 또는 CPS 변환
- Closure conversion
- Lambda lifting
- 이 IR은 바이트코드 VM에도 재활용 가능

### Phase 2: C 런타임 라이브러리

- `Value` tagged union 정의
- Boehm GC 통합
- 기본 연산 (`list_cons`, `list_head`, `string_concat` 등)
- Exception handling (`setjmp`/`longjmp`)
- Prelude 함수들의 C 구현

### Phase 3: C 코드 생성기

- IR → C 소스 변환
- 클로저를 구조체 + 함수 포인터로
- Decision tree → switch/if 체인
- TCO → trampoline 또는 `musttail`

### Phase 4: 빌드 파이프라인

```bash
langthree compile script.fun              # .fun → .c 생성
langthree compile -o script script.fun    # .fun → .c → gcc → binary
```

### 예상 작업량

| Phase | 예상 규모 | 난이도 |
|-------|----------|--------|
| Phase 1: IR | ~1,500-2,500 LOC | 높음 (컴파일러 이론 필요) |
| Phase 2: C 런타임 | ~800-1,500 LOC (C) | 중간 |
| Phase 3: 코드 생성 | ~1,500-3,000 LOC | 높음 |
| Phase 4: 빌드 통합 | ~200-400 LOC | 낮음 |
| **합계** | **~4,000-7,400 LOC** | — |

---

## 6. 대안: 더 쉬운 속도 개선

네이티브 컴파일 전에 더 적은 노력으로 의미 있는 속도 향상을 얻을 수 있는 방법들:

| 방법 | 예상 속도 향상 | 난이도 | 비고 |
|------|---------------|--------|------|
| **바이트코드 VM** | 5-10x | 중간 (~2,000 LOC) | effort/reward 최적 |
| **환경을 배열 기반으로** (De Bruijn index) | 2-3x | 낮음 | Map → 배열 인덱스 |
| **상수 폴딩 + 인라이닝** | 1.5-2x | 낮음 | AST 최적화 패스 |
| **NativeAOT 컴파일** (.NET 자체) | 1.2-1.5x (시작 시간) | 매우 낮음 | 인터프리터 자체를 AOT |

---

## 7. 결론

| 질문 | 답변 |
|------|------|
| **가능한가?** | 가능. Scheme/SML 계열에서 검증된 기법들이 LangThree에 적용 가능 |
| **얼마나 빨라지나?** | **15-40x** (C boxed), 핫패스 monomorphization 시 최대 100x |
| **노력 대비 가치?** | 대규모 프로젝트. 바이트코드 VM을 먼저 구현하면 5-10x를 훨씬 적은 노력으로 얻을 수 있음 |
| **권장 경로** | IR 도입 → 바이트코드 VM → C backend 순서로 단계적 접근 |

> **핵심 판단**: 바이트코드 VM이 effort/reward 비율이 가장 좋다. 이미 decision tree 컴파일이 있으므로, 나머지 AST도 바이트코드로 컴파일하는 것이 자연스러운 다음 단계이다. 이후 C backend는 바이트코드 IR을 재활용하면 된다.
