// 
// ProjectService.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;

using MonoDevelop.Core;
using Mono.Addins;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Projects.Extensions;
using Mono.Unix;
using System.Linq;
using MonoDevelop.Projects.Formats.MSBuild;
using System.Threading.Tasks;

namespace MonoDevelop.Projects
{
	public class ProjectService
	{
		DataContext dataContext = new DataContext ();
		ProjectServiceExtension defaultExtensionChain;
		DefaultProjectServiceExtension extensionChainTerminator = new DefaultProjectServiceExtension ();
		
		FileFormatManager formatManager = new FileFormatManager ();
		FileFormat defaultFormat;
		TargetFramework defaultTargetFramework;
		
		string defaultPlatformTarget = "x86";
		static readonly TargetFrameworkMoniker DefaultTargetFrameworkId = TargetFrameworkMoniker.NET_4_5;
		
		public const string BuildTarget = "Build";
		public const string CleanTarget = "Clean";
		
		const string FileFormatsExtensionPath = "/MonoDevelop/ProjectModel/FileFormats";
		const string SerializableClassesExtensionPath = "/MonoDevelop/ProjectModel/SerializableClasses";
		const string ExtendedPropertiesExtensionPath = "/MonoDevelop/ProjectModel/ExtendedProperties";
		const string ProjectBindingsExtensionPath = "/MonoDevelop/ProjectModel/ProjectBindings";

		internal const string ProjectModelExtensionsPath = "/MonoDevelop/ProjectModel/ProjectModelExtensions";

		internal event EventHandler DataContextChanged;
		
		class ExtensionChainInfo
		{
			public ExtensionContext ExtensionContext;
			public ItemTypeCondition ItemTypeCondition;
			public ProjectLanguageCondition ProjectLanguageCondition;
		}
		
		internal ProjectService ()
		{
			AddinManager.AddExtensionNodeHandler (FileFormatsExtensionPath, OnFormatExtensionChanged);
			AddinManager.AddExtensionNodeHandler (SerializableClassesExtensionPath, OnSerializableExtensionChanged);
			AddinManager.AddExtensionNodeHandler (ExtendedPropertiesExtensionPath, OnPropertiesExtensionChanged);
			AddinManager.ExtensionChanged += OnExtensionChanged;
			
			defaultFormat = formatManager.GetFileFormat (MSBuildProjectService.DefaultFormat);
		}
		
		public DataContext DataContext {
			get { return dataContext; }
		}
		
		public FileFormatManager FileFormats {
			get { return formatManager; }
		}
		
		internal ProjectServiceExtension GetExtensionChain (WorkspaceObject target)
		{
			ProjectServiceExtension chain;
			if (target != null) {
				lock (target) {
					ExtensionChainInfo einfo = (ExtensionChainInfo)target.ExtendedProperties [typeof(ExtensionChainInfo)];
					if (einfo == null) {
						einfo = new ExtensionChainInfo ();
						ExtensionContext ctx = AddinManager.CreateExtensionContext ();
						einfo.ExtensionContext = ctx;
						einfo.ItemTypeCondition = new ItemTypeCondition (target.GetType ());
						einfo.ProjectLanguageCondition = new ProjectLanguageCondition (target);
						ctx.RegisterCondition ("ItemType", einfo.ItemTypeCondition);
						ctx.RegisterCondition ("ProjectLanguage", einfo.ProjectLanguageCondition);
						target.ExtendedProperties [typeof(ExtensionChainInfo)] = einfo;
					} else {
						einfo.ItemTypeCondition.ObjType = target.GetType ();
						einfo.ProjectLanguageCondition.TargetProject = target;
					}
					ProjectServiceExtension[] extensions = einfo.ExtensionContext.GetExtensionObjects<ProjectServiceExtension> ("/MonoDevelop/ProjectModel/ProjectServiceExtensions");
					chain = CreateExtensionChain (extensions);
				
					// After creating the chain there is no need to keep the reference to the target
					einfo.ProjectLanguageCondition.TargetProject = null;
				}
			}
			else {
				if (defaultExtensionChain == null) {
					ExtensionContext ctx = AddinManager.CreateExtensionContext ();
					ctx.RegisterCondition ("ItemType", new ItemTypeCondition (typeof(UnknownItem)));
					ctx.RegisterCondition ("ProjectLanguage", new ProjectLanguageCondition (UnknownItem.Instance));
					ProjectServiceExtension[] extensions = ctx.GetExtensionObjects<ProjectServiceExtension> ("/MonoDevelop/ProjectModel/ProjectServiceExtensions");
					defaultExtensionChain = CreateExtensionChain (extensions);
				}
				chain = defaultExtensionChain;
				target = UnknownItem.Instance;
			}
			
			if (chain.SupportsItem (target))
				return chain;
			else
				return chain.GetNext (target);
		}
		
