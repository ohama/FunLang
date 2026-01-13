module FunLang.NameResolution

open FunLang.Ast
open FunLang.Types

// =============================================================================
// Module Environment Types
// =============================================================================

/// Information about a single module
type ModuleInfo = {
    Name: string
    Values: Map<string, TypeScheme>          // Value bindings with their types
    Types: Map<string, TypeDefInfo>          // Type definitions
    Constructors: Map<string, ConstructorInfo> // Constructor info
    SubModules: Map<string, ModuleInfo>      // Nested modules
    Exports: Set<string>                     // Exported names
}

/// Information about a type definition
and TypeDefInfo = {
    Name: string
    TypeParams: string list
    Constructors: ConstructorInfo list
}

/// Information about a constructor
and ConstructorInfo = {
    Name: string
    TypeName: string
    ArgType: TypeScheme option
}

/// Global module registry
type ModuleRegistry = {
    Modules: Map<string, ModuleInfo>         // Top-level modules
}

// =============================================================================
// Empty/Default Values
// =============================================================================

let emptyModuleInfo name = {
    Name = name
    Values = Map.empty
    Types = Map.empty
    Constructors = Map.empty
    SubModules = Map.empty
    Exports = Set.empty
}

let emptyRegistry : ModuleRegistry = {
    Modules = Map.empty
}

// =============================================================================
// Module Building from AST
// =============================================================================

/// Build module info from a module declaration
let rec buildModuleInfo (moduleDecl: ModuleDecl) : ModuleInfo =
    // Compute exports set from export declarations
    let exports =
        match moduleDecl.Exports with
        | None -> Set.empty  // No exports = nothing exported
        | Some items ->
            items
            |> List.collect (function
                | ExportValue name -> [name]
                | ExportType (name, _) -> [name]
                | ExportModule name -> [name]
                | ExportAll ->
                    // Export all: collect all item names
                    moduleDecl.Items
                    |> List.collect (function
                        | MIValue (name, _, _) -> [name]
                        | MIRecValue (name, _, _) -> [name]
                        | MIType (td, _) -> [td.Name]
                        | MIModule m -> [m.Name]))
            |> Set.ofList

    // Process module items
    let values, types, constructors, subModules =
        moduleDecl.Items
        |> List.fold (fun (vals, tys, cons, subs) item ->
            match item with
            | MIValue (name, _, _) ->
                // Type will be inferred later
                (vals, tys, cons, subs)
            | MIRecValue (name, _, _) ->
                (vals, tys, cons, subs)
            | MIType (typeDef, _) ->
                let typeInfo = {
                    Name = typeDef.Name
                    TypeParams = typeDef.TypeParams
                    Constructors =
                        typeDef.Constructors
                        |> List.map (fun (consName, _) -> {
                            Name = consName
                            TypeName = typeDef.Name
                            ArgType = None  // Will be computed during type inference
                        })
                }
                let newCons =
                    typeInfo.Constructors
                    |> List.fold (fun acc c -> Map.add c.Name c acc) cons
                (vals, Map.add typeDef.Name typeInfo tys, newCons, subs)
            | MIModule nestedModule ->
                let nested = buildModuleInfo nestedModule
                (vals, tys, cons, Map.add nestedModule.Name nested subs)
        ) (Map.empty, Map.empty, Map.empty, Map.empty)

    {
        Name = moduleDecl.Name
        Values = values
        Types = types
        Constructors = constructors
        SubModules = subModules
        Exports = exports
    }

/// Build module registry from program
let buildRegistry (program: Program) : ModuleRegistry =
    let modules =
        program.Modules
        |> List.map (fun m -> (m.Name, buildModuleInfo m))
        |> Map.ofList
    { Modules = modules }

// =============================================================================
// Name Resolution
// =============================================================================

/// Result of name resolution
type ResolveResult<'T> =
    | Found of 'T
    | NotFound of string         // Error message
    | PrivateAccess of string    // Attempted to access private member

