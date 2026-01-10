-- Expected: 55
-- Recursive fibonacci with multiline if-then-else

let rec fib = fun n ->
  if n <= 1 then n
  else fib (n - 1) + fib (n - 2)
in
fib 10
