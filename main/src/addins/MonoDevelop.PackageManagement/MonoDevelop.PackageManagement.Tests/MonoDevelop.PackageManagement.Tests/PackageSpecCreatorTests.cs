﻿//
// PackageSpecCreatorTests.cs
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

using System.Linq;
using MonoDevelop.PackageManagement.Tests.Helpers;
using NUnit.Framework;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace MonoDevelop.PackageManagement.Tests
{
	[TestFixture]
	public class PackageSpecCreatorTests
	{
		PackageSpec spec;
		FakeDotNetProject project;

		void CreateProject (string name, string fileName = @"d:\projects\MyProject\MyProject.csproj")
		{
			project = new FakeDotNetProject (fileName.ToNativePath ());
			project.Name = name;
		}

		void AddTargetFramework (string targetFramework)
		{
			project.AddTargetFramework (targetFramework);
		}

		void CreatePackageSpec ()
		{
			spec = PackageSpecCreator.CreatePackageSpec (project);
		}

		void AddPackageReference (string id, string version)
		{
			var packageReference = new TestableProjectPackageReference (id, version);
			project.PackageReferences.Add (packageReference);
		}

		[Test]
		public void CreatePackageSpec_NewProject_BaseIntermediatePathUsedForProjectAssetsJsonFile ()
		{
			CreateProject ("MyProject", @"d:\projects\MyProject\MyProject.csproj");
			project.BaseIntermediateOutputPath = @"d:\projects\MyProject\obj".ToNativePath ();
			AddTargetFramework ("netcoreapp1.0");
			string expectedAssetsFilePath = @"d:\projects\MyProject\obj\project.assets.json".ToNativePath ();

			CreatePackageSpec ();

			Assert.AreEqual (expectedAssetsFilePath, spec.FilePath);
			Assert.AreEqual ("MyProject", spec.Name);
			Assert.AreEqual ("1.0.0", spec.Version.ToString ());
			Assert.AreEqual (RestoreOutputType.NETCore, spec.RestoreMetadata.OutputType);
			Assert.AreEqual ("MyProject", spec.RestoreMetadata.ProjectName);
			Assert.AreEqual (project.FileName.ToString (), spec.RestoreMetadata.ProjectPath);
			Assert.AreEqual (project.FileName.ToString (), spec.RestoreMetadata.ProjectUniqueName);
			Assert.AreEqual (project.BaseIntermediateOutputPath.ToString (), spec.RestoreMetadata.OutputPath);
			Assert.AreEqual ("netcoreapp1.0", spec.RestoreMetadata.OriginalTargetFrameworks.Single ());
		}

		[Test]
		public void CreatePackageSpec_OnePackageReference_PackageReferencedAddedToPackageSpec ()
		{
			CreateProject ("MyProject", @"d:\projects\MyProject\MyProject.csproj");
			AddTargetFramework ("netcoreapp1.0");
			AddPackageReference ("Newtonsoft.Json", "9.0.1");

			CreatePackageSpec ();

			var targetFramework = spec.TargetFrameworks.Single ();
			var dependency = targetFramework.Dependencies.Single ();
			Assert.AreEqual ("MyProject", spec.Name);
			Assert.AreEqual (RestoreOutputType.NETCore, spec.RestoreMetadata.OutputType);
			Assert.AreEqual ("MyProject", spec.RestoreMetadata.ProjectName);
			Assert.AreEqual ("netcoreapp1.0", spec.RestoreMetadata.OriginalTargetFrameworks.Single ());
			Assert.AreEqual (".NETCoreApp,Version=v1.0", targetFramework.FrameworkName.ToString ());
			Assert.AreEqual ("Newtonsoft.Json", dependency.Name);
			Assert.AreEqual (LibraryDependencyType.Default, dependency.Type);
			Assert.AreEqual (LibraryIncludeFlags.All, dependency.IncludeType);
			Assert.AreEqual (LibraryIncludeFlagUtils.DefaultSuppressParent, dependency.SuppressParent);
			Assert.AreEqual ("[9.0.1, )", dependency.LibraryRange.VersionRange.ToString ());
			Assert.AreEqual (LibraryDependencyTarget.Package, dependency.LibraryRange.TypeConstraint);
			Assert.AreEqual ("Newtonsoft.Json", dependency.LibraryRange.Name);
		}
	}
}
