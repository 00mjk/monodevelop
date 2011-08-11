// 
// NSObjectProjectInfo.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2011 Novell, Inc.
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
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MonoDevelop.Projects;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.TypeSystem;

namespace MonoDevelop.MacDev.ObjCIntegration
{
	public class NSObjectProjectInfo
	{
		Dictionary<string,NSObjectTypeInfo> objcTypes = new Dictionary<string,NSObjectTypeInfo> ();
		Dictionary<string,NSObjectTypeInfo> cliTypes = new Dictionary<string,NSObjectTypeInfo> ();
		
		NSObjectInfoService infoService;
		ITypeResolveContext dom;
		DotNetProject project;
		bool needsUpdating;
		
		public NSObjectProjectInfo (DotNetProject project, ITypeResolveContext dom, NSObjectInfoService infoService)
		{
			this.infoService = infoService;
			this.dom = dom;
			needsUpdating = true;
		}
		
		internal void SetNeedsUpdating ()
		{
			needsUpdating = true;
		}
		
		internal void Update (bool force)
		{
			SetNeedsUpdating ();
			Update ();
		}
		
		static IEnumerable<DotNetProject> GetReferencedProjects (DotNetProject project)
		{
			// is there an easier way doing that ?
			foreach (var r in project.References.Where (rf => rf.ReferenceType == ReferenceType.Project)) {
				var refProject = project.ParentSolution.GetAllProjects ().First (p => p.Name == r.Reference) as DotNetProject;
				if (refProject != null)
					yield return refProject;
			}
		}
		
		internal void Update ()
		{
			if (!needsUpdating)
				return;
			
			foreach (var r in GetReferencedProjects (project)) {
				var info = infoService.GetProjectInfo (r);
				if (info != null)
					info.Update ();
			}
			
			objcTypes.Clear ();
			cliTypes.Clear ();
			
			dom = TypeSystemService.GetContext (project);
			
			foreach (var type in infoService.GetRegisteredObjects (dom)) {
				objcTypes.Add (type.ObjCName, type);
				cliTypes.Add (type.CliName.FullName, type);
			}
			
			foreach (var type in objcTypes.Values) {
				ResolveTypes (type);
			}
			
			needsUpdating = false;
		}
		
		public IEnumerable<NSObjectTypeInfo> GetTypes ()
		{
			return objcTypes.Values;
		}
		
		public NSObjectTypeInfo GetType (string objcName)
		{
			NSObjectTypeInfo ret;
			if (objcTypes.TryGetValue (objcName, out ret))
				return ret;
			return null;
		}
		
		internal void InsertUpdatedType (NSObjectTypeInfo type)
		{
			objcTypes[type.ObjCName] = type;
			cliTypes[type.CliName.FullName] = type;
		}
		
		bool TryResolveCliToObjc (string cliType, out NSObjectTypeInfo resolved)
		{
			if (cliTypes.TryGetValue (cliType, out resolved))
				return true;
			foreach (var r in GetReferencedProjects (project)) {
				var rDom = infoService.GetProjectInfo (r);
				if (rDom != null && rDom.cliTypes.TryGetValue (cliType, out resolved))
					return true;
			}
			resolved = null;
			return false;
		}
		
		bool TryResolveObjcToCli (string objcType, out NSObjectTypeInfo resolved)
		{
			if (objcTypes.TryGetValue (objcType, out resolved))
				return true;
			foreach (var r in GetReferencedProjects (project)) {
				var rDom = infoService.GetProjectInfo (r);
				if (rDom != null && rDom.objcTypes.TryGetValue (objcType, out resolved))
					return true;
			}
			resolved = null;
			return false;
		}
		
		public void ResolveTypes (NSObjectTypeInfo type)
		{
			NSObjectTypeInfo resolved;
			if (type.BaseObjCType == null && type.BaseCliType != null) {
				var baseCliType = type.BaseCliType.Resolve (dom);
				if (TryResolveCliToObjc (baseCliType.FullName, out resolved)) {
					if (resolved.IsModel)
						type.BaseIsModel = true;
					type.BaseObjCType = resolved.ObjCName;
					//FIXME: handle type references better
					if (resolved.IsUserType)
						type.UserTypeReferences.Add (resolved.ObjCName);
				} else {
					//managed classes many have implicitly registered base classes with a name not
					//expressible in obj-c. In this case, the best we can do is walk down the 
					//hierarchy until we find a valid base class
					foreach (var bt in baseCliType.GetAllBaseTypeDefinitions (dom)) {
						if (bt.Kind != TypeKind.Class) 
							continue;
						if (TryResolveCliToObjc (bt.FullName, out resolved)) {
							if (resolved.IsModel)
								type.BaseIsModel = true;
							type.BaseObjCType = resolved.ObjCName;
							if (resolved.IsUserType)
								type.UserTypeReferences.Add (resolved.ObjCName);
							break;
						}
					}
				}
			}
			
			if (type.BaseCliType == null && type.BaseObjCType != null) {
				if (TryResolveObjcToCli (type.BaseObjCType, out resolved))
					type.BaseCliType = resolved.CliName;
			}
			
			foreach (var outlet in type.Outlets) {
				if (outlet.ObjCType == null) {
					if (TryResolveCliToObjc (outlet.CliType, out resolved)) {
						outlet.ObjCType = resolved.ObjCName;
						if (resolved.IsUserType)
							type.UserTypeReferences.Add (resolved.ObjCName);
					}
				}
				if (outlet.CliType == null) {
					if (TryResolveObjcToCli (outlet.ObjCType, out resolved))
						outlet.CliType = resolved.CliName.FullName;
				}
			}
			
			foreach (var action in type.Actions) {
				foreach (var param in action.Parameters) {
					if (param.ObjCType == null) {
						if (TryResolveCliToObjc (param.CliType, out resolved)) {
							param.ObjCType = resolved.ObjCName;
							if (resolved.IsUserType)
								type.UserTypeReferences.Add (resolved.ObjCName);
						}
					}
					if (param.CliType == null) {
						if (TryResolveObjcToCli (param.ObjCType, out resolved))
							param.CliType = resolved.CliName.FullName;
					}
				}
			}
		}
	}
}
