let id x = x

let const x = fun y -> x

let compose f = fun g -> fun x -> f (g x)
