# Phase 8.4: Smart Suggestions Design

## Overview

Add "Did you mean?" suggestions for unbound variable errors using Levenshtein distance.

## Scope

- **In scope:** Unbound variable errors only
- **Out of scope:** Constructor typos, type names, field names (future phases)

## Algorithm

### Levenshtein Distance

Standard dynamic programming implementation:
- Time: O(m × n)
- Space: O(min(m, n)) using single-row optimization
- Threshold: distance ≤ 2

### Suggestion Selection

1. Compute distance from typo to all candidates in scope
2. Filter candidates where distance ≤ 2
3. Sort by distance (ascending), then alphabetically
4. Return at most 3 suggestions

## Output Format

### Single suggestion (most common):

```
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
```

### Multiple suggestions:

```
error[E202]: Unbound variable 'lat'
  --> :1:5
  |
1 | let lat = 10
  |     ^^^
   = help: did you mean `let`?
   = help: other similar: `last`, `map`
```

## File Changes

| File | Change |
|------|--------|
| `src/FunLang/Suggestions.fs` | NEW - Levenshtein algorithm, findSimilar |
| `src/FunLang/Types.fs` | Add Suggestions field to TypeError |
| `src/FunLang/TypeInference.fs` | Call findSimilar on unbound variable |
| `src/FunLang/Diagnostic.fs` | Format suggestions as help messages |
| `src/FunLang/FunLang.fsproj` | Add Suggestions.fs to compile order |
| `tests/FunLang.Tests/SuggestionTests.fs` | NEW - TDD tests |

## Compile Order

```
Errors.fs → Suggestions.fs → Types.fs → ...
```

## Test Cases

### Unit Tests (Levenshtein)

| s1 | s2 | Expected Distance |
|----|----|----|
| "" | "" | 0 |
| "a" | "" | 1 |
| "abc" | "abc" | 0 |
| "kitten" | "sitting" | 3 |
| "print" | "pritn" | 2 |
| "print" | "prnt" | 1 |

### Property Tests

- `levenshteinDistance s s = 0` (identity)
- `levenshteinDistance s1 s2 = levenshteinDistance s2 s1` (symmetry)
- `levenshteinDistance s1 s2 ≤ max(len s1, len s2)` (upper bound)

### Integration Tests

- `prnt 1` with `print` in scope → suggests `print`
- `mpa` with `map`, `max` in scope → suggests both
- `xyz` with `abc` in scope → no suggestions (distance > 2)

## Implementation Order (TDD)

1. RED: Write SuggestionTests.fs with failing tests
2. GREEN: Implement Suggestions.fs
3. RED: Write TypeError suggestion tests
4. GREEN: Add Suggestions field to Types.fs
5. RED: Write TypeInference suggestion tests
6. GREEN: Integrate in TypeInference.fs
7. RED: Write Diagnostic formatting tests
8. GREEN: Update Diagnostic.fs
9. REFACTOR: Clean up

## Date

2026-01-11
