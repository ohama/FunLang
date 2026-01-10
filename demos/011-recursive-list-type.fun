-- Expected: 10
-- Recursive list type with sum

type List 'a = Nil | Cons of 'a * List 'a

let rec sum = fun xs ->
  match xs with
  | Nil -> 0
  | Cons (h, t) -> h + sum t
in
sum (Cons (1, Cons (2, Cons (3, Cons (4, Nil)))))
