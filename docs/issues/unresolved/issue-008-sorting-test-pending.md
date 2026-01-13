# issue-008: Sorting algorithms integrated test 보류

- **Status**: pending (on hold)
- **Priority**: low
- **Context**: tests/file-tests/integrated-tests/001-sorting-algorithms.test.pending
- **Created**: 2026-01-12 06:10
- **Updated**: 2026-01-13
- **Session**: p5678901

## Summary

Sorting algorithms integrated test 보류 - 복잡한 파싱 이슈로 테스트 보류

## Description

`tests/file-tests/integrated-tests/001-sorting-algorithms.test` 파일에 여러 정렬 알고리즘을 구현했으나, 복잡한 파싱 이슈(line 232)로 인해 테스트 보류.

테스트 파일은 `.pending` 확장자로 변경하여 테스트 실행에서 제외됨.

## Current Status

- issue-009 (match INDENT after else) 해결됨
- 그러나 line 232에서 새로운 파싱 에러 발생
- 추가 조사 필요

## Workaround

테스트 파일명을 `.test.pending`으로 변경하여 테스트에서 제외

## Related

- issue-009: Match expression INDENT issue (resolved)
