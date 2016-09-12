//
// PortableRuntimeOptionsPanel.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc.
// Copyright (c) Microsoft Inc.
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
using System.Text;
using System.Linq;
using System.Collections.Generic;

using MonoDevelop.Components;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;

using Gtk;
using MonoDevelop.Ide.Gui.Dialogs;
using Newtonsoft.Json.Linq;
using MonoDevelop.Ide.Editor;

namespace MonoDevelop.Ide.Projects.OptionPanels
{
	internal class PortableRuntimeOptionsPanel : ItemOptionsPanel
	{
		PortableRuntimeOptionsPanelWidget widget;

		public override Control CreatePanelWidget ()
		{
			widget = new PortableRuntimeOptionsPanelWidget ((DotNetProject)ConfiguredProject, ItemConfigurations);

			return widget;
		}

		public override void ApplyChanges ()
		{
			widget.Store ();
		}
	}

	class PortableRuntimeOptionsPanelWidget : Gtk.VBox
	{
		const string netstandardDocsUrl = "https://docs.microsoft.com/en-us/dotnet/articles/standard/library";
		const string pcldDocsUrl = "https://developer.xamarin.com/guides/cross-platform/application_fundamentals/pcl/introduction_to_portable_class_libraries/";

		DotNetProject project;
		TargetFramework target;

		TargetFrameworkMoniker pcl5Tfm = new TargetFrameworkMoniker (TargetFrameworkMoniker.ID_PORTABLE, "v5.0");

		string [] KnownNetStandardVersions = new [] {
			"netstandard1.0",
			"netstandard1.1",
			"netstandard1.2",
			"netstandard1.3",
			"netstandard1.4",
			"netstandard1.5",
			"netstandard1.6",
		};

		const string NetStandardPackageName = "NETStandard.Library";
		const string NetStandardPackageVersion = "1.6.0";
		const string NetStandardDefaultFramework = "netstandard1.3";

		ComboBox netStandardCombo;
		Entry targetFrameworkEntry;
		RadioButton netstandardRadio;
		RadioButton pclRadio;
		Button frameworkPickerButton;

		public PortableRuntimeOptionsPanelWidget (DotNetProject project, IEnumerable<ItemConfiguration> configurations)
		{
			Build ();

			this.project = project;

			TargetFramework = project.TargetFramework;

			string projectJsonFramework = GetProjectJsonFrameworks (project)?.FirstOrDefault ();

			NetStandardVersion = projectJsonFramework;

			if (projectJsonFramework != null && projectJsonFramework.StartsWith ("netstandard", StringComparison.Ordinal)) {
				netstandardRadio.Active = true;
			} else {
				pclRadio.Active = true;
			}
		}

		void Build ()
		{
			Spacing = 6;

			PackStart (new Label { Markup = string.Format ("<b>{0}</b>", GettextCatalog.GetString ("Target Framework")), Xalign = 0f });

			var fxAlignment = new Alignment (0f, 0f, 1f, 1f) { LeftPadding = 12 };
			PackStart (fxAlignment);
			var radioBox = new VBox { Spacing = 10 };
			fxAlignment.Add (radioBox);

			var netstandardPickerHbox = new HBox { Spacing = 10 };
			radioBox.PackStart (netstandardPickerHbox);
			netstandardRadio = new RadioButton (GettextCatalog.GetString (".NET Standard Platform:"));
			netstandardPickerHbox.PackStart (netstandardRadio, false, false, 0);
			netstandardPickerHbox.PackStart (netStandardCombo = ComboBox.NewText (), false, false, 0);

			var netstandardDesc = new Label { Markup = GettextCatalog.GetString ("Your library will be compatible with all frameworks that support the selected <a href='{0}'>.NET Standard</a> version.", netstandardDocsUrl), Xalign = 0f };
			GtkWorkarounds.SetLinkHandler (netstandardDesc, HandleLink);
			radioBox.PackStart (new Alignment (0f, 0f, 1f, 1f) { Child = netstandardDesc, LeftPadding = 24 });

			var pclPickerHbox = new HBox { Spacing = 10 };
			radioBox.PackStart (pclPickerHbox);
			pclRadio = new RadioButton (netstandardRadio, GettextCatalog.GetString (".NET Portable:"));
			pclPickerHbox.PackStart (pclRadio, false, false, 0);
			pclPickerHbox.PackStart (targetFrameworkEntry = new Entry { IsEditable = false, WidthChars = 20 }, false, false, 0);
			frameworkPickerButton = new Button (GettextCatalog.GetString ("Change..."));
			pclPickerHbox.PackStart (frameworkPickerButton, false, false, 0);

			var pclDesc = new Label { Markup = GettextCatalog.GetString ("Your library will be compatible with the frameworks supported by the selected <a href='{0}'>PCL profile</a>.", pcldDocsUrl), Xalign = 0f };
			GtkWorkarounds.SetLinkHandler (pclDesc, HandleLink);
			radioBox.PackStart (new Alignment (0f, 0f, 1f, 1f) { Child = pclDesc, LeftPadding = 24 });

			frameworkPickerButton.Clicked += PickFramework;

			// both toggle when we switch between them, only need to subscribe to one event
			netstandardRadio.Toggled += RadioToggled;

			ShowAll ();
		}

