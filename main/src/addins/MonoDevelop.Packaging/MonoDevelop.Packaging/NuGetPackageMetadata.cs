﻿//
// NuGetPackageMetadata.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
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

using MonoDevelop.Projects;
using MonoDevelop.Projects.MSBuild;

namespace MonoDevelop.Packaging
{
	class NuGetPackageMetadata
	{
		public string Id { get; set; }
		public string Version { get; set; }
		public string Authors { get; set; }
		public string Description { get; set; }

		public string Copyright { get; set; }
		public bool DevelopmentDependency { get; set; }
		public string IconUrl { get; set; }
		public string Language { get; set; }
		public string LicenseUrl { get; set; }
		public string Owners { get; set; }
		public string ProjectUrl { get; set; }
		public string ReleaseNotes { get; set; }
		public bool RequireLicenseAcceptance { get; set; }
		public string Summary { get; set; }
		public string Tags { get; set; }
		public string Title { get; set; }

		public void Load (DotNetProject project)
		{
			Load (project.MSBuildProject);
		}

		public void UpdateProject (DotNetProject project)
		{
			Update (project.MSBuildProject);
		}

		void Load (MSBuildProject project)
		{
			MSBuildPropertyGroup propertyGroup = GetPropertyGroup (project);
			Id = GetProperty (propertyGroup, "NuGetId");
			Version = GetProperty (propertyGroup, "NuGetVersion");
			Authors = GetProperty (propertyGroup, "NuGetAuthors");
			Description = GetProperty (propertyGroup, "NuGetDescription");
			DevelopmentDependency = GetProperty (propertyGroup, "NuGetDevelopmentDependency", false);
			IconUrl = GetProperty (propertyGroup, "NuGetIconUrl");
			Language = GetProperty (propertyGroup, "NuGetLanguage");
			LicenseUrl = GetProperty (propertyGroup, "NuGetLicenseUrl");
			Owners = GetProperty (propertyGroup, "NuGetOwners");
			ProjectUrl = GetProperty (propertyGroup, "NuGetProjectUrl");
			ReleaseNotes = GetProperty (propertyGroup, "NuGetReleaseNotes");
			RequireLicenseAcceptance = GetProperty (propertyGroup, "NuGetRequireLicenseAcceptance", false);
			Summary = GetProperty (propertyGroup, "NuGetSummary");
			Tags = GetProperty (propertyGroup, "NuGetTags");
			Title = GetProperty (propertyGroup, "NuGetTitle");
		}

		MSBuildPropertyGroup GetPropertyGroup (MSBuildProject project)
		{
			foreach (MSBuildPropertyGroup propertyGroup in project.PropertyGroups) {
				if (propertyGroup.HasProperty ("NuGetId"))
					return propertyGroup;
			}

			return project.GetGlobalPropertyGroup ();
		}

		string GetProperty (MSBuildPropertyGroup propertyGroup, string name)
		{
			return propertyGroup.GetProperty (name)?.Value;
		}

		bool GetProperty (MSBuildPropertyGroup propertyGroup, string name, bool defaultValue)
		{
			string value = GetProperty (propertyGroup, name);
			if (string.IsNullOrEmpty (value))
				return defaultValue;

			bool result = false;
			if (bool.TryParse (value, out result))
				return result;

			return defaultValue;
		}

		void Update (MSBuildProject project)
		{
			MSBuildPropertyGroup propertyGroup = GetPropertyGroup (project);
			SetProperty (propertyGroup, "NuGetId", Id);
			SetProperty (propertyGroup, "NuGetVersion", Version);
			SetProperty (propertyGroup, "NuGetAuthors", Authors);
			SetProperty (propertyGroup, "NuGetDescription", Description);
			SetProperty (propertyGroup, "NuGetDevelopmentDependency", DevelopmentDependency);
			SetProperty (propertyGroup, "NuGetLanguage", Language);
			SetProperty (propertyGroup, "NuGetLicenseUrl", LicenseUrl);
			SetProperty (propertyGroup, "NuGetOwners", Owners);
			SetProperty (propertyGroup, "NuGetProjectUrl", ProjectUrl);
			SetProperty (propertyGroup, "NuGetReleaseNotes", ReleaseNotes);
			SetProperty (propertyGroup, "NuGetSummary", Summary);
			SetProperty (propertyGroup, "NuGetTags", Tags);
			SetProperty (propertyGroup, "NuGetTitle", Title);
			SetProperty (propertyGroup, "NuGetRequireLicenseAcceptance", RequireLicenseAcceptance);
		}

		void SetProperty (MSBuildPropertyGroup propertyGroup, string name, string value)
		{
			if (string.IsNullOrEmpty (value))
				propertyGroup.RemoveProperty (name);
			else
				propertyGroup.SetValue (name, value);
		}

		void SetProperty (MSBuildPropertyGroup propertyGroup, string name, bool value)
		{
			if (value)
				propertyGroup.SetValue (name, value);
			else
				propertyGroup.RemoveProperty (name);
		}
	}
}

