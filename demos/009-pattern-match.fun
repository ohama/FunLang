-- Expected: 6
-- Pattern matching on list

let rec sum = fun xs ->
  match xs with
  | [] -> 0
  | h :: t -> h + sum t
in
sum [1; 2; 3]
