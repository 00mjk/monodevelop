﻿
//
// GtkNuGetPackageMetadataOptionsPanelWidget.cs
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

using System;
using MonoDevelop.Projects;

namespace MonoDevelop.Packaging.Gui
{
	[System.ComponentModel.ToolboxItem (true)]
	public partial class GtkNuGetPackageMetadataOptionsPanelWidget : Gtk.Bin
	{
		NuGetPackageMetadata metadata;

		public GtkNuGetPackageMetadataOptionsPanelWidget ()
		{
			this.Build ();
		}

		internal void Load (PackagingProject project)
		{
			metadata = project.GetPackageMetadata ();
			LoadMetadata ();
		}

		internal void Load (DotNetProject project)
		{
			metadata = new NuGetPackageMetadata ();
			metadata.Load (project);
			LoadMetadata ();
		}

		void LoadMetadata ()
		{
			packageIdTextBox.Text = GetTextBoxText (metadata.Id);
			packageVersionTextBox.Text = GetTextBoxText (metadata.Version);
			packageAuthorsTextBox.Text = GetTextBoxText (metadata.Authors);
			packageDescriptionTextView.Buffer.Text = GetTextBoxText (metadata.Description);

			packageCopyrightTextBox.Text = GetTextBoxText (metadata.Copyright);
			packageDevelopmentDependencyCheckBox.Active = metadata.DevelopmentDependency;
			packageIconUrlTextBox.Text = GetTextBoxText (metadata.IconUrl);
			packageLanguageTextBox.Text = GetTextBoxText (metadata.Language);
			packageLicenseUrlTextBox.Text = GetTextBoxText (metadata.LicenseUrl);
			packageOwnersTextBox.Text = GetTextBoxText (metadata.Owners);
			packageProjectUrlTextBox.Text = GetTextBoxText (metadata.ProjectUrl);
			packageReleaseNotesTextView.Buffer.Text = GetTextBoxText (metadata.ReleaseNotes);
			packageRequireLicenseAcceptanceCheckBox.Active = metadata.RequireLicenseAcceptance;
			packageSummaryTextBox.Text = GetTextBoxText (metadata.Summary);
			packageTagsTextBox.Text = GetTextBoxText (metadata.Tags);
			packageTitleTextBox.Text = GetTextBoxText (metadata.Title);
		}

		static string GetTextBoxText (string text)
		{
			return text ?? string.Empty;
		}

		internal void Save (PackagingProject project)
		{
			UpdateMetadata ();
			project.UpdatePackageMetadata (metadata);
		}

		internal void Save (DotNetProject project)
		{
			UpdateMetadata ();
			metadata.UpdateProject (project);
		}

		void UpdateMetadata ()
		{
			metadata.Id = packageIdTextBox.Text;
			metadata.Version = packageVersionTextBox.Text;
			metadata.Authors = packageAuthorsTextBox.Text;
			metadata.Description = packageDescriptionTextView.Buffer.Text;

			metadata.Copyright = packageCopyrightTextBox.Text;
			metadata.DevelopmentDependency = packageDevelopmentDependencyCheckBox.Active;
			metadata.IconUrl = packageIconUrlTextBox.Text;
			metadata.Language = packageLanguageTextBox.Text;
			metadata.LicenseUrl = packageLicenseUrlTextBox.Text;
			metadata.Owners = packageOwnersTextBox.Text;
			metadata.ProjectUrl = packageProjectUrlTextBox.Text;
			metadata.ReleaseNotes = packageReleaseNotesTextView.Buffer.Text;
			metadata.RequireLicenseAcceptance = packageRequireLicenseAcceptanceCheckBox.Active;
			metadata.Summary = packageSummaryTextBox.Text;
			metadata.Tags = packageTagsTextBox.Text;
			metadata.Title = packageTitleTextBox.Text;
		}
	}
}

