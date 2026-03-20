let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t

let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t

let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t

let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t

let rec reverse acc = fun xs -> match xs with | [] -> acc | h :: t -> reverse (h :: acc) t

let rec append xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: append t ys

let hd xs = match xs with | h :: _ -> h

let tl xs = match xs with | _ :: t -> t