		string NetStandardVersion {
			get {
				return netStandardCombo.ActiveText;
			}
			set {
				((ListStore)netStandardCombo.Model).Clear ();

				int selected = -1;

				for (int i = 0; i < KnownNetStandardVersions.Length; i++) {
					var version = KnownNetStandardVersions [i];
					netStandardCombo.AppendText (version);
					if (version == value) {
						selected = i;
					}
				}

				if (value == null) {
					selected = KnownNetStandardVersions.Length - 1;
				} else if (selected < 0) {
					//project uses some version we don't know about, add it
					netStandardCombo.AppendText (value);
					selected = KnownNetStandardVersions.Length;
				}

				netStandardCombo.Active = selected;
			}
		}

		TargetFramework TargetFramework {
			get {
				return target;
			}
			set {
				target = value;
				targetFrameworkEntry.Text = PortableRuntimeSelectorDialog.GetPclShortDisplayName (target, false);
			}
		}

		void RadioToggled (object sender, EventArgs e)
		{
			UpdateSensitivity ();
		}

		void UpdateSensitivity ()
		{
			bool pcl = pclRadio.Active;

			netStandardCombo.Sensitive = !pcl;
			targetFrameworkEntry.Sensitive = pcl;
			frameworkPickerButton.Sensitive = pcl;
		}

		void HandleLink (string url)
		{
			DesktopService.ShowUrl (url);
		}

		void PickFramework (object sender, EventArgs e)
		{
			var dlg = new PortableRuntimeSelectorDialog (target);
			try {
				var result = MessageService.RunCustomDialog (dlg, (Gtk.Window)Toplevel);
				if (result == (int)Gtk.ResponseType.Ok) {
					TargetFramework = dlg.TargetFramework;
				}
			} finally {
				dlg.Destroy ();
			}
		}

		//TODO error handling
		public void Store ()
		{
			bool needsReload = false;

			//get the new framework and netstandard version
			var isNetStandard = netstandardRadio.Active;
			var nsVersion = isNetStandard ? NetStandardVersion : null;
			var fx = TargetFramework;


			//netstandard always uses PCL5 framework
			if (isNetStandard) {
				fx = Runtime.SystemAssemblyService.GetTargetFramework (pcl5Tfm);
			}

			//netstandard always uses project.json, ensure it exists
			var projectJsonFile = project.GetProjectFile (project.BaseDirectory.Combine ("project.json"));
			if (isNetStandard && projectJsonFile == null) {
				projectJsonFile = MigrateToProjectJson (project);
				needsReload = true;
			}

			//if project.json exists, update it
			if (projectJsonFile != null) {
				var nugetFx = nsVersion ?? GetPclShortNameMapping (fx.Id) ?? NetStandardDefaultFramework;
				bool projectJsonChanged;
				SetProjectJsonValues (projectJsonFile.FilePath, nugetFx, out projectJsonChanged);
				needsReload |= projectJsonChanged;
			}

			//if the framework has changed, update it
			if (fx != null && fx != project.TargetFramework) {
				project.TargetFramework = fx;
			}

			//FIXME: if we add or modify project.json, we currently need to reload the project to make the NuGet
			//addin restore the packages and reset the code completion assembly references
			if (needsReload) {
				//the options dialog asynchronously saves the project, which will interfere with us reloading it
				//instead, set the reload to happen after the project is next saved
				project.Saved += OneShotProjectReloadAfterSave;
			}
		}

		static void OneShotProjectReloadAfterSave (object sender, EventArgs args)
		{
			var project = (Project)sender;

			project.Saved -= OneShotProjectReloadAfterSave;

			//the project is marked as not needing reloading immediately after the event is fired, so defer this
			GLib.Timeout.Add (0, () => {
				project.NeedsReload = true;
				FileService.NotifyFileChanged (project.FileName);
				return false;
			});
		}

