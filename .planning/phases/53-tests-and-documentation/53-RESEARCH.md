# Phase 53: Tests and Documentation - Research

**Researched:** 2026-03-29
**Domain:** flt integration testing + LangThree tutorial authoring
**Confidence:** HIGH

## Summary

Phase 53 completes v6.0 by adding missing test coverage and writing tutorial chapter 22. All three v6.0 features (newline implicit sequencing, for-in loops, Option/Result utilities) were implemented in Phases 50–52, and each phase already deposited flt tests as part of its own execution. The job of Phase 53 is to audit what was actually created, identify any gaps against the success criteria, and write the tutorial chapter.

The audit reveals: FORIN tests (TST-34) and Option/Result tests (TST-35) are fully covered by tests created in Phases 51 and 52. The NLSEQ tests (TST-33) are partially covered — 5 positive tests exist but two specific regression cases ("multi-line application not split" and "structural terminators not preceded by SEMICOLON") have no dedicated flt files. TST-36 (tutorial chapter 22) requires creating a new file `tutorial/22-practical-programming.md` and updating `tutorial/SUMMARY.md`.

**Primary recommendation:** Phase 53 is a two-task phase: (1) add 2 missing regression flt tests for newline sequencing, (2) write tutorial/22-practical-programming.md and update SUMMARY.md.

## Standard Stack

Phase 53 uses only tools and conventions already established in the codebase.

### Core
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| FsLit (flt runner) | current | Execute integration tests against LangThree binary | All integration testing uses this runner |
| LangThree binary | Release build | Execute .l3 programs for tutorial examples | Tests run against compiled binary |

