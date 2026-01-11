# Indent Tests

Indentation processing is tested internally via `IndentationTests.fs` (unit tests).

The `--show-indents` CLI flag is not yet implemented, so file-based indentation tests are not available.

To add file-based indent tests, implement `--show-indents` in `Program.fs` to output indentation tokens.
