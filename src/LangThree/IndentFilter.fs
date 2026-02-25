module LangThree.IndentFilter

open Parser

/// Configuration for indent processing
type IndentConfig = {
    IndentWidth: int  // Expected indent width (2, 4, or 8)
    StrictWidth: bool // If true, enforce exact multiples of IndentWidth
}

/// Default configuration: 4 spaces, not strict
let defaultConfig = { IndentWidth = 4; StrictWidth = false }

/// State maintained during filtering
type FilterState = {
    IndentStack: int list  // Stack of indent levels, starts with [0]
    LineNum: int           // Current line number for errors
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1 }

/// Error for indentation problems
exception IndentationError of line: int * message: string
