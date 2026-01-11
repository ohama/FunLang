module FunLang.Suggestions

/// Compute Levenshtein distance between two strings using dynamic programming.
/// Uses O(min(m,n)) space with single-row optimization.
let levenshteinDistance (s1: string) (s2: string) : int =
    let m = s1.Length
    let n = s2.Length

    // Handle edge cases
    if m = 0 then n
    elif n = 0 then m
    else
        // Ensure s1 is the shorter string for space optimization
        let s1, s2, m, n =
            if m <= n then s1, s2, m, n
            else s2, s1, n, m

        // Use single row for space efficiency
        let prev = Array.init (m + 1) id

        for j in 1..n do
            let mutable prevDiag = prev.[0]
            prev.[0] <- j

            for i in 1..m do
                let temp = prev.[i]
                prev.[i] <-
                    if s1.[i - 1] = s2.[j - 1] then
                        prevDiag
                    else
                        1 + min (min prev.[i - 1] prev.[i]) prevDiag
                prevDiag <- temp

        prev.[m]

/// Maximum edit distance for suggestions
let private maxDistance = 2

/// Maximum number of suggestions to return
let private maxSuggestions = 3

/// Find similar names from candidates within the distance threshold.
/// Returns names with distance <= 2, sorted by distance then alphabetically.
/// Returns at most 3 suggestions.
let findSimilar (name: string) (candidates: string list) : string list =
    if String.length name = 0 then
        []
    else
        candidates
        |> List.choose (fun candidate ->
            if String.length candidate = 0 then
                None
            else
                let dist = levenshteinDistance name candidate
                if dist <= maxDistance then
                    Some (candidate, dist)
                else
                    None)
        |> List.sortBy (fun (candidate, dist) -> dist, candidate)
        |> List.map fst
        |> List.truncate maxSuggestions
