# issue-010: Position info change causes 43 test failures

- **Status**: resolved
- **Priority**: high
- **Context**: Parser.fsy locate function, ParserTests.fs, ParserWrapper.fs
- **Created**: 2026-01-13
- **Resolved**: 2026-01-13
- **Session**: p5678901

## Summary

`locate` 함수에서 Line/Column에 +1을 추가한 변경으로 인해 43개 테스트 실패

## Root Cause

Parser.fsy의 `locate` 함수 변경:
```fsharp
// Before
{ Line = pos.Line; Column = pos.Column; File = None }

// After
{ Line = pos.Line + 1; Column = pos.Column + 1; File = None }
```

이 변경으로 Position이 0-based에서 1-based로 바뀌면서:
1. ParserTests의 expected 값들이 불일치
2. error-tests의 expected 출력이 불일치

## Resolution

1. **Ast.fs**: `noPos`를 `{ Line = 1; Column = 1; File = None }`로 변경 (1-based)
2. **ParserWrapper.fs**: `makeListLexerWithPositions`에서 LexBuffer의 StartPos/EndPos를 업데이트하여 `parseState.InputStartPosition`이 올바른 위치 반환
3. **Parser.fsy**: `locateAt` 헬퍼 함수 추가로 특정 심볼의 위치 지정 가능
4. **Interpreter.fs**: "No pattern matched" 에러에서 마지막 pattern 위치 표시
5. **error-tests/008-runtime-no-pattern-match.test**: expected 출력 업데이트

## Key Changes

```fsharp
// ParserWrapper.fs - LexBuffer 위치 업데이트
let private makeListLexerWithPositions (tokensWithPos: (Token * Position) list ref) =
    fun lexbuf ->
        match !tokensWithPos with
        | (t, pos) :: rest ->
            let lexPos = FSharp.Text.Lexing.Position.Empty
            let lexPos = { lexPos with pos_lnum = pos.Line - 1; pos_cnum = pos.Column - 1 }
            lexbuf.StartPos <- lexPos
            lexbuf.EndPos <- lexPos
            tokensWithPos := rest
            t
        | [] -> EOF
```

## Related

- 커밋 912b9ed에서 발생
- issue-008 (inline match syntax)와 별개 이슈
