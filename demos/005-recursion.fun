-- Expected: 120
-- Recursive factorial with multiline if-then-else

let rec factorial = fun n ->
  if n <= 1 then 1
  else n * factorial (n - 1)
in
factorial 5
