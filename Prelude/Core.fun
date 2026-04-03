module Core =
    let id x = x
    let const x = fun y -> x
    let compose f = fun g -> fun x -> f (g x)
    let flip f = fun x -> fun y -> f y x
    let apply f = fun x -> f x
    let (^^) a b = string_concat a b
    #[left 1]
    let (|>) x f = f x
    #[right 2]
    let (>>) f g = fun x -> g (f x)
    #[left 2]
    let (<<) f g = fun x -> f (g x)
    let not x = if x then false else true
    let min a = fun b -> if a < b then a else b
    let max a = fun b -> if a > b then a else b
    let abs x = if x < 0 then 0 - x else x
    let fst p = match p with | (a, _) -> a
    let snd p = match p with | (_, b) -> b
    let ignore x = ()

open Core
