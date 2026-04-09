typeclass Show 'a =
    | show : 'a -> string

instance Show int =
    let show (x : int) : string = to_string x

instance Show bool =
    let show (x : bool) : string = if x then "true" else "false"

instance Show string =
    let show (x : string) : string = x

instance Show char =
    let show (x : char) : string = to_string x

typeclass Eq 'a =
    | eq : 'a -> 'a -> bool

instance Eq int =
    let eq (x : int) (y : int) : bool = x = y

instance Eq bool =
    let eq (x : bool) (y : bool) : bool = x = y

instance Eq string =
    let eq (x : string) (y : string) : bool = x = y

instance Eq char =
    let eq (x : char) (y : char) : bool = x = y
