# FunLexYacc -> LangThree Native API Workaround Guide

**Date:** 2026-03-29
**Purpose:** FunLexYacc 소스코드의 .NET interop 호출을 LangThree v7.1 네이티브 API로 전환하는 가이드

## Overview

FunLexYacc는 .NET interop을 통해 `Dictionary`, `HashSet`, `Queue`, `List<T>`, `StringBuilder` 등을 사용한다.
LangThree v7.1에서 dot notation이 제거되었으므로, 모든 .NET 스타일 호출을 module function API로 전환해야 한다.

이 문서는 51개 feature gap 중 **데이터 구조 관련 항목**의 워크어라운드를 정리한다.

---

## 1. Dictionary -> Hashtable

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `Dictionary<K,V>()` | `Hashtable.create ()` | |
| `dict.[key]` | `Hashtable.get ht key` | 없는 키 -> 런타임 에러 |
| `dict.[key] <- value` | `Hashtable.set ht key value` | |
| `dict.TryGetValue(key)` | `Hashtable.tryGetValue ht key` | `(bool, value)` 튜플 반환 |
| `dict.ContainsKey(key)` | `Hashtable.containsKey ht key` | `bool` 반환 |
| `dict.Count` | `Hashtable.count ht` | `int` 반환 |
| `dict.Keys` | `Hashtable.keys ht` | `'k list` 반환 |
| `dict.Remove(key)` | `Hashtable.remove ht key` | |
| `for kvp in dict do kvp.Key, kvp.Value` | `for (k, v) in ht do k, v` | v7.1 tuple destructuring |

**예시 변환:**

```
// Before (.NET interop)
let dict = Dictionary<string, int>()
dict.[key] <- value
match dict.TryGetValue(key) with
| (true, v) -> ...
| _ -> ...
for kvp in dict do
  println kvp.Key

// After (LangThree native)
let ht = Hashtable.create ()
Hashtable.set ht key value
match Hashtable.tryGetValue ht key with
| (true, v) -> ...
| _ -> ...
for (k, v) in ht do
  println k
```

**주의:** `Hashtable.get`은 키가 없으면 런타임 에러 발생. 안전하게 쓰려면 `Hashtable.tryGetValue` 사용.

---

## 2. HashSet -> HashSet

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `HashSet<T>()` | `HashSet.create ()` | |
| `hs.Add(v)` | `HashSet.add hs v` | `bool` 반환 (이미 있으면 false) |
| `hs.Contains(v)` | `HashSet.contains hs v` | `bool` 반환 |
| `hs.Count` | `HashSet.count hs` | `int` 반환 |

**예시 변환:**

```
// Before
let visited = HashSet<int>()
visited.Add(stateId) |> ignore
if visited.Contains(s) then ...

// After
let visited = HashSet.create ()
let _ = HashSet.add visited stateId
if HashSet.contains visited s then ...
```

**주의:** `.Add()` 반환값(`bool`)을 무시하려면 `let _ = HashSet.add ...` 사용.

---

## 3. Queue -> Queue

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `Queue<T>()` | `Queue.create ()` | |
| `q.Enqueue(v)` | `Queue.enqueue q v` | `()` 반환 |
| `q.Dequeue()` | `Queue.dequeue q ()` | 두 번째 인자 unit 필요 |
| `q.Count` | `Queue.count q` | `int` 반환 |

**예시 변환:**

```
// Before
let worklist = Queue<int>()
worklist.Enqueue(startState)
while worklist.Count > 0 do
  let s = worklist.Dequeue()

// After
let worklist = Queue.create ()
Queue.enqueue worklist startState
while Queue.count worklist > 0 do
  let s = Queue.dequeue worklist ()
```

**주의:** `Queue.dequeue`는 두 번째 인자로 `()`가 필요 — `Queue.dequeue q ()`. 빈 큐에서 dequeue하면 "Queue.Dequeue: queue is empty" 에러.

---