		ProjectServiceExtension CreateExtensionChain (ProjectServiceExtension[] extensions)
		{
			if (extensions.Length > 0) {
				for (int n=0; n<extensions.Length - 1; n++)
					extensions [n].Next = extensions [n + 1];
				extensions [extensions.Length - 1].Next = extensionChainTerminator;
				return extensions [0];
			} else {
				return extensionChainTerminator;
			}
		}
		
		public string DefaultPlatformTarget {
			get { return defaultPlatformTarget; }
			set { defaultPlatformTarget = value; }
		}

		public TargetFramework DefaultTargetFramework {
			get {
				if (defaultTargetFramework == null)
					defaultTargetFramework = Runtime.SystemAssemblyService.GetTargetFramework (DefaultTargetFrameworkId); 
				return defaultTargetFramework;
			}
			set {
				defaultTargetFramework = value;
			}
		}

		public FileFormat DefaultFileFormat {
			get { return defaultFormat; }
		}

		internal FileFormat GetDefaultFormat (object ob)
		{
			if (defaultFormat.CanWrite (ob))
				return defaultFormat;
			FileFormat[] formats = FileFormats.GetFileFormatsForObject (ob);
			if (formats.Length == 0)
				throw new InvalidOperationException ("Can't handle objects of type '" + ob.GetType () + "'");
			return formats [0];
		}
		
		public Task<SolutionItem> ReadSolutionItem (ProgressMonitor monitor, string file)
		{
			using (var ctx = new SolutionLoadContext (null))
				return ReadSolutionItem (monitor, file, null, null, null, ctx);
		}
		
		public Task<SolutionItem> ReadSolutionItem (ProgressMonitor monitor, string file, MSBuildFileFormat format, string typeGuid = null, string itemGuid = null, SolutionLoadContext ctx = null)
		{
			return Runtime.RunInMainThread (async delegate {
				file = Path.GetFullPath (file);
				using (Counters.ReadSolutionItem.BeginTiming ("Read project " + file)) {
					file = GetTargetFile (file);
					SolutionItem loadedItem = await GetExtensionChain (null).LoadSolutionItem (monitor, ctx, file, format, typeGuid, itemGuid);
					loadedItem.NeedsReload = false;
					return loadedItem;
				}
			});
		}

		public Task<SolutionFolderItem> ReadSolutionItem (ProgressMonitor monitor, SolutionItemReference reference, params WorkspaceItem[] workspaces)
		{
			return Runtime.RunInMainThread (async delegate {
				if (reference.Id == null) {
					FilePath file = reference.Path.FullPath;
					foreach (WorkspaceItem workspace in workspaces) {
						foreach (SolutionItem eitem in workspace.GetAllItems<Solution>().SelectMany (s => s.GetAllSolutionItems ()))
							if (file == eitem.FileName)
								return eitem;
					}
					return await ReadSolutionItem (monitor, reference.Path);
				} else {
					Solution sol = null;
					if (workspaces.Length > 0) {
						FilePath file = reference.Path.FullPath;
						foreach (WorkspaceItem workspace in workspaces) {
							foreach (Solution item in workspace.GetAllItems<Solution>()) {
								if (item.FileName.FullPath == file) {
									sol = item;
									break;
								}
							}
							if (sol != null)
								break;
						}
					}
					if (sol == null)
						sol = await ReadWorkspaceItem (monitor, reference.Path) as Solution;
					
					if (reference.Id == ":root:")
						return sol.RootFolder;
					else
						return sol.GetSolutionItem (reference.Id);
				}
			});
		}
		
