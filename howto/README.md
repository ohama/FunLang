# Howto Documents

| # | 문서 | 설명 | 작성일 |
|---|------|------|--------|
| 1 | [add-sequencing-without-lalr-conflict](add-sequencing-without-lalr-conflict.md) | 이미 구분자로 쓰이는 토큰(;)을 시퀀싱 연산자로 추가할 때 LALR 충돌을 피하는 SeqExpr 패턴 | 2026-03-28 |
| 2 | [add-indexing-syntax-with-single-token](add-indexing-syntax-with-single-token.md) | 복합 토큰(.[)을 단일 토큰으로 lexing하여 기존 dot-access 문법과 LALR 충돌을 피하는 방법 | 2026-03-28 |
| 3 | [add-syntax-via-parser-desugar](add-syntax-via-parser-desugar.md) | 새 구문을 기존 AST 노드로 desugar하여 Eval/TypeChecker 변경 없이 기능을 추가하는 패턴 | 2026-03-28 |
| 4 | [implement-mutable-variables-with-refvalue](implement-mutable-variables-with-refvalue.md) | RefValue 패턴으로 가변 변수와 클로저 캡처를 구현하는 방법 | 2026-03-27 |
| 5 | [resolve-assignment-ambiguity-in-lalr-parser](resolve-assignment-ambiguity-in-lalr-parser.md) | LALR(1) 파서에서 변수 할당과 필드 할당의 모호성을 shift 우선으로 해결 | 2026-03-27 |
| 6 | [implement-polymorphic-gadt-return](implement-polymorphic-gadt-return.md) | GADT match에서 분기마다 다른 타입을 반환하는 다형적 함수 구현하기 | 2026-03-23 |
| 7 | [write-bracket-stack-transform-script](write-bracket-stack-transform-script.md) | 괄호 종류를 추적하는 스택으로 소스 코드의 구분자를 안전하게 일괄 변환하기 | 2026-03-22 |
| 8 | [change-separator-token-in-lalr-parser](change-separator-token-in-lalr-parser.md) | LALR(1) 문법에서 리스트/튜플 구분자를 다른 토큰으로 변경하는 전략 | 2026-03-22 |
| 9 | [implement-offside-rule-with-context-stack](implement-offside-rule-with-context-stack.md) | Counter 대신 context stack으로 F# 스타일 offside rule 구현하기 | 2026-03-20 |
| 10 | [handle-indent-dedent-in-lalr-parser](handle-indent-dedent-in-lalr-parser.md) | Python/F# 스타일 들여쓰기를 LALR(1) 파서에 적용하는 IndentFilter 패턴 | 2026-03-20 |

---
총 10개 | 업데이트: 2026-03-28
