# FunLexYacc Refactoring Guide

FunLexYacc 코드베이스를 관용적 FunLang 스타일로 리팩토링하기 위한 가이드.

소스 20개 파일(~5,700줄), 테스트 27개 파일(~4,800줄) 분석 기반.

## 요약: 공통 안티패턴

| 패턴 | 심각도 | 빈도 | 해당 파일 |
|------|--------|------|----------|
| `acc ++ [x]` 수동 재귀 → `List.map` | 높음 | 20+ | DfaMin, LexEmit, Nfa |
| `let _ = expr` 남용 | 높음 | 50+ | Dfa, DfaMin, 테스트 전체 |
| 중첩 `match` Result 피라미드 | 중간 | 15+ | FunlexMain, GrammarParser, Nfa |
| 테스트 헬퍼 중복 정의 | 높음 | 27벌 | 테스트 전체 |
| `List.map` + discard → `List.iter` | 중간 | 10+ | DfaMin, Nfa |
| `while` 수동 인덱싱 → `List.exists`/`List.iter` | 중간 | 15+ | 테스트 (Lalr, Lr0, Tables) |
| `Hashtable.tryGetValue` 반복 패턴 | 낮음 | 10+ | FirstFollow, Ielr |

---

## Tier 1: 높은 우선순위

### 1.1 `acc ++ [x]` 수동 재귀 → `List.map`

가장 흔하고 개선 효과가 큰 패턴. 리스트를 순회하며 변환 결과를 누적하는 재귀를 `List.map`으로 교체.

**DfaMin.fun:314-319**
```
// Before
let rec remapTransRec stateGroup groupToNewId trans acc =
    match trans with
    | [] -> acc
    | ctgt :: rest ->
        let remapped = remapOneTrans stateGroup groupToNewId (fst ctgt) (snd ctgt)
        remapTransRec stateGroup groupToNewId rest (acc ++ [remapped])

// After
let remapTrans stateGroup groupToNewId trans =
    List.map (fun (c, tgt) -> remapOneTrans stateGroup groupToNewId c tgt) trans
```

**LexEmit.fun:48-51**
```
// Before
let rec emitTransRowsRec table acc =
    match table with
    | [] -> acc
    | row :: rest -> emitTransRowsRec rest (acc ++ [emitTransRow row])

// After
let emitTransRows table = List.map emitTransRow table
```

**LexEmit.fun:117-120** (index 필요 → `List.mapi`)
```
// Before
let rec emitCasesRec cases i acc =
    match cases with
    | [] -> acc
    | rc :: rest -> emitCasesRec rest (i + 1) (acc ++ [emitOneCase i rc])

// After
let emitCases cases = List.mapi emitOneCase cases
```

**LexEmit.fun:136-139** (같은 패턴)
```
// After
let emitEntryPoints rules = List.mapi (fun i r -> emitEntryPoint r i) rules
```

**Nfa.fun:345-356**
```
// Before
let rec collectStartIdsRec triples acc =
    match triples with
    | [] -> acc
    | triple :: rest -> collectStartIdsRec rest (acc ++ [caseFragStart triple])

// After
let collectStartIds triples = List.map caseFragStart triples
```

**해당 파일 전체 목록:**
- `DfaMin.fun` — lines 314-319, 392
- `LexEmit.fun` — lines 48-51, 117-120, 136-139
- `Nfa.fun` — lines 345-356, 362

### 1.2 `let _ = expr` → 블록 시퀀싱 또는 `|> ignore`

**Dfa.fun:82-84** — 반환 타입이 다르면 개별 `let _ =` 유지, 같은 타입이면 블록 시퀀싱:
```
// Before
let _ = HashSet.add visited s
let _ = MutableList.add items s
Queue.enqueue wl s

// After: 반환 타입이 다를 수 있으므로 개별 유지 + ignore
HashSet.add visited s |> ignore
MutableList.add items s |> ignore
Queue.enqueue wl s
```

**Dfa.fun:284**
```
// Before
let _ = subsetCreateDfaState nfa ss initNfaSet

// After
subsetCreateDfaState nfa ss initNfaSet |> ignore
```

**FunlexMain.fun:133**
```
// Before
let _ = runWithPaths (fst pathPair) (snd pathPair)
()

// After
runWithPaths (fst pathPair) (snd pathPair) |> ignore
```