		public Task<WorkspaceItem> ReadWorkspaceItem (ProgressMonitor monitor, string file)
		{
			return Runtime.RunInMainThread (async delegate {
				file = Path.GetFullPath (file);
				using (Counters.ReadWorkspaceItem.BeginTiming ("Read solution " + file)) {
					file = GetTargetFile (file);
					WorkspaceItem item = await GetExtensionChain (null).LoadWorkspaceItem (monitor, file) as WorkspaceItem;
					if (item != null)
						item.NeedsReload = false;
					else
						throw new InvalidOperationException ("Invalid file format: " + file);
					return item;
				}
			});
		}
		
		internal async Task<WorkspaceItem> InternalReadWorkspaceItem (string file, ProgressMonitor monitor)
		{
			var res = await ReadFile (monitor, file, typeof(WorkspaceItem));
			WorkspaceItem item = res.Item1 as WorkspaceItem;
			
			if (item == null)
				throw new InvalidOperationException ("Invalid file format: " + file);
			
			if (!item.FormatSet)
				await item.ConvertToFormat (res.Item2, false);

			return item;
		}
		
		internal async Task InternalWriteWorkspaceItem (ProgressMonitor monitor, FilePath file, WorkspaceItem item)
		{
			var newFile = await WriteFile (monitor, file, item, item.FileFormat);
			if (newFile != null)
				item.FileName = newFile;
			else
				throw new InvalidOperationException ("FileFormat not provided for workspace item '" + item.Name + "'");
		}
		
		async Task<Tuple<object,FileFormat>> ReadFile (ProgressMonitor monitor, string file, Type expectedType)
		{
			FileFormat[] formats = formatManager.GetFileFormats (file, expectedType);

			if (formats.Length == 0)
				throw new InvalidOperationException ("Unknown file format: " + file);
			
			var format = formats [0];
			object obj = await format.Format.ReadFile (file, expectedType, monitor);
			if (obj == null)
				throw new InvalidOperationException ("Invalid file format: " + file);

			return new Tuple<object,FileFormat> (obj, format);
		}
		
		async Task<FilePath> WriteFile (ProgressMonitor monitor, FilePath file, object item, FileFormat format)
		{
			if (format == null) {
				if (defaultFormat.CanWrite (item))
					format = defaultFormat;
				else {
					FileFormat[] formats = formatManager.GetFileFormatsForObject (item);
					format = formats.Length > 0 ? formats [0] : null;
				}
				
				if (format == null)
					return null;

				file = format.GetValidFileName (item, file);
			}
			
			FileService.RequestFileEdit (file);

			await format.Format.WriteFile (file, item, monitor);
			return file;
		}
		
		public Task<string> Export (ProgressMonitor monitor, string rootSourceFile, string targetPath, FileFormat format)
		{
			rootSourceFile = GetTargetFile (rootSourceFile);
			return Export (monitor, rootSourceFile, null, targetPath, format);
		}
		
		public async Task<string> Export (ProgressMonitor monitor, string rootSourceFile, string[] includedChildIds, string targetPath, FileFormat format)
		{
			IWorkspaceFileObject obj;
			
			if (IsWorkspaceItemFile (rootSourceFile)) {
				obj = await ReadWorkspaceItem (monitor, rootSourceFile) as Solution;
			} else {
				obj = await ReadSolutionItem (monitor, rootSourceFile);
				if (obj == null)
					throw new InvalidOperationException ("File is not a solution or project.");
			}
			using (obj) {
				return await Export (monitor, obj, includedChildIds, targetPath, format);
			}
		}
		
