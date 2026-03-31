module ProjectFile

open System.IO
open Tomlyn

// TOML POCO types (must be mutable for Tomlyn reflection-based deserialization)
[<CLIMutable>]
type TomlProjectSection = {
    mutable name: string
    mutable prelude: string
}

[<CLIMutable>]
type TomlTarget = {
    mutable name: string
    mutable main: string
}

[<CLIMutable>]
type TomlFunProj = {
    mutable project: TomlProjectSection
    mutable executable: System.Collections.Generic.List<TomlTarget>
    mutable test: System.Collections.Generic.List<TomlTarget>
}

// Idiomatic F# records for use in Program.fs
type TargetConfig = {
    Name: string
    Main: string   // absolute path
}

type FunProjConfig = {
    ProjectName: string
    PreludePath: string option   // None = not set in TOML; absolute path if set
    Executables: TargetConfig list
    Tests: TargetConfig list
}

let private makeTarget (projDir: string) (t: TomlTarget) : TargetConfig = {
    Name = if t.name <> null then t.name else ""
    Main = if t.main <> null then Path.GetFullPath(Path.Combine(projDir, t.main)) else ""
}

/// Parse a funproj.toml file. projDir is the directory containing funproj.toml (for relative path resolution).
let parseFunProj (tomlText: string) (projDir: string) : FunProjConfig =
    let raw = TomlSerializer.Deserialize<TomlFunProj>(tomlText)
    let projSection = raw.project   // may be Unchecked.defaultof if [project] absent
    {
        ProjectName =
            if box projSection <> null && projSection.name <> null then projSection.name else ""
        PreludePath =
            if box projSection <> null && projSection.prelude <> null && projSection.prelude <> ""
            then Some (Path.GetFullPath(Path.Combine(projDir, projSection.prelude)))
            else None
        Executables =
            if raw.executable = null then []
            else raw.executable |> Seq.map (makeTarget projDir) |> Seq.toList
        Tests =
            if raw.test = null then []
            else raw.test |> Seq.map (makeTarget projDir) |> Seq.toList
    }

/// Find funproj.toml in the current working directory.
let findFunProj () : string option =
    let candidate = Path.GetFullPath("funproj.toml")
    if File.Exists candidate then Some candidate else None

/// Load and parse funproj.toml. Returns Error message string on failure.
let loadFunProj (path: string) : Result<FunProjConfig, string> =
    try
        let text = File.ReadAllText path
        let projDir = Path.GetDirectoryName path
        Ok (parseFunProj text projDir)
    with ex ->
        Error (sprintf "Failed to parse %s: %s" path ex.Message)
