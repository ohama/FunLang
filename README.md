# FunLang

FsLex/FsYacc를 사용한 간단한 함수형 언어 인터프리터

## 요구사항

- .NET 10.0 이상

## 빌드

```bash
dotnet build
```

## 실행

```bash
dotnet run --project src/FunLang
```

## 프로젝트 구조

```
src/FunLang/
├── Ast.fs          # AST(추상 구문 트리) 타입 정의
├── Parser.fsy      # FsYacc 문법 정의 파일
├── Lexer.fsl       # FsLex 렉서 정의 파일
├── Interpreter.fs  # 인터프리터 구현
└── Program.fs      # 진입점
```

## 파일 설명

| 파일 | 설명 |
|------|------|
| `Ast.fs` | 언어의 AST 타입을 정의 (표현식, 문장 등) |
| `Parser.fsy` | 문법 규칙 정의. 빌드 시 `Parser.fs` 자동 생성 |
| `Lexer.fsl` | 토큰 규칙 정의. 빌드 시 `Lexer.fs` 자동 생성 |
| `Interpreter.fs` | AST를 순회하며 실행하는 인터프리터 |
| `Program.fs` | REPL 또는 파일 실행 진입점 |

## 컴파일 순서

F#은 파일 순서가 중요합니다. 현재 순서:

1. `Ast.fs` - 다른 모든 파일에서 사용하는 타입
2. `Parser.fs` - AST 타입 참조
3. `Lexer.fs` - Parser의 토큰 타입 참조
4. `Interpreter.fs` - AST 타입 참조
5. `Program.fs` - 모든 모듈 사용

## 개발 가이드

### 새 토큰 추가

1. `Lexer.fsl`에 토큰 패턴 추가
2. `Parser.fsy`에 `%token` 선언 추가

### 새 문법 규칙 추가

1. `Ast.fs`에 필요한 타입 추가
2. `Parser.fsy`에 문법 규칙 추가
3. `Interpreter.fs`에 평가 로직 추가
