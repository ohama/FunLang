# Phase 102: Fix LambdaAnnot Span Collision - Research

**Researched:** 2026-04-10
**Domain:** FunLang Parser / Type Annotation Map / AST span identity
**Confidence:** HIGH

## Summary

Issue #18는 `desugarAnnotParams`(및 `desugarMixedParams`)가 중첩 LambdaAnnot 노드를 생성할 때 모든 노드에 동일한 `span`을 부여하는 버그다. `Bidir.synth`는 각 `LambdaAnnot` 노드를 방문할 때 `recordTy span finalTy`를 호출하므로, 같은 span을 가진 노드들이 순서대로 처리되면 `annotationMap[span]`은 마지막으로 기록된 타입(가장 바깥쪽 arrow 타입)으로 덮어써진다. 외부 소비자인 `FunLangCompiler`의 `isPtrParamTyped`는 `lambdaSpan`으로 annotationMap을 조회해 파라미터 타입을 판단하므로, 내부 LambdaAnnot들이 항상 outermost 타입을 받아 잘못된 Ptr/I64 선택이 일어난다.

Fix A(권장)는 각 `AnnotParam` / `MixedParam` 규칙이 자신의 위치 span을 함께 반환하도록 타입을 변경하고, `desugarAnnotParams` / `desugarMixedParams`가 그 개별 span을 각 LambdaAnnot 노드에 할당하는 방식이다. 이렇게 하면 annotationMap 조회가 파라미터별로 정확한 arrow 타입을 반환한다.

`desugarMixedParams`도 동일한 span 전파 방식을 사용하므로 같이 수정해야 한다.

**Primary recommendation:** `AnnotParam`과 `MixedParam` 문법 규칙이 `ruleSpan parseState 1 5`를 포함한 `(string * TypeExpr * Span)` / `Choice<string, string * TypeExpr * Span>` 튜플을 반환하도록 변경하고, desugar 함수들이 외부에서 전달받은 전체 span 대신 파라미터 자체 span을 사용하게 한다.

## Standard Stack

이 phase는 외부 라이브러리가 없다. 순수 FunLang 내부 코드 수정이다.

### Core
| 파일 | 역할 | 수정 여부 |
|------|------|----------|
| `src/FunLang/Parser.fsy` | `desugarAnnotParams`, `desugarMixedParams`, `AnnotParam`, `MixedParam` 규칙 정의 | 수정 필요 |
| `src/FunLang/Bidir.fs` | `LambdaAnnot` synth handler — `recordTy span finalTy` 호출 | 수정 불필요 (span만 고치면 됨) |
| `src/FunLang/TypeAnnotationMap.fs` | `record`/`tryFind` 구현 — `map.[span] <- ty` (덮어쓰기 방식) | 수정 불필요 |
| `tests/FunLang.Tests/TypeAnnotationTests.fs` | annotationMap 단위 테스트 | 새 테스트 추가 |
| `tests/flt/` | 통합 flt 테스트 | 새 regression 테스트 추가 |

## Architecture Patterns

### 문제 재현 경로

```
Parser.fsy: FUN AnnotParamList ARROW SeqExpr
  → desugarAnnotParams [(ps, TEName "ParserState"); (s, TEString); (i, TEInt)] body outerSpan
  → LambdaAnnot("ps", TEName "ParserState",
       LambdaAnnot("s", TEString,
         LambdaAnnot("i", TEInt, body, outerSpan),  ← span collision
       outerSpan),                                   ← span collision
     outerSpan)                                      ← outermost

Bidir.synth (LambdaAnnot):
  recordTy outerSpan (TArrow(TName "ParserState", TArrow(TString, TArrow(TInt, bodyTy))))
  recordTy outerSpan (TArrow(TString, TArrow(TInt, bodyTy)))   ← overwrites previous
  recordTy outerSpan (TArrow(TInt, bodyTy))                    ← overwrites previous

결과: annotationMap[outerSpan] = TArrow(TInt, bodyTy)  (가장 마지막 = 가장 내부)
```