**테스트 파일 전체** (50+ 개소) — 같은 타입 반환 시 블록 시퀀싱 가능:
```
// Before (test_cset.fun, test_symtab.fun, etc.)
let _ = intern st "foo"
let _ = intern st "bar"
let _ = intern st "baz"

// After: intern이 동일 타입 반환 시
let _ =
    intern st "foo"
    intern st "bar"
    intern st "baz"

// 타입이 다르면 개별 유지 또는 |> ignore
intern st "foo" |> ignore
intern st "bar" |> ignore
intern st "baz" |> ignore
```

### 1.3 `List.map` + discard → `List.iter`

부수 효과만 필요할 때 `List.map`으로 리스트를 만들고 버리는 패턴.

**DfaMin.fun:122-123**
```
// Before
let _ = List.map (fun ctgt -> addOneTransEntry transByChar s.id ctgt) s.trans
()

// After
List.iter (fun ctgt -> addOneTransEntry transByChar s.id ctgt) s.trans
```

**DfaMin.fun:392**
```
// Before
let _ = List.map (fun ct -> addCharEntryFromPair charMap ct) s.trans
charMap

// After
List.iter (fun ct -> addCharEntryFromPair charMap ct) s.trans
charMap
```

---

## Tier 2: 중간 우선순위

### 2.1 중첩 `match` Result 피라미드 → `resultBind` 체인

**FunlexMain.fun:102-104**
```
// Before
match parseLexSpec "<input>" inputText with
| Err e -> Err e
| Ok spec -> runPipelineWithSpec moduleName spec

// After
parseLexSpec "<input>" inputText
|> bind (fun spec -> runPipelineWithSpec moduleName spec)
```

**Nfa.fun:86-123** (resolveAst — 깊은 중첩)
```
// Before
let rec resolveAst loc resolved ast =
    match ast with
    | Seq (a, b) ->
        match resolveAst loc resolved a with
        | Err e -> Err e
        | Ok ra ->
            match resolveAst loc resolved b with
            | Err e -> Err e
            | Ok rb -> Ok (Seq (ra, rb))
    | Alt (a, b) ->
        match resolveAst loc resolved a with
        | Err e -> Err e
        | Ok ra ->
            match resolveAst loc resolved b with
            | Err e -> Err e
            | Ok rb -> Ok (Alt (ra, rb))

// After
let rec resolveAst loc resolved ast =
    match ast with
    | Seq (a, b) ->
        resolveAst loc resolved a
        |> bind (fun ra ->
            resolveAst loc resolved b
            |> map (fun rb -> Seq (ra, rb)))
    | Alt (a, b) ->
        resolveAst loc resolved a
        |> bind (fun ra ->
            resolveAst loc resolved b
            |> map (fun rb -> Alt (ra, rb)))
```

**GrammarParser.fun:798-824** (parseGrammarSpec — 4단 중첩)
```
// Before
match parseHeader ps with
| Err e -> Err e
| Ok header ->
    match parseDecls ps with
    | Err e -> Err e
    | Ok declState ->
        match consumeSectionSep ps with
        | Err e -> Err e
        | Ok _ ->
            match parseRules ps declState with
            | Err e -> Err e
            | Ok rules -> Ok (buildSpec header declState rules)

// After
parseHeader ps
|> bind (fun header ->
    parseDecls ps
    |> bind (fun declState ->
        consumeSectionSep ps
        |> bind (fun _ ->
            parseRules ps declState
            |> map (fun rules -> buildSpec header declState rules))))
```

**해당 파일:**
- `FunlexMain.fun` — lines 102-104, 129-134
- `Nfa.fun` — lines 86-123
- `GrammarParser.fun` — lines 798-824
- `test_lexparser_regex.fun` — lines 56-65

### 2.2 수동 `assocFind` → `List.tryFind`

**Nfa.fun:79-84**
```
// Before
let rec assocFind name lst =
    match lst with
    | [] -> None
    | (k, v) :: rest ->
        if k = name then Some v
        else assocFind name rest

// After
let assocFind name lst =
    lst
    |> List.tryFind (fun (k, _) -> k = name)
    |> optionMap snd
```

### 2.3 `Hashtable.tryGetValue` 반복 패턴 → 헬퍼