		static IEnumerable<string> GetProjectJsonFrameworks (DotNetProject project)
		{
			var packagesJsonFile = project.GetProjectFile (project.BaseDirectory.Combine ("project.json"));
			if (packagesJsonFile == null) {
				return null;
			}

			var file = TextFileProvider.Instance.GetEditableTextFile (packagesJsonFile.FilePath.ToString ());

			JObject json;

			using (var tr = file.CreateReader ())
			using (var jr = new Newtonsoft.Json.JsonTextReader (tr)) {
				json = (JObject)JToken.Load (jr);
			}

			var frameworks = json ["frameworks"] as JObject;
			if (frameworks == null)
				return null;

			return frameworks.Properties ().Select (p => p.Name);
		}

		static void SetProjectJsonValues (string filename, string framework, out bool changed)
		{
			changed = false;

			bool isOpen;
			var file = TextFileProvider.Instance.GetTextEditorData (filename, out isOpen);

			JObject json;

			using (var tr = file.CreateReader ())
			using (var jr = new Newtonsoft.Json.JsonTextReader (tr)) {
				json = (JObject)JToken.Load (jr);
			}

			//TODO: remove this if the FX is not a NugetID
			var deps = (json ["dependencies"] as JObject) ?? ((JObject) (json ["dependencies"] = new JObject ()));
			var existingRefVersion = deps.Property (NetStandardPackageName)?.Value?.Value<string> ();
			string newRefVersion = EnsureMinimumVersion (NetStandardPackageVersion, existingRefVersion);
			if (existingRefVersion != newRefVersion) {
				deps [NetStandardPackageName] = newRefVersion;
				changed = true;
			}

			string [] existingTargetFrameworks = null;
			var frameworks = (json ["frameworks"] as JObject);
			if (frameworks != null) {
				existingTargetFrameworks = frameworks.Properties ().Select (p => p.Name).ToArray ();
			}

			if (existingTargetFrameworks != null && !existingTargetFrameworks.Any(f => f == framework)) {
				var existingFxValue = ((JProperty) json ["frameworks"].First()).Value as JObject;
				json ["frameworks"] = new JObject (
					new JProperty (framework, existingFxValue ?? new JObject ())
				);
				changed = true;
			}

			if (changed) {
				file.Text = json.ToString ();

				if (!isOpen) {
					file.Save ();
				}
			}
		}

		static ProjectFile MigrateToProjectJson (DotNetProject project)
		{
			var projectJsonName = project.BaseDirectory.Combine ("project.json");
			var projectJsonFile = new ProjectFile (projectJsonName, BuildAction.None);

			bool isOpen = false;
			JObject json;
			ITextDocument file;

			if (System.IO.File.Exists (projectJsonName)) {
				file = TextFileProvider.Instance.GetTextEditorData (projectJsonFile.FilePath.ToString (), out isOpen);
				using (var tr = file.CreateReader ())
				using (var jr = new Newtonsoft.Json.JsonTextReader (tr)) {
					json = (JObject)JToken.Load (jr);
				}
			} else {
				file = TextEditorFactory.CreateNewDocument ();
				file.FileName = projectJsonName;
				file.Encoding = System.Text.Encoding.UTF8;
				json = new JObject (
					new JProperty ("supports", new JObject ()),
					new JProperty ("dependencies", new JObject ()),
					new JProperty ("frameworks", new JObject())
				);
			}

			var packagesConfigFile = project.GetProjectFile (project.BaseDirectory.Combine ("packages.config"));
			if (packagesConfigFile != null) {
				//NOTE: it might also be open and unsaved, but that's an unimportant edge case, ignore it
				var configDoc = System.Xml.Linq.XDocument.Load (packagesConfigFile.FilePath);
				if (configDoc.Root != null) {
					var deps = (json ["dependencies"] as JObject) ?? ((JObject)(json ["dependencies"] = new JObject ()));
					foreach (var packagelEl in configDoc.Root.Elements ("package")) {
						deps [(string)packagelEl.Attribute ("id")] = (string)packagelEl.Attribute ("version");
					}
				}
			}

			var framework = GetPclShortNameMapping (project.TargetFramework.Id) ?? NetStandardDefaultFramework;
			json ["frameworks"] = new JObject (
				new JProperty (framework, new JObject())
			);

			file.Text = json.ToString ();

			if (!isOpen) {
				file.Save ();
			}

			project.AddFile (projectJsonFile);
			if (packagesConfigFile != null) {
				project.Files.Remove (packagesConfigFile);

				//we have to delete the packages.config, or the NuGet addin will try to retarget its packages
				FileService.DeleteFile (packagesConfigFile.FilePath);

				//remove the package refs nuget put in the file, project.json doesn't use those
				project.References.RemoveRange (project.References.Where (IsFromPackage).ToArray ());
			}

			return projectJsonFile;
		}

