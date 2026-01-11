# issue-008: Sorting algorithms integrated test 보류

- **Status**: unresolved
- **Priority**: low
- **Context**: tests/file-tests/integrated-tests/001-sorting-algorithms.test
- **Created**: 2026-01-12 06:10
- **Session**: m2345678

## Summary

Sorting algorithms integrated test 보류 - 주석 기능 미구현으로 테스트 파일 실행 실패

## Description

`tests/file-tests/integrated-tests/001-sorting-algorithms.test` 파일에 여러 정렬 알고리즘(BST Sort, Merge Sort, Quick Sort, Insertion Sort, Selection Sort, Bubble Sort)을 구현했으나, FunLang에서 주석(`//`)을 지원하지 않아 실행 실패.

## Workaround

주석 기능 구현 후 테스트 파일 재작성 필요.

## Related

- issue-004: Comments (-- ...) not supported in lexer (Won't Fix로 종료됨, 재검토 필요)