**FirstFollow.fun:74-77** (동일 패턴이 6회 이상 반복)
```
// Before (반복 등장)
let curVal =
    match Hashtable.tryGetValue nullable rule.lhs with
    | (true, v) -> v
    | _ -> false

// After: 헬퍼 정의
let getOr ht key def =
    let (found, v) = Hashtable.tryGetValue ht key
    if found then v else def

// 사용
let curVal = getOr nullable rule.lhs false
```

**해당 파일:** FirstFollow.fun, Ielr.fun, Lalr.fun, ParserTables.fun

### 2.4 테스트 `while` 루프 → `List.exists` / `List.iter`

**test_lr0_dragon.fun:131-140**
```
// Before
let mutable si = 0
let mutable found = false
while si < automaton.states.Count do
    let state = automaton.states.[si]
    let mutable ii = 0
    while ii < state.items.Length do
        let item = state.items.[ii]
        if item.ruleId = 0 && item.dot = 2 then
            found <- true
        ii <- ii + 1
    si <- si + 1

// After
let found =
    automaton.states
    |> List.exists (fun state ->
        state.items
        |> List.exists (fun item -> item.ruleId = 0 && item.dot = 2))
```

**test_tables_precedence.fun:127-134**
```
// Before
let findTermIdx name =
    let mutable idx = -1
    let mutable ti = 0
    while ti < tables.nTerminals do
        if tables.termNames.[ti] = name then
            idx <- ti
        ti <- ti + 1
    idx

// After
let findTermIdx name =
    let rec go i =
        if i >= tables.nTerminals then -1
        else if tables.termNames.[i] = name then i
        else go (i + 1)
    go 0
```

**해당 파일:**
- `test_lr0_dragon.fun` — lines 131-140
- `test_lalr_lookahead.fun` — lines 146-168
- `test_tables_basic.fun` — lines 147-151, 174-181
- `test_tables_precedence.fun` — lines 127-134, 142-158

---

## Tier 3: 낮은 우선순위 / 구조 개선

### 3.1 테스트 헬퍼 모듈 추출

27개 테스트 파일 모두 `check`/`checkEq` 함수를 동일하게 재정의.

```
// TestHelper.fun (새 파일)
module TestHelper =
    let mutable passed = 0
    let mutable failed = 0

    let check (name : string) (ok : bool) =
        if ok then
            passed <- passed + 1
            printfn "PASS: %s" name
        else
            failed <- failed + 1
            printfn "FAIL: %s" name

    let checkEq (name : string) (expected : 'a) (actual : 'a) =
        if actual = expected then
            passed <- passed + 1
            printfn "PASS: %s" name
        else
            failed <- failed + 1
            printfn "FAIL: %s -- expected %s, got %s" name (show expected) (show actual)

    let summary () =
        printfn "Results: %d passed, %d failed" passed failed
        if failed > 0 then failwith (to_string failed ^^ " test(s) failed")
```

각 테스트에서:
```
open "TestHelper.fun"
open TestHelper

// ... 테스트 코드 ...

let _ = summary ()
```

### 3.2 `fst`/`snd` 대신 패턴 매칭

**Nfa.fun:362, FunlexMain.fun:132**
```
// Before
runWithPaths (fst pathPair) (snd pathPair)

// After
let (inputPath, outputPath) = pathPair
runWithPaths inputPath outputPath
```

### 3.3 `failwith` at test end → Result 반환

현재 27개 테스트 모두 실패 시 `failwith` 사용:
```
if failed > 0 then
    failwith (to_string failed ^^ " test(s) failed")
```

이건 테스트 러너 구조상 유지해도 무방. exit code가 필요하면 이 패턴이 적절.

---

## 파일별 리팩토링 상세

### src/common/

| 파일 | 줄 수 | 상태 | 주요 작업 |
|------|-------|------|----------|
| Cset.fun | 166 | 양호 | 거의 수정 불필요 |
| Diagnostics.fun | 57 | 양호 | 수정 불필요 |
| ErrorInfo.fun | 71 | 우수 | 수정 불필요 — `bind`/`map` 정의 파일 |
| Symtab.fun | 56 | 양호 | 파이프 추가 가능 (line 55) |

### src/funlex/