		//HACK: we don't have the info to do this properly, really the package management addin should handle this
		static bool IsFromPackage (ProjectReference r)
		{
			FilePath hintPath = r.Metadata.GetValue<string> ("HintPath");
			if (hintPath.IsNullOrEmpty)
				return false;
			var packagesDir = r.Project.ParentSolution.BaseDirectory.Combine ("packages");
			return hintPath.IsChildPathOf (packagesDir);
		}

		static string GetPclShortNameMapping (TargetFrameworkMoniker tfm)
		{
			if (tfm.Identifier != TargetFrameworkMoniker.ID_PORTABLE) {
				return null;
			}

			//we can only look up via profile numbers for 4.x PCLs
			if (tfm.Version == null || !tfm.Version.StartsWith ("4.", StringComparison.Ordinal)
			    || tfm.Profile == null || !tfm.Profile.StartsWith ("Profile", StringComparison.Ordinal))
			{
				return null;
			}

			// look up against all extant profile numbers
			switch (tfm.Profile.Substring ("Profile".Length)) {
			case "31": return "portable-win81+wp81";
			case "32": return "portable-win81+wpa81";
			case "44": return "portable-net451+win81";
			case "84": return "portable-wp81+wpa81";
			case "151": return "portable-net451+win81+wpa81";
			case "157": return "portable-win81+wp81+wpa81";
			case "7": return "portable-net45+win8";
			case "49": return "portable-net45+wp8";
			case "78": return "portable-net45+win8+wp8";
			case "111": return "portable-net45+win8+wpa81";
			case "259": return "portable-net45+win8+wpa81+wp8";
			case "2": return "portable-net4+win8+sl4+wp7";
			case "3": return "portable-net4+sl4";
			case "4": return "portable-net45+sl4+win8+wp7";
			case "5": return "portable-net4+win8";
			case "6": return "portable-net403+win8";
			case "14": return "portable-net4+sl5";
			case "18": return "portable-net403+sl4";
			case "19": return "portable-net403+sl5";
			case "23": return "portable-net45+sl4";
			case "24": return "portable-net45+sl5";
			case "36": return "portable-net4+sl4+win8+wp8";
			case "37": return "portable-net4+sl5+win8";
			case "41": return "portable-net403+sl4+win8";
			case "42": return "portable-net403+sl5+win8";
			case "46": return "portable-net45+sl4+win8";
			case "47": return "portable-net45+sl5+win8";
			case "88": return "portable-net4+sl4+win8+wp75";
			case "92": return "portable-net4+win8+wpa81";
			case "95": return "portable-net403+sl4+win8+wp7";
			case "96": return "portable-net403+sl4+win8+wp75";
			case "102": return "portable-net403+win8+wpa81";
			case "104": return "portable-net45+sl4+win8+wp75";
			case "136": return "portable-net4+sl5+win8+wp8";
			case "143": return "portable-net403+sl4+win8+wp8";
			case "147": return "portable-net403+sl5+win8+wp8";
			case "154": return "portable-net45+sl4+win8+wp8";
			case "158": return "portable-net45+sl5+win8+wp8";
			case "225": return "portable-net4+sl5+win8+wpa81";
			case "240": return "portable-net403+sl5+win8+wpa81";
			case "255": return "portable-net45+sl5+win8+wpa81";
			case "328": return "portable-net4+sl5+win8+wpa81+wp8";
			case "336": return "portable-net403+sl5+win8+wpa81+wp8";
			case "344": return "portable-net45+sl5+win8+wpa81+wp8";
			}

			return null;
		}

		static string EnsureMinimumVersion (string minimum, string existing)
		{
			if (existing == null) {
				return minimum;
			}

			var minimumSplit = minimum.Split (new char [] { '.', '-' });
			var existingSplit = existing.Split (new char [] { '.', '-' });

			for (int i = 0; i < minimumSplit.Length; i++) {
				var m = int.Parse (minimumSplit [i]);
				int e;
				if (existingSplit.Length <= i || !int.TryParse (existingSplit [i], out e)) {
					return minimum;
				}
				if (m > e) {
					return minimum;
				}
				if (e > m) {
					return existing;
				}
			}

			return minimum;
		}
	}
}