## 4. List\<T\> (Mutable) -> MutableList

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `List<T>()` | `MutableList.create ()` | |
| `ml.Add(v)` | `MutableList.add ml v` | |
| `ml.[i]` | `ml.[i]` | 인덱싱 구문 동일 |
| `ml.[i] <- v` | `ml.[i] <- v` | 인덱싱 대입 동일 |
| `ml.Count` | `MutableList.count ml` | `int` 반환 |

**예시 변환:**

```
// Before
let items = List<string>()
items.Add("hello")
let n = items.Count
let first = items.[0]

// After
let items = MutableList.create ()
MutableList.add items "hello"
let n = MutableList.count items
let first = items.[0]
```

**주의:** `ml.[i]`와 `ml.[i] <- v` 인덱싱 구문은 dot notation이 아님 (IndexGet/IndexSet AST). 그대로 사용 가능.

---

## 5. StringBuilder -> StringBuilder

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `StringBuilder()` | `StringBuilder.create ()` | |
| `sb.Append(s)` | `StringBuilder.add sb s` | `append` 아님! (`add`로 이름 변경) |
| `sb.ToString()` | `StringBuilder.toString sb` | |

**예시 변환:**

```
// Before
let sb = StringBuilder()
sb.Append("hello") |> ignore
sb.Append(" world") |> ignore
let result = sb.ToString()

// After
let sb = StringBuilder.create ()
let _ = StringBuilder.add sb "hello"
let _ = StringBuilder.add sb " world"
let result = StringBuilder.toString sb
```

**주의:** `StringBuilder.append`가 아니라 `StringBuilder.add`! (`List.append`와 이름 충돌 방지를 위해 v7.1에서 변경됨)

---

## 6. String Properties/Methods -> String Module

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `s.Length` | `String.length s` | `int` 반환 |
| `s.Contains(needle)` | `String.contains s needle` | `bool` 반환 |
| `s.EndsWith(suffix)` | `String.endsWith s suffix` | `bool` 반환 |
| `s.StartsWith(prefix)` | `String.startsWith s prefix` | `bool` 반환 |
| `s.Trim()` | `String.trim s` | 양쪽 공백 제거 |
| `s.[i..j]` | `s.[i..j]` | 슬라이싱 구문 동일 |

**예시 변환:**

```
// Before
if input.EndsWith(".fsl") then
  let baseName = input.[0 .. input.Length - 5]
  let trimmed = line.Trim()

// After
if String.endsWith input ".fsl" then
  let baseName = input.[0 .. String.length input - 5]
  let trimmed = String.trim line
```

**주의:** 문자열 슬라이싱 `s.[i..j]`는 dot notation이 아님 (StringSliceExpr AST). 그대로 사용 가능.

---

## 7. Array Properties -> Array Module

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `arr.Length` | `Array.length arr` | `int` 반환 |
| `arr.[i]` | `arr.[i]` | 인덱싱 구문 동일 |
| `arr.[i] <- v` | `arr.[i] <- v` | 인덱싱 대입 동일 |
| `Array.sort arr` | `Array.sort arr` | 정렬된 새 배열 반환 |
| `Array.ofSeq coll` | `Array.ofSeq coll` | 컬렉션 -> 배열 |
| `Array.ofList xs` | `Array.ofList xs` | 리스트 -> 배열 |
| `Array.toList arr` | `Array.toList arr` | 배열 -> 리스트 |
| `Array.map f arr` | `Array.map f arr` | |
| `Array.init n f` | `Array.init n f` | |

---

## 8. Char Methods -> Char Module

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `Char.IsDigit(c)` | `Char.IsDigit c` | `bool` 반환 |
| `Char.ToUpper(c)` | `Char.ToUpper c` | |
| `Char.IsLetter(c)` | `Char.IsLetter c` | |
| `Char.IsUpper(c)` | `Char.IsUpper c` | |
| `Char.IsLower(c)` | `Char.IsLower c` | |
| `Char.ToLower(c)` | `Char.ToLower c` | |

---

## 9. KeyValuePair -> Tuple Destructuring

