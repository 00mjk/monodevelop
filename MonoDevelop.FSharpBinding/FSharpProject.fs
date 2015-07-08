﻿namespace MonoDevelop.FSharp

open System
open System.IO
open MonoDevelop.Core
open MonoDevelop.Projects
open MonoDevelop.Projects.Formats.MSBuild
open MonoDevelop.Ide
open System.Xml
open MonoDevelop.Core.Assemblies
open ExtCore.Control

type FSharpProject() as self = 
    inherit DotNetProject()
    // Keep the platforms combo of CodeGenerationPanelWidget in sync with this list
    let supportedPlatforms = [| "anycpu"; "x86"; "x64"; "itanium" |]
    let FSharp3Import          = "$(MSBuildExtensionsPath32)\\..\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\Microsoft.FSharp.Targets"
    let FSharp31Import         = "$(MSBuildExtensionsPath32)\\..\\Microsoft SDKs\\F#\\3.1\\Framework\\v4.0\\Microsoft.FSharp.Targets"
    let FSharp31PortableImport = "$(MSBuildExtensionsPath32)\\..\\Microsoft SDKs\\F#\\3.1\\Framework\\v4.0\\Microsoft.Portable.FSharp.Targets"
    let oldFSharpProjectGuid   = "{4925A630-B079-445D-BCD4-3A9C94FE9307}"
    let supportedPortableProfiles = ["Profile7";"Profile47";"Profile78";"Profile259"]

    ///keyed on TargetProfile, Value: TargetFSharpCoreVersion, netcore
    let profileMap =
      Map.ofList ["Profile7",   ("3.3.1.0",   true)
                  "Profile47",  ("2.3.5.1",   false)
                  "Profile78",  ("3.78.3.1",  true)
                  "Profile259", ("3.259.3.1", true) ]

    let langServ = MDLanguageService.Instance
    let mutable initialisedAsPortable = false
    
    let invalidateProjectFile() =
      try 
        if File.Exists (self.FileName.ToString()) then
          let options = langServ.GetProjectCheckerOptions(self.FileName.ToString(), [("Configuration", IdeApp.Workspace.ActiveConfigurationId)])
          langServ.InvalidateConfiguration(options)
          langServ.ClearProjectInfoCache()
      with ex -> LoggingService.LogError ("Could not invalidate configuration", ex)
    
    let invalidateFiles (args:#ProjectFileEventInfo seq) =
      for projectFileEvent in args do
        if MDLanguageService.SupportedFileName (projectFileEvent.ProjectFile.FilePath.ToString()) then
          invalidateProjectFile()

    let isPortable (project:MSBuildProject) = 
      project.Imports
      |> Seq.tryFind (fun i -> i.EvaluatedProject.EndsWith "Microsoft.Portable.FSharp.Targets" )
      |> Option.isSome

    [<ProjectPathItemProperty ("TargetProfile", DefaultValue = "mscorlib")>]
    member val TargetProfile = "mscorlib" with get, set

    [<ProjectPathItemProperty ("TargetFSharpCoreVersion", DefaultValue = "")>]
    member val TargetFSharpCoreVersion = String.Empty with get, set

    override x.OnInitialize() =
      base.OnInitialize()

    override x.OnReadProject(progress, project) =
      project.Imports
      |> Seq.tryFind (fun i -> Path.GetFileName(i.EvaluatedProject) = "Microsoft.Portable.FSharp.Targets" )
      |> Option.iter (fun _ -> initialisedAsPortable <- true)
      base.OnReadProject(progress, project)

    override x.OnReadProjectHeader(progress, project) =
      project.Imports
      |> Seq.tryFind (fun i -> Path.GetFileName(i.EvaluatedProject) = "Microsoft.Portable.FSharp.Targets" )
      |> Option.iter (fun _ -> initialisedAsPortable <- true)
      base.OnReadProjectHeader(progress, project)

    override x.OnSupportsFramework (framework) =
      if isPortable self.MSBuildProject then
        framework.Id.Identifier = TargetFrameworkMoniker.ID_PORTABLE && supportedPortableProfiles |> List.exists ((=) framework.Id.Profile)
      else base.OnSupportsFramework (framework)

    override x.OnInitializeFromTemplate(createInfo, options) =
      base.OnInitializeFromTemplate(createInfo, options)
      if options.HasAttribute "FSharpPortable" then initialisedAsPortable <- true
      if options.HasAttribute "TargetProfile" then x.TargetProfile <- options.GetAttribute "TargetProfile"
      if options.HasAttribute "TargetFSharpCoreVersion" then x.TargetFSharpCoreVersion <- options.GetAttribute "TargetFSharpCoreVersion"

    override x.OnGetDefaultImports (imports) =
      base.OnGetDefaultImports (imports)
      // By default projects use the F# 3.1 targets file unless only 3.0 is available on the machine.
      // New projects will be created with this targets file
      // If FSharp 3.1 is available, use it. If not, use 3.0
      if initialisedAsPortable then
        if MSBuildProjectService.IsTargetsAvailable(FSharp31PortableImport) then imports.Add (FSharp31PortableImport)
        else failwith "F# portable target not found"
        
      else
        if MSBuildProjectService.IsTargetsAvailable(FSharp31Import) then imports.Add (FSharp31Import)
        else imports.Add (FSharp3Import)
    
    override x.OnWriteProject(monitor, msproject) =
      base.OnWriteProject(monitor, msproject)

      //Fix pcl netcore and TargetFSharpCoreVersion
      let globalGroup = msproject.GetGlobalPropertyGroup()

      maybe {
        let! targetFrameworkProfile = x.TargetFramework.Id.Profile |> Option.ofString
        let! fsharpcoreversion, netcore = profileMap |> Map.tryFind targetFrameworkProfile
        do globalGroup.SetValue ("TargetFSharpCoreVersion", fsharpcoreversion, "", true)
        let targetProfile = if netcore then "netcore" else "mscorlib"
        do globalGroup.SetValue ("TargetProfile", targetProfile, "mscorlib", true) } |> ignore

      // This removes the old guid on saving the project
      let removeGuid (innerText:string) guidToRemove =
        innerText.Split ( [|';'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.filter (fun guid -> not (guid.Equals (guidToRemove, StringComparison.OrdinalIgnoreCase)))
        |> String.concat ";"

      try 
        let fsimportExists = 
          msproject.Imports 
          |> Seq.exists (fun import -> import.Project.EndsWith("FSharp.Targets", StringComparison.OrdinalIgnoreCase))
        if fsimportExists then 
          globalGroup.GetProperties()
          |> Seq.tryFind (fun p -> p.Name = "ProjectTypeGuids")
          |> Option.iter (fun currentGuids -> let newProjectTypeGuids = removeGuid currentGuids.Value oldFSharpProjectGuid
                                              currentGuids.SetValue(newProjectTypeGuids))
      with exn -> LoggingService.LogWarning("Failed to remove old F# guid", exn)
    
    override x.OnCompileSources(items, config, configSel, monitor) = 
      CompilerService.Compile(items, config, configSel, monitor)
    
    override x.OnCreateCompilationParameters(config, kind) = 
      let pars = new FSharpCompilerParameters()
      // Set up the default options
      if supportedPlatforms |> Array.exists (fun x -> x.Contains(config.Platform)) then pars.PlatformTarget <- config.Platform
      match kind with
      | ConfigurationKind.Debug -> 
          pars.AddDefineSymbol "DEBUG"
          pars.Optimize <- false
          pars.GenerateTailCalls <- false
      | ConfigurationKind.Release -> 
          pars.Optimize <- true
          pars.GenerateTailCalls <- true
      | _ -> ()
      //pars.DocumentationFile <- config.CompiledOutputName.FileNameWithoutExtension + ".xml"
      pars :> DotNetCompilerParameters
    
    override x.OnGetSupportedClrVersions() = 
      [| ClrVersion.Net_2_0; ClrVersion.Net_4_0; ClrVersion.Net_4_5; ClrVersion.Clr_2_1 |]

    override x.OnFileAddedToProject(e) =
      base.OnFileAddedToProject(e)
      if not self.Loading then invalidateFiles(e)

    override x.OnFileRemovedFromProject(e) =
      base.OnFileRemovedFromProject(e)
      if not self.Loading then invalidateFiles(e)

    override x.OnFileRenamedInProject(e) =
      base.OnFileRenamedInProject(e)
      if not self.Loading then invalidateFiles(e)

    override x.OnFilePropertyChangedInProject(e) =
      base.OnFilePropertyChangedInProject(e)
      if not self.Loading then invalidateFiles(e)

    override x.OnReferenceAddedToProject(e) =
      base.OnReferenceAddedToProject(e)
      if not self.Loading then invalidateProjectFile()

    override x.OnReferenceRemovedFromProject(e) =
      base.OnReferenceRemovedFromProject(e)
      if not self.Loading then invalidateProjectFile()

    override x.OnDispose () =
      //if not self.Loading then invalidateProjectFile()

      // FIXME: is it correct to do it every time a project is disposed?
      //Should only be done on solution close
      //langServ.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
      base.OnDispose ()