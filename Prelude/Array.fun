module Array =
    let create n def = array_create n def
    let get arr i    = array_get arr i
    let set arr i v  = array_set arr i v
    let length arr   = array_length arr
    let ofList xs    = array_of_list xs
    let toList arr   = array_to_list arr
