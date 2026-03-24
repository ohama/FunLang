// Phase 28 regression tests: N-tuple support
// SC2: Module-level tuple pattern binding - binds a=1, b=hello, c=true
let (a, b, c) = (1, "hello", true)

// SC3: Function with tuple parameter applied to 3-tuple -> 6
let add3 = fun (x, y, z) -> x + y + z
let sum = add3 (1, 2, 3)

// SC4: fst regression - 2-tuple still works -> 10
let fstResult = fst (10, 20)

// SC1: simple tuple binding prints the tuple as final value
let t = (1, "hello", true)