/// Resolve a qualified path to a value in a module
let rec resolveValueInModule (path: QualifiedPath) (moduleInfo: ModuleInfo) : ResolveResult<string * ModuleInfo> =
    match path with
    | [] -> NotFound "Empty path"
    | [name] ->
        // Final segment - look up in values
        if Set.contains name moduleInfo.Exports || moduleInfo.Exports = Set.empty then
            if Map.containsKey name moduleInfo.Values then
                Found (name, moduleInfo)
            else
                NotFound $"Value '{name}' not found in module '{moduleInfo.Name}'"
        else
            PrivateAccess $"Value '{name}' is not exported from module '{moduleInfo.Name}'"
    | segment :: rest ->
        // Intermediate segment - look up in submodules
        match Map.tryFind segment moduleInfo.SubModules with
        | Some subModule -> resolveValueInModule rest subModule
        | None -> NotFound $"Module '{segment}' not found in module '{moduleInfo.Name}'"

/// Resolve a qualified path starting from the registry
let resolveQualifiedValue (path: QualifiedPath) (registry: ModuleRegistry) : ResolveResult<string * ModuleInfo> =
    match path with
    | [] -> NotFound "Empty qualified path"
    | [name] -> NotFound $"Single-segment path '{name}' should be resolved as local variable"
    | moduleName :: rest ->
        match Map.tryFind moduleName registry.Modules with
        | Some moduleInfo -> resolveValueInModule rest moduleInfo
        | None -> NotFound $"Module '{moduleName}' not found"

/// Resolve a qualified constructor path
let resolveQualifiedConstructor (path: QualifiedPath) (registry: ModuleRegistry) : ResolveResult<ConstructorInfo * ModuleInfo> =
    match path with
    | [] -> NotFound "Empty qualified path"
    | [name] -> NotFound $"Single-segment path '{name}' should be resolved as local constructor"
    | _ ->
        // Path like ["Option"; "Some"] - last segment is constructor name
        let modulePath = List.take (List.length path - 1) path
        let consName = List.last path

        let rec findInModule (segments: string list) (moduleInfo: ModuleInfo) =
            match segments with
            | [] ->
                // At target module - look up constructor
                match Map.tryFind consName moduleInfo.Constructors with
                | Some consInfo ->
                    if Set.contains consName moduleInfo.Exports || moduleInfo.Exports = Set.empty then
                        Found (consInfo, moduleInfo)
                    else
                        PrivateAccess $"Constructor '{consName}' is not exported from module '{moduleInfo.Name}'"
                | None -> NotFound $"Constructor '{consName}' not found in module '{moduleInfo.Name}'"
            | seg :: rest ->
                match Map.tryFind seg moduleInfo.SubModules with
                | Some subModule -> findInModule rest subModule
                | None -> NotFound $"Module '{seg}' not found"

        match modulePath with
        | [] -> NotFound "Invalid path"
        | modName :: rest ->
            match Map.tryFind modName registry.Modules with
            | Some moduleInfo -> findInModule rest moduleInfo
            | None -> NotFound $"Module '{modName}' not found"

// =============================================================================
// Import Processing
// =============================================================================

/// Process open statement - bring all exports into scope
let processOpen (modulePath: QualifiedPath) (registry: ModuleRegistry) : Result<Map<string, string * ModuleInfo>, string> =
    let rec findModule (path: QualifiedPath) (current: ModuleInfo option) =
        match path, current with
        | [], Some m -> Ok m
        | [], None -> Error "Empty path"
        | name :: rest, None ->
            match Map.tryFind name registry.Modules with
            | Some m -> findModule rest (Some m)
            | None -> Error $"Module '{name}' not found"
        | name :: rest, Some m ->
            match Map.tryFind name m.SubModules with
            | Some sub -> findModule rest (Some sub)
            | None -> Error $"Module '{name}' not found in '{m.Name}'"

    match findModule modulePath None with
    | Error e -> Error e
    | Ok moduleInfo ->
        // Return all exported values
        let exported =
            moduleInfo.Exports
            |> Set.toList
            |> List.choose (fun name ->
                if Map.containsKey name moduleInfo.Values then
                    Some (name, (name, moduleInfo))
                else
                    None)
            |> Map.ofList
        Ok exported

/// Process selective import - bring specific names into scope
let processImportItems (modulePath: QualifiedPath) (items: string list) (registry: ModuleRegistry) : Result<Map<string, string * ModuleInfo>, string> =
    match processOpen modulePath registry with
    | Error e -> Error e
    | Ok allExports ->
        items
        |> List.fold (fun acc name ->
            match acc with
            | Error e -> Error e
            | Ok m ->
                match Map.tryFind name allExports with
                | Some v -> Ok (Map.add name v m)
                | None -> Error $"'{name}' is not exported from module")
            (Ok Map.empty)