		async Task<string> Export (ProgressMonitor monitor, IWorkspaceFileObject obj, string[] includedChildIds, string targetPath, FileFormat format)
		{
			string rootSourceFile = obj.FileName;
			string sourcePath = Path.GetFullPath (Path.GetDirectoryName (rootSourceFile));
			targetPath = Path.GetFullPath (targetPath);
			
			if (sourcePath != targetPath) {
				if (!CopyFiles (monitor, obj, obj.GetItemFiles (true), targetPath, true))
					return null;
				
				string newFile = Path.Combine (targetPath, Path.GetFileName (rootSourceFile));
				if (IsWorkspaceItemFile (rootSourceFile))
					obj = await ReadWorkspaceItem (monitor, newFile);
				else
					obj = (SolutionItem) await ReadSolutionItem (monitor, newFile);
				
				using (obj) {
					var oldFiles = obj.GetItemFiles (true).ToList ();
					ExcludeEntries (obj, includedChildIds);
					if (format != null)
						await obj.ConvertToFormat (format, true);
					await obj.SaveAsync (monitor);
					var newFiles = obj.GetItemFiles (true);
					
					foreach (FilePath f in newFiles) {
						if (!f.IsChildPathOf (targetPath)) {
							if (obj is Solution)
								monitor.ReportError ("The solution '" + obj.Name + "' is referencing the file '" + f.FileName + "' which is located outside the root solution directory.", null);
							else
								monitor.ReportError ("The project '" + obj.Name + "' is referencing the file '" + f.FileName + "' which is located outside the project directory.", null);
						}
						oldFiles.Remove (f);
					}
	
					// Remove old files
					foreach (FilePath file in oldFiles) {
						if (File.Exists (file)) {
							File.Delete (file);
						
							// Exclude empty directories
							FilePath dir = file.ParentDirectory;
							if (Directory.GetFiles (dir).Length == 0 && Directory.GetDirectories (dir).Length == 0) {
								try {
									Directory.Delete (dir);
								} catch (Exception ex) {
									monitor.ReportError (null, ex);
								}
							}
						}
					}
					return obj.FileName;
				}
			}
			else {
				using (obj) {
					ExcludeEntries (obj, includedChildIds);
					if (format != null)
						await obj.ConvertToFormat (format, true);
					await obj.SaveAsync (monitor);
					return obj.FileName;
				}
			}
		}
		
		void ExcludeEntries (IWorkspaceFileObject obj, string[] includedChildIds)
		{
			Solution sol = obj as Solution;
			if (sol != null && includedChildIds != null) {
				// Remove items not to be exported.
				
				Dictionary<string,string> childIds = new Dictionary<string,string> ();
				foreach (string it in includedChildIds)
					childIds [it] = it;
				
				foreach (SolutionFolderItem item in sol.GetAllItems<SolutionFolderItem> ()) {
					if (!childIds.ContainsKey (item.ItemId) && item.ParentFolder != null)
						item.ParentFolder.Items.Remove (item);
				}
			}
		}

		bool CopyFiles (ProgressMonitor monitor, IWorkspaceFileObject obj, IEnumerable<FilePath> files, FilePath targetBasePath, bool ignoreExternalFiles)
		{
			FilePath baseDir = obj.BaseDirectory.FullPath;
			foreach (FilePath file in files) {

				if (!File.Exists (file)) {
					monitor.ReportWarning (GettextCatalog.GetString ("File '{0}' not found.", file));
					continue;
				}
				FilePath fname = file.FullPath;
				
				// Can't export files from outside the root solution directory
				if (!fname.IsChildPathOf (baseDir)) {
					if (ignoreExternalFiles)
						continue;
					if (obj is Solution)
						monitor.ReportError ("The solution '" + obj.Name + "' is referencing the file '" + Path.GetFileName (file) + "' which is located outside the root solution directory.", null);
					else
						monitor.ReportError ("The project '" + obj.Name + "' is referencing the file '" + Path.GetFileName (file) + "' which is located outside the project directory.", null);
					return false;
				}

				FilePath rpath = fname.ToRelative (baseDir);
				rpath = rpath.ToAbsolute (targetBasePath);
				
				if (!Directory.Exists (rpath.ParentDirectory))
					Directory.CreateDirectory (rpath.ParentDirectory);

				File.Copy (file, rpath, true);
			}
			return true;
		}
		
		public DotNetProject CreateDotNetProject (string language, params string[] typeGuids)
		{
			string typeGuid = MSBuildProjectService.GetLanguageGuid (language);
			return (DotNetProject) MSBuildProjectService.CreateProject (typeGuid, typeGuids);
		}

		public Project CreateProject (string typeGuid, params string[] typeGuids)
		{
			return MSBuildProjectService.CreateProject (typeGuid, typeGuids);
		}

		public Project CreateProject (string typeAlias, ProjectCreateInformation info, XmlElement projectOptions)
		{
			return MSBuildProjectService.CreateSolutionItem (typeAlias, info, projectOptions) as Project;
		}