| 파일 | 줄 수 | 상태 | 주요 작업 |
|------|-------|------|----------|
| LexSyntax.fun | 60 | 우수 | 순수 타입 정의, 수정 불필요 |
| Nfa.fun | 382 | 개선필요 | assocFind→List.tryFind, resolveAst bind 체인, collectStartIds→List.map |
| Dfa.fun | 287 | 개선필요 | let _ → 블록 시퀀싱/ignore |
| DfaMin.fun | 491 | 개선필요 | List.map 3건, List.iter 2건, 수동 재귀 3건 |
| LexEmit.fun | 161 | 개선필요 | List.map 3건, List.mapi 2건 |
| LexParser.fun | 920 | 보통 | 뮤터블 파서 상태 — 구조상 while이 적절. 일부 `()` 정리 가능 |
| FunlexMain.fun | 135 | 개선필요 | bind 체인 2건, ignore 1건 |

### src/funyacc/

| 파일 | 줄 수 | 상태 | 주요 작업 |
|------|-------|------|----------|
| GrammarSyntax.fun | 84 | 우수 | 순수 타입 정의, 수정 불필요 |
| FirstFollow.fun | 278 | 보통 | getOr 헬퍼 추출 (6+ 반복) |
| GrammarParser.fun | 825 | 개선필요 | bind 체인 (parseGrammarSpec), while은 파서 특성상 유지 |
| Lr0.fun | ~400 | 보통 | Hashtable 헬퍼 |
| Lalr.fun | ~500 | 보통 | Hashtable 헬퍼 |
| Ielr.fun | 566 | 보통 | 그래프 순회 while은 유지, Hashtable 헬퍼 |
| ParserTables.fun | ~400 | 보통 | Hashtable 헬퍼 |
| YaccEmit.fun | ~300 | 보통 | 일부 파이프 추가 가능 |
| FunyaccMain.fun | 125 | 보통 | stripSuffix 헬퍼 추출 |

### tests/

| 카테고리 | 파일 수 | 주요 작업 |
|----------|---------|----------|
| common/ | 4 | `let _ =` → 블록 시퀀싱, TestHelper 추출 |
| funlex/ | 10 | `let _ =` → 블록 시퀀싱, while → List.exists |
| funyacc/ | 13 | while → List.exists/iter, findTermIdx 리팩토링 |

---

## 리팩토링 순서 권장

### Phase 1: 기계적 변환 (안전, 높은 효과)
1. `acc ++ [x]` 재귀 → `List.map`/`List.mapi` (src 6건)
2. `List.map` + discard → `List.iter` (src 4건)
3. `let _ =` → 블록 시퀀싱 그룹화 (src + tests)
4. `expr |> ignore` 적용 (값 버리기)

### Phase 2: 구조적 개선
5. Result `bind` 체인 적용 (Nfa resolveAst, GrammarParser parseGrammarSpec, FunlexMain)
6. `assocFind` → `List.tryFind` (Nfa)
7. `getOr` Hashtable 헬퍼 추출 (FirstFollow, Ielr, Lalr)

### Phase 3: 테스트 인프라
8. `TestHelper.fun` 모듈 생성
9. 27개 테스트 파일에서 중복 헬퍼 제거
10. while 루프 → List 함수 변환 (tests)

### Phase 4: 코스메틱
11. `fst`/`snd` → 패턴 매칭
12. 불필요한 `()` 정리
13. 파이프 연산자 추가

---

## 수정하지 말 것

다음은 현재 구조가 적절하므로 리팩토링 대상이 아님:

- **LexParser.fun / GrammarParser.fun의 while 루프** — 뮤터블 파서 상태 관리에 적합
- **FirstFollow.fun의 fixed-point while** — 워크리스트 알고리즘 특성
- **Ielr.fun의 그래프 순회** — BFS/DFS에 while + Queue가 자연스러움
- **LexParser.fun의 상호 재귀** (`let rec ... and ...`) — 문법 파싱에 관용적
- **LexSyntax.fun / GrammarSyntax.fun** — 순수 타입 정의, 이미 우수
- **ErrorInfo.fun** — `bind`/`map` 정의, 이미 우수
- **저장/복원 패턴** (LexParser savedPos/savedLine/savedCol) — 파서 lookahead에 필요
