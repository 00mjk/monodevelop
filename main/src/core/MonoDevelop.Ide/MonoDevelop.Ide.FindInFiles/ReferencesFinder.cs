// 
// ReferenceFinder.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;
using System.Linq;
using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.TypeSystem;

namespace MonoDevelop.Ide.FindInFiles
{
	// TODO: Find all references
	public abstract class ReferenceFinder
	{
		public bool IncludeDocumentation {
			get;
			set;
		}
		
		static List<ReferenceFinderCodon> referenceFinderCodons = new List<ReferenceFinderCodon> ();
		
		static ReferenceFinder ()
		{
			AddinManager.AddExtensionNodeHandler ("/MonoDevelop/Ide/ReferenceFinder", delegate(object sender, ExtensionNodeEventArgs args) {
				var codon = (ReferenceFinderCodon)args.ExtensionNode;
				switch (args.Change) {
				case ExtensionChange.Add:
					referenceFinderCodons.Add (codon);
					break;
				case ExtensionChange.Remove:
					referenceFinderCodons.Remove (codon);
					break;
				}
			});
		}
		
		static ReferenceFinder GetReferenceFinder (string mimeType)
		{
			var codon = referenceFinderCodons.FirstOrDefault (c => c.SupportedMimeTypes.Any (mt => mt == mimeType));
			return codon != null ? codon.CreateFinder () : null;
		}
		
		
		public static IEnumerable<MemberReference> FindReferences (object member, IProgressMonitor monitor = null)
		{
			return FindReferences (IdeApp.ProjectOperations.CurrentSelectedSolution, member, RefactoryScope.Unknown, monitor);
		}

		public static IEnumerable<MemberReference> FindReferences (object member, RefactoryScope scope, IProgressMonitor monitor = null)
		{
			return FindReferences (IdeApp.ProjectOperations.CurrentSelectedSolution, member, scope, monitor);
		}
		
		
		static IEnumerable<Tuple<IProjectContent, FilePath>> GetFileNames (Solution solution, IParsedFile unit, object member, RefactoryScope scope, IProgressMonitor monitor)
		{
			yield break;
/*			if (scope == RefactoryScope.Unknown)
				scope = GetScope (member);
			switch (scope) {
			case RefactoryScope.File:
			case RefactoryScope.DeclaringType:
				if (dom != null && unit != null)
					yield return Tuple.Create (dom, (FilePath)unit.FileName);
				break;
			case RefactoryScope.Project:
				if (dom == null)
					yield break;
				if (monitor != null)
					monitor.BeginTask (GettextCatalog.GetString ("Search reference in project..."), dom.GetProject ().Files.Count);
				int counter = 0;
				foreach (var file in dom.GetProject ().Files) {
					if (monitor != null && monitor.IsCancelRequested)
						yield break;
					yield return Tuple.Create (dom, file.FilePath);
					if (monitor != null) {
						if (counter % 10 == 0)
							monitor.Step (10);
						counter++;
					}
				}
				if (monitor != null)
					monitor.EndTask ();
				break;
			case RefactoryScope.Solution:
				if (monitor != null)
					monitor.BeginTask (GettextCatalog.GetString ("Search reference in solution..."), solution.GetAllProjects ().Count);
				var sourceProject = TypeSystemService.GetProject ((IEntity)member);
				foreach (var tuple in GetAllReferencingProjects (solution, sourceProject)) {
					if (monitor != null && monitor.IsCancelRequested)
						yield break;
					var project = tuple.Item1;
					var currentDom = tuple.Item2;
					if (project != null) {
						foreach (var file in project.Files) {
							if (monitor != null && monitor.IsCancelRequested)
								yield break;
							yield return Tuple.Create (currentDom, file.FilePath);
						}
					}
					if (monitor != null)
						monitor.Step (1);
				}
				if (monitor != null)
					monitor.EndTask ();
				break;
			}*/
		}
		
		public static List<Project> GetAllReferencingProjects (Solution solution, Project sourceProject)
		{
			var projects = new List<Project> ();
			projects.Add (sourceProject);
			foreach (var project in solution.GetAllProjects ()) {
				if (project.GetReferencedItems (ConfigurationSelector.Default).Any (prj => prj == sourceProject))
					projects.Add (project);
			}
			return projects;
		}
		
