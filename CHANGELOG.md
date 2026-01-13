# Changelog

All notable changes to FunLang will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-01-13

### Added
- Add pattern matching analysis module (exhaustiveness & redundancy)
- Add Phase 9: Pattern Matching Improvements plan

### Changed
- Integrate pattern analysis warnings into type inference (Phase 9.3)

## [0.2.0] - 2026-01-13

### Added
- Add version system with upgrade script

### Fixed
- Fix inline match syntax parsing (issue-008)
- Fix position tracking to use 1-based line/column numbers
- Fix parser indentation issues and improve error messages

## [0.1.0] - 2026-01-13

### Added
- Initial release of FunLang interpreter
- Core language features: literals, variables, operators, functions
- Pattern matching with guards
- User-defined algebraic data types
- Hindley-Milner type inference
- List and tuple support
- Interactive REPL mode
- Rich error messages with source locations
- Comment syntax (// ...)