		public bool CanCreateProject (string typeAlias)
		{
			// TODO NPM: review
			return MSBuildProjectService.CanCreateSolutionItem (typeAlias, null, null);
		}

		public bool CanCreateProject (string typeAlias, ProjectCreateInformation info, XmlElement projectOptions)
		{
			return MSBuildProjectService.CanCreateSolutionItem (typeAlias, info, projectOptions);
		}

		//TODO: find solution that contains the project if possible
		public async Task<Solution> GetWrapperSolution (ProgressMonitor monitor, string filename)
		{
			// First of all, check if a solution with the same name already exists
			
			FileFormat[] formats = Services.ProjectService.FileFormats.GetFileFormats (filename, typeof(SolutionItem));
			if (formats.Length == 0)
				formats = new  [] { DefaultFileFormat };
			
			Solution tempSolution = new Solution ();
			
			FileFormat solutionFileFormat = formats.FirstOrDefault (f => f.CanWrite (tempSolution)) ?? DefaultFileFormat;
			
			string solFileName = solutionFileFormat.GetValidFileName (tempSolution, filename);
			
			if (File.Exists (solFileName)) {
				return (Solution) await Services.ProjectService.ReadWorkspaceItem (monitor, solFileName);
			}
			else {
				// Create a temporary solution and add the project to the solution
				tempSolution.SetLocation (Path.GetDirectoryName (filename), Path.GetFileNameWithoutExtension (filename));
				SolutionItem sitem = await Services.ProjectService.ReadSolutionItem (monitor, filename);
				await tempSolution.ConvertToFormat (solutionFileFormat, false);
				tempSolution.RootFolder.Items.Add (sitem);
				tempSolution.CreateDefaultConfigurations ();
				await tempSolution.SaveAsync (monitor);
				return tempSolution;
			}
		}
		
		public bool IsSolutionItemFile (string filename)
		{
			if (filename.StartsWith ("file://"))
				filename = new Uri(filename).LocalPath;
			filename = GetTargetFile (filename);
			return GetExtensionChain (null).IsSolutionItemFile (filename);
		}
		
		public bool IsWorkspaceItemFile (string filename)
		{
			if (filename.StartsWith ("file://"))
				filename = new Uri(filename).LocalPath;
			filename = GetTargetFile (filename);
			return GetExtensionChain (null).IsWorkspaceItemFile (filename);
		}
		
		internal bool IsSolutionItemFileInternal (string filename)
		{
			return formatManager.GetFileFormats (filename, typeof(SolutionFolderItem)).Length > 0;
		}
		
		internal bool IsWorkspaceItemFileInternal (string filename)
		{
			return formatManager.GetFileFormats (filename, typeof(WorkspaceItem)).Length > 0;
		}
		
		internal void InitializeDataContext (DataContext ctx)
		{
			foreach (DataTypeCodon dtc in AddinManager.GetExtensionNodes (SerializableClassesExtensionPath)) {
				ctx.IncludeType (dtc.Addin, dtc.TypeName, dtc.ItemName);
			}
			foreach (ItemPropertyCodon cls in AddinManager.GetExtensionNodes (ExtendedPropertiesExtensionPath)) {
				ctx.RegisterProperty (cls.Addin, cls.TypeName, cls.PropertyName, cls.PropertyTypeName, cls.External, cls.SkipEmpty);
			}
		}

		void OnFormatExtensionChanged (object s, ExtensionNodeEventArgs args)
		{
			FileFormatNode node = (FileFormatNode) args.ExtensionNode;
			if (args.Change == ExtensionChange.Add)
				formatManager.RegisterFileFormat ((IFileFormat) args.ExtensionObject, node.Id, node.Name, node.CanDefault);
			else
				formatManager.UnregisterFileFormat ((IFileFormat) args.ExtensionObject);
		}
		
		void OnSerializableExtensionChanged (object s, ExtensionNodeEventArgs args)
		{
			if (args.Change == ExtensionChange.Add) {
				DataTypeCodon t = (DataTypeCodon) args.ExtensionNode;
				DataContext.IncludeType (t.Addin, t.TypeName, t.ItemName);
			}
			// Types can't be excluded from a DataContext, but that's not a big problem anyway
			
			if (DataContextChanged != null)
				DataContextChanged (this, EventArgs.Empty);
		}
		
