-- Expected: 42
-- User-defined Option type

type Option 'a = None | Some of 'a

match Some 42 with
| Some x -> x
| None -> 0
