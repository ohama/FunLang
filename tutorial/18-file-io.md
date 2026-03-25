# 파일 I/O와 시스템 함수 (File I/O and System Functions)

모듈로 코드를 구조화하고 외부 파일을 임포트할 수 있게 되었으니, 이번에는 파일 시스템과 운영체제와 상호작용하는 방법을 알아봅니다. LangThree는 파일 읽기/쓰기, 환경 변수, 디렉토리 탐색 등 시스템과 상호작용하는 내장 함수를 제공합니다. 이 함수들은 인터프리터에 내장되어 있으며 import 없이 사용할 수 있습니다.

## 파일 읽기와 쓰기

### read_file / write_file

가장 기본적인 파일 I/O입니다. `write_file`은 파일에 문자열을 쓰고, `read_file`은 파일 전체를 문자열로 읽습니다:

```
$ cat file_rw.l3
let _ = write_file "/tmp/hello.txt" "hello world"
let content = read_file "/tmp/hello.txt"
let result = content

$ langthree file_rw.l3
"hello world"
```

`write_file`은 기존 파일을 덮어씁니다. 파일이 없으면 새로 생성합니다.

### append_file

기존 파일 끝에 내용을 추가합니다:

```
$ cat file_append.l3
let _ = write_file "/tmp/log.txt" "line1"
let _ = append_file "/tmp/log.txt" "\nline2"
let result = read_file "/tmp/log.txt"

$ langthree file_append.l3
"line1\nline2"
```

### read_lines / write_lines

줄 단위로 읽고 쓸 때 사용합니다. `write_lines`는 문자열 리스트를 각 줄로 쓰고, `read_lines`는 파일을 줄 단위로 읽어 리스트로 반환합니다:

```
$ cat file_lines.l3
let _ = write_lines "/tmp/data.txt" ["alice"; "bob"; "carol"]
let names = read_lines "/tmp/data.txt"
let result = length names

$ langthree file_lines.l3
3
```

### file_exists

파일 존재 여부를 확인합니다:

```
$ cat file_check.l3
let _ = write_file "/tmp/exists.txt" "data"
let result = file_exists "/tmp/exists.txt"

$ langthree file_check.l3
true
```

## 에러 처리

파일이 없는 경우 `read_file`과 `read_lines`는 예외를 발생시킵니다. `try-with`로 처리할 수 있습니다:

```
$ cat file_error.l3
let result =
    try
        read_file "/tmp/nonexistent_file_xyz.txt"
    with
    | e -> "file not found"

$ langthree file_error.l3
"file not found"
```

## 시스템 함수

### get_cwd

현재 작업 디렉토리를 반환합니다:

```
$ cat get_cwd.l3
let cwd = get_cwd ()
let result = string_length cwd > 0

$ langthree get_cwd.l3
true
```

### get_env

환경 변수를 읽습니다. 변수가 설정되어 있지 않으면 예외가 발생합니다:

```
$ cat get_env.l3
let result =
    try get_env "NONEXISTENT_VAR_XYZ"
    with e -> "not set"

$ langthree get_env.l3
"not set"
```

### get_args

스크립트에 전달된 커맨드라인 인자를 문자열 리스트로 반환합니다:

```
$ cat show_args.l3
let args = get_args ()
let result = args

$ langthree show_args.l3 -- foo bar
["foo"; "bar"]
```

### path_combine

두 경로를 합칩니다:

```
funlang> path_combine "/home/user" "file.txt"
"/home/user/file.txt"
```

### dir_files

디렉토리의 파일 목록을 반환합니다:

```
$ cat dir_list.l3
let files = dir_files "/tmp"
let result = length files > 0

$ langthree dir_list.l3
true
```

### eprint

표준 오류(stderr)로 출력합니다. 디버깅이나 로그에 유용합니다:

```
$ cat debug.l3
let _ = eprint "debug: starting\n"
let result = 42

$ langthree debug.l3
42
```

`eprint`의 출력은 stderr로 가므로, stdout의 결과와 섞이지 않습니다.

### stdin_read_line

표준 입력에서 한 줄을 읽습니다. 대화형 프로그램에 유용합니다:

```
$ cat greet.l3
let _ = print "이름을 입력하세요: "
let name = stdin_read_line ()
let _ = println ("안녕하세요, " + name + "!")
let result = ()

$ echo "Alice" | langthree greet.l3
이름을 입력하세요: 안녕하세요, Alice!
```

## 함수 요약

| 함수 | 타입 | 설명 |
|------|------|------|
| `read_file` | `string -> string` | 파일 전체를 문자열로 읽기 |
| `write_file` | `string -> string -> unit` | 파일에 문자열 쓰기 (덮어쓰기) |
| `append_file` | `string -> string -> unit` | 파일 끝에 추가 |
| `file_exists` | `string -> bool` | 파일 존재 여부 |
| `read_lines` | `string -> string list` | 줄 단위로 읽기 |
| `write_lines` | `string -> string list -> unit` | 줄 단위로 쓰기 |
| `get_cwd` | `unit -> string` | 현재 작업 디렉토리 |
| `get_env` | `string -> string` | 환경 변수 (없으면 예외) |
| `get_args` | `unit -> string list` | 커맨드라인 인자 |
| `path_combine` | `string -> string -> string` | 경로 합치기 |
| `dir_files` | `string -> string list` | 디렉토리 파일 목록 |
| `eprint` | `string -> unit` | stderr 출력 |
| `stdin_read_line` | `unit -> string` | stdin에서 한 줄 읽기 |

## 참고 사항

- **unit 인자:** `get_cwd`, `get_args`, `stdin_read_line`은 `()` 인자가 필요합니다
- **커링:** `write_file`, `append_file`, `write_lines`, `path_combine`은 커링됩니다. `write_file "/tmp/f.txt" "content"` 형태로 호출
- **예외:** `read_file`, `read_lines`, `get_env`, `dir_files`는 대상이 없으면 예외 발생 — `try-with`로 처리
- **경로:** 상대 경로는 현재 작업 디렉토리 기준, 절대 경로도 사용 가능