> 주의: `ConcurrentDictionary`의 `map.[span] <- ty`는 마지막 기록이 우선이다. Bidir이 AST를 bottom-up이 아닌 top-down synth로 처리하면 가장 외부가 마지막일 수 있다. 실제 traversal 순서는 `synth`가 재귀적으로 `LambdaAnnot` 안의 body를 처리한 후 자신을 recordTy하므로 **안쪽 먼저 → 바깥쪽 나중** 순서 = 바깥쪽이 덮어씀.

### Pattern 1: AnnotParam에 Span 추가 (Fix A)

**변경 전:**
```fsharp
// Parser.fsy 헤더부
let rec desugarAnnotParams (paramList: (string * TypeExpr) list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> failwith "desugarAnnotParams: empty param list"
    | [(name, ty)] -> LambdaAnnot(name, ty, body, span)
    | (name, ty) :: rest -> LambdaAnnot(name, ty, desugarAnnotParams rest body span, span)

// 문법 규칙
AnnotParam:
    | LPAREN IDENT COLON TypeExpr RPAREN    { ($2, $4) }
```

**변경 후:**
```fsharp
// 각 파라미터가 자신의 span을 보유
let rec desugarAnnotParams (paramList: (string * TypeExpr * Span) list) (body: Expr) : Expr =
    match paramList with
    | [] -> failwith "desugarAnnotParams: empty param list"
    | [(name, ty, span)] -> LambdaAnnot(name, ty, body, span)
    | (name, ty, span) :: rest -> LambdaAnnot(name, ty, desugarAnnotParams rest body, span)

// 문법 규칙 — ruleSpan parseState 1 5 = LPAREN..RPAREN span
AnnotParam:
    | LPAREN IDENT COLON TypeExpr RPAREN    { ($2, $4, ruleSpan parseState 1 5) }
```

호출부 변경:
```fsharp
// FUN AnnotParamList ARROW SeqExpr
{ desugarAnnotParams $2 $4 }   // span 파라미터 제거
```

### Pattern 2: MixedParam에 Span 추가

`desugarMixedParams`도 동일 패턴. `MixedParam`의 annotated branch가 span 없이 `Choice2Of2(name, ty)`를 반환하는데, 이를 `Choice2Of2(name, ty, span)`으로 확장해야 한다.

```fsharp
// 변경 전
let rec desugarMixedParams (paramList: Choice<string, string * TypeExpr> list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> body
    | Choice1Of2 name :: rest -> Lambda(name, desugarMixedParams rest body span, span)
    | Choice2Of2 (name, ty) :: rest -> LambdaAnnot(name, ty, desugarMixedParams rest body span, span)

// 변경 후
let rec desugarMixedParams (paramList: Choice<string * Span, string * TypeExpr * Span> list) (body: Expr) : Expr =
    match paramList with
    | [] -> body
    | Choice1Of2 (name, span) :: rest -> Lambda(name, desugarMixedParams rest body, span)
    | Choice2Of2 (name, ty, span) :: rest -> LambdaAnnot(name, ty, desugarMixedParams rest body, span)

// 문법 규칙
MixedParam:
    | IDENT                                     { Choice1Of2 ($1, symSpan parseState 1) }
    | LPAREN IDENT COLON TypeExpr RPAREN        { Choice2Of2 ($2, $4, ruleSpan parseState 1 5) }
```

> `desugarMixedParams` 호출부는 매우 많다(30개 이상). `span` 파라미터를 제거하는 방향이 간단하지만, 호출부를 모두 수정해야 한다.

### Anti-Patterns to Avoid