		void OnPropertiesExtensionChanged (object s, ExtensionNodeEventArgs args)
		{
			if (args.Change == ExtensionChange.Add) {
				ItemPropertyCodon cls = (ItemPropertyCodon) args.ExtensionNode;
				DataContext.RegisterProperty (cls.Addin, cls.TypeName, cls.PropertyName, cls.PropertyTypeName, cls.External, cls.SkipEmpty);
			}
			else {
				ItemPropertyCodon cls = (ItemPropertyCodon) args.ExtensionNode;
				DataContext.UnregisterProperty (cls.Addin, cls.TypeName, cls.PropertyName);
			}
			
			if (DataContextChanged != null)
				DataContextChanged (this, EventArgs.Empty);
		}
		
		void OnExtensionChanged (object s, ExtensionEventArgs args)
		{
			if (args.PathChanged ("/MonoDevelop/ProjectModel/ProjectServiceExtensions"))
				defaultExtensionChain = null;
		}
		
		string GetTargetFile (string file)
		{
			if (!Platform.IsWindows) {
				try {
					UnixSymbolicLinkInfo fi = new UnixSymbolicLinkInfo (file);
					if (fi.IsSymbolicLink)
						return fi.ContentsPath;
				} catch {
				}
			}
			return file;
		}
	}
	
	internal class DefaultProjectServiceExtension: ProjectServiceExtension
	{
		public override bool IsSolutionItemFile (string filename)
		{
			return Services.ProjectService.IsSolutionItemFileInternal (filename);
		}
		
		public override bool IsWorkspaceItemFile (string filename)
		{
			return Services.ProjectService.IsWorkspaceItemFileInternal (filename);
		}
		
		public override Task<SolutionItem> LoadSolutionItem (ProgressMonitor monitor, SolutionLoadContext ctx, string fileName, MSBuildFileFormat expectedFormat, string typeGuid, string itemGuid)
		{
			return MSBuildProjectService.LoadItem (monitor, fileName, expectedFormat, typeGuid, itemGuid, ctx);
		}
		
		public override Task<WorkspaceItem> LoadWorkspaceItem (ProgressMonitor monitor, string fileName)
		{
			return Services.ProjectService.InternalReadWorkspaceItem (fileName, monitor);
		}
	}	
	
	internal static class Counters
	{
		public static Counter ItemsInMemory = InstrumentationService.CreateCounter ("Projects in memory", "Project Model");
		public static Counter ItemsLoaded = InstrumentationService.CreateCounter ("Projects loaded", "Project Model");
		public static Counter SolutionsInMemory = InstrumentationService.CreateCounter ("Solutions in memory", "Project Model");
		public static Counter SolutionsLoaded = InstrumentationService.CreateCounter ("Solutions loaded", "Project Model");
		public static TimerCounter ReadWorkspaceItem = InstrumentationService.CreateTimerCounter ("Workspace item read", "Project Model", id:"Core.ReadWorkspaceItem");
		public static TimerCounter ReadSolutionItem = InstrumentationService.CreateTimerCounter ("Solution item read", "Project Model");
		public static TimerCounter ReadMSBuildProject = InstrumentationService.CreateTimerCounter ("MSBuild project read", "Project Model");
		public static TimerCounter WriteMSBuildProject = InstrumentationService.CreateTimerCounter ("MSBuild project written", "Project Model");
		public static TimerCounter BuildSolutionTimer = InstrumentationService.CreateTimerCounter ("Solution built", "Project Model");
		public static TimerCounter BuildProjectTimer = InstrumentationService.CreateTimerCounter ("Project built", "Project Model");
		public static TimerCounter BuildWorkspaceItemTimer = InstrumentationService.CreateTimerCounter ("Workspace item built", "Project Model");
		public static TimerCounter NeedsBuildingTimer = InstrumentationService.CreateTimerCounter ("Needs building checked", "Project Model");
		
		public static TimerCounter HelpServiceInitialization = InstrumentationService.CreateTimerCounter ("Help Service initialization", "IDE");
		public static TimerCounter ParserServiceInitialization = InstrumentationService.CreateTimerCounter ("Parser Service initialization", "IDE");
	}
}
