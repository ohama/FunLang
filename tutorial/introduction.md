# LangThree 튜토리얼

함수형 프로그래밍 경험자를 위한 LangThree 언어 튜토리얼입니다.

LangThree는 F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 파이프 연산자, 문자열 내장 함수를 갖춘 ML 계열 함수형 프로그래밍 언어입니다.

## 시작하기

[1장: 시작하기](01-getting-started.md)부터 시작하세요.

## 목차

**기초**
- [시작하기](01-getting-started.md) — 실행 방법, 기본 값, 연산자
- [함수](02-functions.md) — lambda, let, let rec, 고차 함수
- [리스트와 튜플](03-lists-and-tuples.md) — 리스트, 튜플, cons
- [패턴 매칭](04-pattern-matching.md) — 패턴 종류, when guard

**타입 시스템**
- [대수적 데이터 타입](05-algebraic-types.md) — ADT, 파라메트릭, 재귀 타입
- [레코드](06-records.md) — 레코드, mutable fields

**실용 프로그래밍**
- [문자열과 출력](07-strings-and-output.md) — string 함수, printf
- [파이프와 합성](08-pipes-and-composition.md) — |>, >>, <<
- [Prelude 표준 라이브러리](09-prelude.md) — Option, Result, 리스트 함수
- [모듈과 네임스페이스](10-modules.md) — module, open, qualified access

**에러 처리**
- [예외](11-exceptions.md) — try-with, raise, when guard
- [에러 처리 전략](12-error-handling.md) — Exception vs Option vs Result

**심화 주제**
- [사용자 정의 연산자](13-user-defined-operators.md) — 연산자 정의, 우선순위
- [GADT](14-gadt.md) — 타입 정제, 어노테이션
- [알고리즘과 자료구조](15-algorithms.md) — 정렬, 트리, 수론

**부록**
- [CLI 참조](16-cli-reference.md) — 모든 CLI 모드
