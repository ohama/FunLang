let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t

let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t

let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t

let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t

let rec reverse acc = fun xs -> match xs with | [] -> acc | h :: t -> reverse (h :: acc) t

let rec append xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: append t ys

let hd xs = match xs with | h :: _ -> h

let tl xs = match xs with | _ :: t -> t

let rec zip xs = fun ys -> match xs with | [] -> [] | x :: xt -> match ys with | [] -> [] | y :: yt -> (x, y) :: zip xt yt

let rec take n = fun xs -> if n = 0 then [] else match xs with | [] -> [] | h :: t -> h :: take (n - 1) t

let rec drop n = fun xs -> if n = 0 then xs else match xs with | [] -> [] | _ :: t -> drop (n - 1) t

let rec any pred = fun xs -> match xs with | [] -> false | h :: t -> if pred h then true else any pred t

let rec all pred = fun xs -> match xs with | [] -> true | h :: t -> if pred h then all pred t else false

let rec flatten xss = match xss with | [] -> [] | xs :: rest -> append xs (flatten rest)

let rec nth n = fun xs -> match xs with | h :: t -> if n = 0 then h else nth (n - 1) t
