module LangThree.Tests.IndentFilterTests

open Expecto
open LangThree.IndentFilter

[<Tests>]
let configTests = testList "IndentFilter.Config" [
    test "defaultConfig has IndentWidth 4" {
        Expect.equal defaultConfig.IndentWidth 4 "Default indent should be 4"
    }

    test "initialState starts with stack [0]" {
        Expect.equal initialState.IndentStack [0] "Initial stack should be [0]"
    }
]