- **외부 span을 fallback으로 전달하는 방식:** `desugarAnnotParams`의 시그니처를 `(paramList * body * fallbackSpan)` 형태로 유지하면서 파라미터 span이 없을 때 fallback을 쓰는 방식 — 타입 변경이 없어 보이기 쉬우나, AnnotParam이 span을 반환하지 않으면 호출 시점에서 개별 span에 접근할 방법이 없다.
- **annotationMap 조회 방식 변경(Fix B):** TypeAnnotationMap.record를 "최초 기록이 우선(skip if exists)" 방식으로 변경하면 outermost가 유지되나, `FunLangCompiler`의 `isPtrParamTyped`는 `lambdaSpan`으로 각 LambdaAnnot의 정확한 타입을 찾으려 하므로 근본 해결이 아니다.

## Don't Hand-Roll

| 문제 | 하지 말 것 | 올바른 방법 |
|------|-----------|------------|
| 고유 span 생성 | 카운터 기반 가상 span 생성(`{unknownSpan with StartColumn = idx}`) | 파서의 `ruleSpan parseState 1 5` — 실제 소스 위치 사용 |
| annotationMap 충돌 해결 | Map을 `Map<Span * int, Type>`으로 바꿔 인덱스 추가 | span 자체를 고유하게 만드는 것이 맞음 |

## Common Pitfalls

### Pitfall 1: desugarMixedParams 호출부 누락
**What goes wrong:** `desugarMixedParams`의 시그니처에서 `span` 파라미터를 제거하면 30개 이상의 호출부를 모두 수정해야 한다. 하나라도 빠뜨리면 컴파일 오류.
**Why it happens:** `desugarMixedParams`는 Let/LetRec 선언 모두에서 사용되므로 Decls 섹션과 SeqExpr 섹션 양쪽에 분산되어 있다.
**How to avoid:** `dotnet build` 컴파일 오류를 기준으로 모든 호출부를 추적한다. grep으로 `desugarMixedParams`를 검색하면 전체 목록 파악 가능.
**Warning signs:** 컴파일 오류 "too many arguments" 또는 "type mismatch".

### Pitfall 2: LetRec 첫 번째 파라미터 분리 패턴
**What goes wrong:** `LetRec` 바인딩은 첫 번째 파라미터를 `(name, firstParam, typeOpt, body, span)` 튜플로 분리해 저장한다. `desugarMixedParams` 결과에서 첫 Lambda/LambdaAnnot을 unwrap하는 패턴 매칭이 있는데, span 변경 후에도 이 unwrap 패턴이 올바른 span을 사용하는지 확인 필요.
**How to avoid:** LetRec 관련 파서 규칙(`LetRecDeclaration`, let rec 표현식)에서 `desugarMixedParams` 결과를 패턴 매칭하는 부분 각각 검토.

### Pitfall 3: 단일 파라미터 경우와 다중 파라미터 경우의 분기
**What goes wrong:** `FUN AnnotParamList ARROW SeqExpr` 규칙은 2개 이상의 파라미터에만 적용된다(단일은 별도 규칙). 단일 파라미터 LambdaAnnot 노드는 이미 올바른 span을 가지고 있으므로 수정 불필요. 수정 범위를 다중 파라미터 경로에만 한정.

### Pitfall 4: desugarMultiParamLambda도 같은 span 문제
**What goes wrong:** `desugarMultiParamLambda`도 모든 Lambda 노드에 같은 span 전달. 그러나 Lambda(unannotated)의 경우 `isPtrParamTyped`는 `isPtrParamBody` 휴리스틱으로 fallback하므로 annotationMap 충돌이 실제 버그를 유발하지 않는다. Phase 102 범위 밖 — 별도 이슈로 처리.

### Pitfall 5: FunLangCompiler 사이드 영향
**What goes wrong:** `isPtrParamTyped`는 `FunLangCompiler`에 있으며, FunLang의 `ExportApi.typeCheckFile`이 반환하는 `AnnotationMap`을 사용한다. span이 변경되면 이 map의 키가 달라지므로, FunLangCompiler가 올바른 새 span으로 조회하는지 확인이 필요하다.
**How to avoid:** FunLangCompiler는 `Ast.spanOf(LambdaAnnot node)`를 사용해 span을 얻는다. AST 노드가 올바른 span을 가지면 `spanOf` 결과도 올바르므로, FunLangCompiler 측 코드 변경은 불필요하다.

