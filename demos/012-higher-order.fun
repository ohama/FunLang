-- Expected: 30
-- Higher-order functions

let apply = fun f -> fun x -> f x in
let double = fun x -> x * 2 in
apply double 15