		public static IEnumerable<MemberReference> FindReferences (Solution solution, object member, RefactoryScope scope = RefactoryScope.Unknown, IProgressMonitor monitor = null)
		{
//			if (member == null)
				yield break;
			/*
//			IProjectContent dom = null;
			IParsedFile unit = null;
			IEnumerable<object> searchNodes = new [] { member };
			if (member is IVariable) { 
				var doc = IdeApp.Workbench.GetDocument (((IVariable)member).Region.FileName);
//				dom = doc.GetProjectContext ();
				unit = doc.ParsedDocument;
			} else if (member is IType) {
//				dom = ((IType)member).GetDefinition ().ProjectContent;
				unit = dom.GetFile (((IType)member).GetDefinition ().Region.FileName);
			} else if (member is IEntity) {
//				dom = ((IEntity)member).DeclaringTypeDefinition.ProjectContent;
				unit = dom.GetFile (((IEntity)member).DeclaringTypeDefinition.Region.FileName);
//				if (member is IMethod)
//					searchNodes = CollectMembers (dom, (IMethod)member);
			}
			
			// prepare references finder
			var preparedFinders = new Dictionary<string, Tuple<ReferenceFinder, List<Tuple<IProjectContent, FilePath>>>> ();
			foreach (var info in GetFileNames (solution, unit, member, scope, monitor)) {
				if (monitor != null && monitor.IsCancelRequested)
					yield break;
				
				string mime = DesktopService.GetMimeTypeForUri (info.Item2);
				
				Tuple<ReferenceFinder, List<Tuple<IProjectContent, FilePath>>> list;
				if (!preparedFinders.TryGetValue (mime, out list)) {
					var finder = GetReferenceFinder (mime);
					if (finder == null)
						continue;
					preparedFinders[mime] = list = Tuple.Create (finder, new List<Tuple<IProjectContent, FilePath>> ());
				}
				list.Item2.Add (info);
			}
			
			// execute search
			foreach (var tuple in preparedFinders.Values) {
				var finder = tuple.Item1;
				finder.SetSearchedMembers (searchNodes);
				finder.SetPossibleFiles (tuple.Item2);
				foreach (var foundReference in finder.FindReferences ()) {
					if (monitor != null && monitor.IsCancelRequested)
						yield break;
					yield return foundReference;
				}
			}*/
		}
		
		public abstract void SetSearchedMembers (IEnumerable<object> searchedMembers);
		public abstract void SetPossibleFiles (IEnumerable<Tuple<IProjectContent, FilePath>> files);
		public abstract IEnumerable<MemberReference> FindReferences ();
		
//		internal static IEnumerable<IEntity> CollectMembers (ITypeResolveContext dom, IMethod member)
//		{
//			if (member.IsConstructor) {
//				yield return member;
//				yield break;
//			}
//			
//			bool isOverrideable = member.DeclaringType.GetDefinition ().ClassType == ClassType.Interface || member.IsOverride || member.IsVirtual || member.IsAbstract;
//			bool isLastMember = false;
//			// for members we need to collect the whole 'class' of members (overloads & implementing types)
//			HashSet<string> alreadyVisitedTypes = new HashSet<string> ();
//			foreach (var type in member.DeclaringTypeDefinition.GetBaseTypes (dom)) {
//				if (type.GetDefinition ().ClassType == ClassType.Interface || isOverrideable || type.Equals (member.DeclaringType)) {
//					// search in the class for the member
//					foreach (var interfaceMember in type.GetDefinition ().Methods.Where (m => m.Name == member.Name)) {
//						yield return interfaceMember;
//					}
//					
//					// now search in all subclasses of this class for the member
//					isLastMember = !member.IsOverride;
//					foreach (var implementingType in type.GetBaseTypes (dom)) {
//						string name = implementingType.ReflectionName;
//						if (alreadyVisitedTypes.Contains (name))
//							continue;
//						alreadyVisitedTypes.Add (name);
//						foreach (var typeMember in implementingType.GetDefinition ().Methods.Where (m => m.Name == member.Name)) {
//							isLastMember = type.GetDefinition ().ClassType != ClassType.Interface && (typeMember.IsVirtual || typeMember.IsAbstract || !typeMember.IsOverride);
//							yield return typeMember;
//						}
//						if (!isOverrideable)
//							break;
//					}
//					if (isLastMember)
//						break;
//				}
//			}
//		}
		
		public enum RefactoryScope{ Unknown, File, DeclaringType, Solution, Project}
		static RefactoryScope GetScope (object o)
		{
			IEntity node = o as IEntity;
			if (node == null)
				return RefactoryScope.DeclaringType;
			
			if (node.DeclaringTypeDefinition != null && node.DeclaringTypeDefinition.Kind == TypeKind.Interface)
				return GetScope (node.DeclaringTypeDefinition);
			
			if ((node.Accessibility & Accessibility.Public) == Accessibility.Public)
				return RefactoryScope.Solution;
			
			// TODO: RefactoringsScope.Hierarchy
			if ((node.Accessibility & Accessibility.Protected) == Accessibility.Protected)
				return RefactoryScope.Solution;
			if ((node.Accessibility & Accessibility.Internal) == Accessibility.Protected)
				return RefactoryScope.Project;
			return RefactoryScope.DeclaringType;
		}
	}
	
	[ExtensionNode (Description="A reference finder. The specified class needs to inherit from MonoDevelop.Projects.CodeGeneration.ReferenceFinder")]
	internal class ReferenceFinderCodon : TypeExtensionNode
	{
		[NodeAttribute("supportedmimetypes", "Mime types supported by this binding (to be shown in the Open File dialog)")]
		string[] supportedMimetypes;
		
		public string[] SupportedMimeTypes {
			get {
				return supportedMimetypes;
			}
			set {
				supportedMimetypes = value;
			}
		}
		
		public ReferenceFinder CreateFinder ()
		{
			return (ReferenceFinder)CreateInstance ();
		}
		
		public override string ToString ()
		{
			return string.Format ("[ReferenceFinderCodon: SupportedMimeTypes={0}]", SupportedMimeTypes);
		}
	}
}