## Code Examples

### 현재 desugarAnnotParams (버그 있음)
```fsharp
// Parser.fsy, 라인 15-19
let rec desugarAnnotParams (paramList: (string * TypeExpr) list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> failwith "desugarAnnotParams: empty param list"
    | [(name, ty)] -> LambdaAnnot(name, ty, body, span)
    | (name, ty) :: rest -> LambdaAnnot(name, ty, desugarAnnotParams rest body span, span)
// 문제: 모든 LambdaAnnot가 동일한 outerSpan 사용
```

### Fix A 적용 후 desugarAnnotParams
```fsharp
// 각 파라미터가 자신의 위치 span 보유
let rec desugarAnnotParams (paramList: (string * TypeExpr * Span) list) (body: Expr) : Expr =
    match paramList with
    | [] -> failwith "desugarAnnotParams: empty param list"
    | [(name, ty, span)] -> LambdaAnnot(name, ty, body, span)
    | (name, ty, span) :: rest -> LambdaAnnot(name, ty, desugarAnnotParams rest body, span)
```

### AnnotParam 문법 규칙 수정
```yacc
AnnotParam:
    | LPAREN IDENT COLON TypeExpr RPAREN    { ($2, $4, ruleSpan parseState 1 5) }
```

### AnnotParamList 호출부 수정
```yacc
| FUN AnnotParamList ARROW SeqExpr
    { desugarAnnotParams $2 $4 }    // span 파라미터 제거
| FUN AnnotParamList ARROW INDENT SeqExpr DEDENT
    { desugarAnnotParams $2 $5 }    // span 파라미터 제거
```

### Fix A 적용 후 desugarMixedParams
```fsharp
let rec desugarMixedParams (paramList: Choice<string * Span, string * TypeExpr * Span> list) (body: Expr) : Expr =
    match paramList with
    | [] -> body
    | Choice1Of2 (name, span) :: rest -> Lambda(name, desugarMixedParams rest body, span)
    | Choice2Of2 (name, ty, span) :: rest -> LambdaAnnot(name, ty, desugarMixedParams rest body, span)
```

### MixedParam 문법 규칙 수정
```yacc
MixedParam:
    | IDENT                                     { Choice1Of2 ($1, symSpan parseState 1) }
    | LPAREN IDENT COLON TypeExpr RPAREN        { Choice2Of2 ($2, $4, ruleSpan parseState 1 5) }
```

### Bidir.synth LambdaAnnot 핸들러 (변경 불필요)
```fsharp
// src/FunLang/Bidir.fs, 라인 326-334
| LambdaAnnot (param, paramTyExpr, body, span) ->
    let paramTy = elaborateTypeExpr paramTyExpr
    let ctx' = InCheckMode (paramTy, "annotation", span) :: ctx
    let bodyEnv = Map.add param (Scheme ([], [], paramTy)) env
    let s, bodyTy = synth ctorEnv recEnv ctx' bodyEnv body
    let finalTy = TArrow (apply s paramTy, bodyTy)
    recordTy span finalTy    // ← span이 각 LambdaAnnot마다 고유하면 충돌 없음
    (s, finalTy)
```

### 새 회귀 테스트 (flt)
```
// tests/flt/file/let/let-annot-multi-param-types.flt
// Test: multi-param annotated function — each param gets correct type
// --- Command: src/FunLang/bin/Release/net10.0/fn %input
// --- Input:
let rec f (ps : string) (s : string) (i : int) = i
let _ = println (to_string (f "a" "b" 42))
// --- Stdout:
42
```

