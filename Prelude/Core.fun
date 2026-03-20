let id x = x

let const x = fun y -> x

let compose f = fun g -> fun x -> f (g x)

let flip f = fun x -> fun y -> f y x

let apply f = fun x -> f x

let (^^) a b = string_concat a b