### flt File Format
Every flt test follows this exact format (no library needed — it's a plain text convention):
```
// [description comment]
// --- Command: /absolute/path/to/LangThree %input
// --- Input:
[LangThree source code]
// --- Output:
[expected stdout]
```

For error tests:
```
// --- Error:
E0XXX
```

The binary path is always: `/Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree`

### Tutorial Format
Tutorial chapters are Markdown files in `tutorial/`. Conventions from existing chapters:
- Title: `# NN장: 한국어 제목 (English Subtitle)`
- Code blocks: ` ``` ` fenced blocks with `$ cat file.l3` then `$ langthree file.l3` then output
- File suffix: `.l3` in examples (not `.fun`, not `.langthree`)
- Language: Korean prose, English code/identifiers
- No syntax highlighting marker on fenced code blocks (just triple backtick, no language tag)

### Supporting
| File | Purpose | Notes |
|------|---------|-------|
| `tutorial/SUMMARY.md` | mdBook table of contents | Add chapter 22 entry in "실용 프로그래밍" section |
| `tests/flt/expr/seq/` | Home for NLSEQ regression tests | All existing nlseq tests live here |

## Architecture Patterns

### flt Test Directory Structure
```
tests/flt/
├── expr/
│   ├── seq/          # newline sequencing tests (all nlseq-*.flt here)
│   └── loop/         # for-loop tests (all loop-for-*.flt here)
└── file/
    └── prelude/      # prelude library tests (option/result tests here)
```

### Pattern 1: Regression flt Test
**What:** Tests that a feature does NOT produce spurious output when near other features
**When to use:** For NLSEQ regression cases — structural terminators and multi-line application
**Example:**
```
// Test NLSEQ-06: else-branch at same indent is not preceded by spurious SEMICOLON
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let x = 3
let result =
    if x > 2 then
        println "big"
        42
    else
        0
// --- Output:
big
42
```

### Pattern 2: Tutorial Chapter Structure
All tutorial chapters follow this outline pattern:
1. Title with chapter number and Korean/English name
2. Opening paragraph explaining why this topic matters
3. Sections with `## Section Name` headers
4. Code examples in ` ``` ` blocks with `$ cat` / `$ langthree` shell format
5. Optional summary table at the end
6. Cross-references to related chapters

### Anti-Patterns to Avoid
- **Inventing new syntax for tests:** Tests must use exactly what the language produces. Check existing working tests to verify output format (e.g., `()` for unit, `Some 42` not `Some(42)`).
- **Using relative binary paths:** flt Command header must use the absolute path `/Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree`.
- **Over-counting TST-35:** The 8 new functions from Phase 52 are already fully covered. Do not add redundant tests.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Checking test coverage | Custom script | Manual audit of flt files vs requirement list | The mapping is simple: 1 requirement = ≥1 flt test file |
| Tutorial code examples | Invent from memory | Run actual LangThree binary to verify output | Tutorial examples must produce exactly the shown output |

**Key insight:** The main risk in Phase 53 is incorrect expected output in tutorial examples. All code snippets in the tutorial chapter must be run through the actual binary to verify output before the chapter is written. Fabricating expected output leads to documentation that contradicts the language.

## Common Pitfalls

### Pitfall 1: TST-33 Gap Analysis Confusion
**What goes wrong:** Phase 50 PLAN used different numbering for NLSEQ requirements than the actual test comment labels — tests labeled "NLSEQ-02" and "NLSEQ-03" in the files are NOT the same as NLSEQ-02 and NLSEQ-03 in REQUIREMENTS.md.
**Why it happens:** The Phase 50 executor adapted the tests to what actually worked (e.g., while needs `let _ =` wrapper) and renumbered test comments accordingly.
**How to avoid:** Trace requirement coverage by behavior, not by comment number. The gap is: no dedicated regression test for (a) structural terminators (else/with/| at same indent not preceded by SEMICOLON) and (b) multi-line function application (argument on indented continuation line — NOT same-level, so SEMICOLON injection does not fire, but a regression test documents this).
**Warning signs:** If you think all TST-33 regression cases are covered, check that each item in the success criterion has a named test file.

### Pitfall 2: Tutorial Output Mismatch
**What goes wrong:** Code example in tutorial produces different output than shown.
**Why it happens:** Tutorial was written before testing, or language details changed.
**How to avoid:** Run every tutorial code example through the binary before writing the expected output. Pay attention to: unit `()` appearing in output when expressions don't start with `let _`; `for-in` loops return unit (shown as `()`).
**Warning signs:** Output shows `()` unexpectedly, or output from `for-in` body side effects has trailing `()`.

### Pitfall 3: SUMMARY.md Not Updated
**What goes wrong:** Chapter 22 file created but SUMMARY.md not updated, so it doesn't appear in mdBook.
**Why it happens:** SUMMARY.md is a separate file that requires manual update.
**How to avoid:** Update SUMMARY.md at the same time as creating the chapter file. Add the entry under the "실용 프로그래밍" section (after the `21-imperative-ergonomics.md` entry).

### Pitfall 4: for-in Loop Output Includes Unit
**What goes wrong:** `for x in coll do ...` returns `()` and the REPL/test shows it.
**Why it happens:** In the language, `for x in` returns unit. If the loop is the last expression and its result is bound to `let result = for x in ...`, the binding prints `()`.
**How to avoid:** In tutorial examples, wrap loops with `let _ = ...` or ensure the example's final printed value is from a `println` call, not the loop return value.

## Code Examples

Verified patterns from inspecting actual test files:

### NLSEQ Regression Test: Structural Terminators
```
// Test: else at same indent does not get spurious SEMICOLON before it
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let x = 5
let result =
    if x > 2 then
        println "big"
        42
    else
        0
// --- Output:
big
42
```

### NLSEQ Regression Test: Multi-line Application
Multi-line function application (argument at deeper indent) is handled by INDENT/DEDENT, not by same-level SEMICOLON injection. A regression test verifies:
```
// Test: multi-line function application is not broken by newline sequencing
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let add3 a b c = a + b + c
let result =
    add3
        1
        2
        3
// --- Output:
6
```

### Tutorial Chapter 22 Structure
Chapter 22 covers three features:
1. Newline implicit sequencing (v6.0) — multi-line bodies without `;`
2. for-in collection loops — `for x in list do body`
3. Option/Result utilities — the 8 new prelude combinators

The chapter should be titled something like:
```markdown
# 22장: 실용 프로그래밍 (Practical Programming)
```

And placed in SUMMARY.md under:
```markdown
- [실용 프로그래밍](22-practical-programming.md)
```
(after the `21-imperative-ergonomics.md` entry in the "실용 프로그래밍" section)

## State of the Art

| Area | Status | Notes |
|------|--------|-------|
| NLSEQ flt tests | 5/7 needed | Missing: `nlseq-structural-terminator.flt`, `nlseq-multiline-app.flt` |
| FORIN flt tests | 4/4 complete | All success criteria covered by Phase 51 |
| Option/Result flt tests | 8/8 covered | All 8 new functions have dedicated flt tests from Phase 52 |
| Tutorial chapter 22 | Not started | File `tutorial/22-practical-programming.md` does not exist |
| SUMMARY.md | Needs update | Currently ends at chapter 21 |

**Current test counts:**
- Total flt tests: 589 (as of Phase 52 completion)
- NLSEQ tests in `tests/flt/expr/seq/`: 5 nlseq-*.flt + 5 seq-*.flt = 10 total
- FORIN tests in `tests/flt/expr/loop/`: 4 loop-for-in-*.flt
- Option/Result prelude tests in `tests/flt/file/prelude/`: 16 files total (option + result)

## Open Questions

1. **Should multi-line application test be a positive or regression test?**
   - What we know: Multi-line application uses INDENT/DEDENT (deeper indent), not same-level SEMICOLON injection. It already works because INDENT pushes a new context. The test confirms no regression from Phase 50.
   - What's unclear: Whether this scenario is actually broken in any edge case, or whether it's always safe.
   - Recommendation: Write it as a positive test that confirms multi-line application works (serves as regression protection). Target file: `tests/flt/expr/seq/nlseq-multiline-app.flt`.

2. **Chapter 22 language: Korean or English?**
   - What we know: All 21 existing chapters use Korean prose with English code/identifiers.
   - What's unclear: Should section headings mix Korean and English like chapter 21 does?
   - Recommendation: Follow chapter 21's style exactly — Korean section headings with English names in parentheses.

3. **How many tutorial examples per feature?**
   - What we know: Chapter 21 has 2-4 code examples per major section.
   - Recommendation: 2-3 examples per v6.0 feature is sufficient. Cover the main use case and one realistic composition example.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `tests/flt/expr/seq/*.flt` — existing NLSEQ test coverage
- Direct inspection of `tests/flt/expr/loop/loop-for-in-*.flt` — FORIN coverage
- Direct inspection of `tests/flt/file/prelude/prelude-option-*.flt` and `prelude-result-*.flt` — Option/Result coverage
- `.planning/phases/50-newline-implicit-sequencing/50-01-SUMMARY.md` — authoritative record of what Phase 50 created
- `.planning/phases/52-option-result-prelude/52-01-PLAN.md` — authoritative record of what Phase 52 created
- `.planning/ROADMAP.md` — Phase 53 success criteria (lines 98–103)
- `tutorial/SUMMARY.md` — current chapter list (ends at 21)
- `tutorial/21-imperative-ergonomics.md` — style reference for chapter authoring

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md` — NLSEQ-01 through NLSEQ-05 requirement definitions
- `tests/flt/file/offside/offside-if-branch.flt` — confirms existing tests DO cover if/else at deeper indent, but not as NLSEQ regression

## Metadata

**Confidence breakdown:**
- Test gap analysis: HIGH — directly read all relevant test files
- Architecture (what to build): HIGH — clear pattern from 50–52 plans
- Tutorial approach: HIGH — 21 existing chapters as style reference
- Exact test expected outputs: MEDIUM — must verify by running binary before writing

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable codebase, low churn expected)