### 새 단위 테스트 (TypeAnnotationTests)
```fsharp
// TA-08: Multi-param LambdaAnnot — each LambdaAnnot gets distinct span and correct type
test "TA-08: multi-param LambdaAnnot has distinct spans" {
    let input = "let f = fun (x: int) (y: string) -> x"
    let annots = typeCheckAndSnapshot input
    // Should have TWO distinct TArrow entries:
    // outer: TArrow(TInt, TArrow(TString, TInt))
    // inner: TArrow(TString, TInt)
    let arrowTypes =
        annots
        |> Map.toSeq
        |> Seq.choose (fun (_, ty) -> match ty with Type.TArrow _ -> Some ty | _ -> None)
        |> Seq.distinct
        |> Seq.length
    Expect.isGreaterThan arrowTypes 1 "Should have at least 2 distinct TArrow types for 2-param LambdaAnnot"
}
```

## State of the Art

| 이전 방식 | 현재 상태 | 변경 시점 | 영향 |
|----------|----------|----------|------|
| AnnotParam → (string * TypeExpr) | (string * TypeExpr) — span 없음 | Phase 102에서 수정 예정 | 다중 파라미터 함수에서 span collision 발생 |
| MixedParam → Choice 타입 | span 없는 Choice — 동일 문제 | Phase 102에서 수정 예정 | annotated param 사용 시 collision |

## Open Questions

1. **desugarMultiParamLambda도 수정해야 하는가?**
   - What we know: `Lambda`(unannotated) 노드는 `isPtrParamTyped`가 annotationMap miss 시 `isPtrParamBody` 휴리스틱으로 fallback하므로 현재 버그를 유발하지 않는다.
   - What's unclear: 미래에 unannotated Lambda도 annotationMap 의존 로직이 추가될 경우 문제가 될 수 있다.
   - Recommendation: Phase 102에서는 `desugarAnnotParams`와 `desugarMixedParams`만 수정. `desugarMultiParamLambda`는 별도 이슈로 추적.

2. **FunLangCompiler 업데이트 필요 여부**
   - What we know: `isPtrParamTyped(annotationMap, lambdaSpan, paramName, bodyExpr)`에서 `lambdaSpan`은 `Ast.spanOf(LambdaAnnot node)`로 계산된다. AST 노드가 올바른 span을 보유하면 자동으로 해결된다.
   - What's unclear: FunLangCompiler가 FunLang의 어느 버전을 참조하는지(로컬 submodule? NuGet?).
   - Recommendation: FunLangCompiler 코드 변경 없이 FunLang 수정만으로 해결 가능. FunLangCompiler가 이미 `spanOf` 기반으로 조회하므로.

## Sources

### Primary (HIGH confidence)
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Parser.fsy` — `desugarAnnotParams`, `desugarMixedParams`, `AnnotParam`, `MixedParam` 직접 코드 읽음
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Bidir.fs` — `LambdaAnnot` synth 핸들러, `annotationMap` 기록 방식 확인
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/TypeAnnotationMap.fs` — `record` 함수가 `map.[span] <- ty` 덮어쓰기 방식임 확인
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Ast.fs` — `Span` 타입 구조, `unknownSpan`, `mkSpan` 확인
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/ElabHelpers.fs` — `isPtrParamTyped` 구현 확인 (라인 643-647)

### Secondary (MEDIUM confidence)
- `/Users/ohama/vibe-coding/FunLang/tests/FunLang.Tests/TypeAnnotationTests.fs` — 기존 annotationMap 테스트 구조 파악
- `/Users/ohama/vibe-coding/FunLang/tests/flt/file/let/let-annot-param-mixed.flt` — 기존 다중 파라미터 테스트 baseline 확인

## Metadata

**Confidence breakdown:**
- 버그 원인 분석: HIGH — 코드 직접 추적으로 확인
- Fix A 구현 방향: HIGH — 파서의 `ruleSpan`/`symSpan` API로 개별 span 획득 가능함 확인
- desugarMixedParams 호출부 수: MEDIUM — grep으로 30개 이상 파악했으나 LetRecDecl 포함 정확한 수는 빌드 시 확인
- FunLangCompiler 측 영향: HIGH — `spanOf` 기반 조회이므로 FunLang 수정만으로 해결

**Research date:** 2026-04-10
**Valid until:** 안정적 코드베이스이므로 30일