| .NET (FunLexYacc 현재) | LangThree Native | 비고 |
|------------------------|------------------|------|
| `for kvp in dict do kvp.Key` | `for (k, v) in ht do k` | v7.1 tuple destructuring |
| `for kvp in dict do kvp.Value` | `for (k, v) in ht do v` | |

---

## 10. 데이터 구조 외 필요 기능 (별도 구현 필요)

이 항목들은 데이터 구조 워크어라운드가 아니라 **언어 기능** 자체의 구현이 필요한 항목이다.

| Feature | 상태 | 워크어라운드 |
|---------|------|-------------|
| `sprintf "%d" 42` | LangThree 인터프리터 지원 | 그대로 사용 가능 |
| `printfn "%d" x` | LangThree 인터프리터 지원 | 그대로 사용 가능 |
| `open "file.fun"` | LangThree 인터프리터 지원 | 그대로 사용 가능 |
| `get_args ()` | LangThree 인터프리터 지원 | 그대로 사용 가능 |
| `read_file path` | LangThree 인터프리터 지원 | `.NET File.ReadAllText` 대체 |
| `write_file path content` | LangThree 인터프리터 지원 | `.NET File.WriteAllText` 대체 |
| `file_exists path` | LangThree 인터프리터 지원 | `.NET File.Exists` 대체 |
| `eprintfn fmt args` | LangThree 인터프리터 지원 | 그대로 사용 가능 |
| `[for x in coll -> expr]` | LangThree 인터프리터 지원 | 리스트 컴프리헨션 그대로 |
| `List.sort/sortBy/...` | Prelude 지원 | 그대로 사용 가능 |

---

## 11. 변환 체크리스트

FunLexYacc 소스 파일을 전환할 때 다음을 확인:

- [ ] `Dictionary<K,V>()` -> `Hashtable.create ()`
- [ ] `dict.[key]` -> `Hashtable.get ht key`
- [ ] `dict.[key] <- v` -> `Hashtable.set ht key v`
- [ ] `dict.TryGetValue(k)` -> `Hashtable.tryGetValue ht k`
- [ ] `dict.ContainsKey(k)` -> `Hashtable.containsKey ht k`
- [ ] `dict.Count` -> `Hashtable.count ht`
- [ ] `dict.Keys` -> `Hashtable.keys ht`
- [ ] `HashSet<T>()` -> `HashSet.create ()`
- [ ] `hs.Add(v)` -> `HashSet.add hs v`
- [ ] `hs.Contains(v)` -> `HashSet.contains hs v`
- [ ] `hs.Count` -> `HashSet.count hs`
- [ ] `Queue<T>()` -> `Queue.create ()`
- [ ] `q.Enqueue(v)` -> `Queue.enqueue q v`
- [ ] `q.Dequeue()` -> `Queue.dequeue q ()`
- [ ] `q.Count` -> `Queue.count q`
- [ ] `List<T>()` -> `MutableList.create ()`
- [ ] `ml.Add(v)` -> `MutableList.add ml v`
- [ ] `ml.Count` -> `MutableList.count ml`
- [ ] `StringBuilder()` -> `StringBuilder.create ()`
- [ ] `sb.Append(s)` -> `StringBuilder.add sb s` (NOT append!)
- [ ] `sb.ToString()` -> `StringBuilder.toString sb`
- [ ] `s.Length` -> `String.length s`
- [ ] `s.Contains(x)` -> `String.contains s x`
- [ ] `s.EndsWith(x)` -> `String.endsWith s x`
- [ ] `s.StartsWith(x)` -> `String.startsWith s x`
- [ ] `s.Trim()` -> `String.trim s`
- [ ] `arr.Length` -> `Array.length arr`
- [ ] `for kvp in dict do kvp.Key/kvp.Value` -> `for (k, v) in ht do k/v`
- [ ] `.Add() |> ignore` -> `let _ = Module.add ...`

---

*Generated: 2026-03-29 after v7.1 Remove Dot Notation milestone*
