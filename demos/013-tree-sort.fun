-- Expected: [1; 2; 3; 4; 5]
type Tree 'a = Leaf | Node of 'a * Tree 'a * Tree 'a
let rec append = fun xs -> fun ys -> match xs with | [] -> ys | h :: t -> h :: append t ys in
let rec insert = fun t -> fun x -> match t with | Leaf -> Node (x, Leaf, Leaf) | Node (v, left, right) -> if x <= v then Node (v, insert left x, right) else Node (v, left, insert right x) in
let rec inorder = fun t -> match t with | Leaf -> [] | Node (v, left, right) -> append (inorder left) (append [v] (inorder right)) in
let rec buildTree = fun xs -> match xs with | [] -> Leaf | h :: t -> insert (buildTree t) h in
let treeSort = fun xs -> inorder (buildTree xs) in
treeSort [3; 1; 4; 5; 2]
