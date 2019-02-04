﻿//
// DefaultSourceEditorOptions.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
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
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.Fonts;
using MonoDevelop.Ide.Editor.Extension;
using Microsoft.VisualStudio.CodingConventions;
using System.Threading.Tasks;

namespace MonoDevelop.Ide.Editor
{
	public enum WordNavigationStyle
	{
		Unix,
		Windows
	}

	public enum LineEndingConversion {
		Ask,
		LeaveAsIs,
		ConvertAlways
	}
	
	/// <summary>
	/// This class contains all text editor options from ITextEditorOptions and additional options
	/// the text editor frontend may use.  
	/// </summary>
	public sealed class DefaultSourceEditorOptions : ITextEditorOptions
	{
		static DefaultSourceEditorOptions instance;
		//static TextStylePolicy defaultPolicy;
		static bool inited;
		ICodingConventionContext context;

		public static DefaultSourceEditorOptions Instance {
			get { return instance; }
		}

		public static ITextEditorOptions PlainEditor {
			get;
			private set;
		}

		static DefaultSourceEditorOptions ()
		{
			Init ();
		}

		public static void Init ()
		{
			if (inited)
				return;
			inited = true;

			var policy = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> ("text/plain");
			instance = new DefaultSourceEditorOptions (policy);
			MonoDevelop.Projects.Policies.PolicyService.DefaultPolicies.PolicyChanged += instance.HandlePolicyChanged;

			PlainEditor = new PlainEditorOptions ();
		}

		internal void FireChange ()
		{
			OnChanged (EventArgs.Empty);
		}

		class PlainEditorOptions : ITextEditorOptions
		{
			#region IDisposable implementation

			void IDisposable.Dispose ()
			{
				// nothing
			}

			#endregion

			#region ITextEditorOptions implementation

			WordFindStrategy ITextEditorOptions.WordFindStrategy {
				get {
					return DefaultSourceEditorOptions.Instance.WordFindStrategy;
				}
			}

			bool ITextEditorOptions.TabsToSpaces {
				get {
					return DefaultSourceEditorOptions.Instance.TabsToSpaces;
				}
			}

			int ITextEditorOptions.IndentationSize {
				get {
					return DefaultSourceEditorOptions.Instance.IndentationSize;
				}
			}

			int ITextEditorOptions.TabSize {
				get {
					return DefaultSourceEditorOptions.Instance.TabSize;
				}
			}

			bool ITextEditorOptions.ShowIconMargin {
				get {
					return false;
				}
			}

			bool ITextEditorOptions.ShowLineNumberMargin {
				get {
					return false;
				}
			}

			bool ITextEditorOptions.ShowFoldMargin {
				get {
					return false;
				}
			}

			bool ITextEditorOptions.HighlightCaretLine {
				get {
					return false;
				}
			}

			int ITextEditorOptions.RulerColumn {
				get {
					return DefaultSourceEditorOptions.Instance.RulerColumn;
				}
			}

			bool ITextEditorOptions.ShowRuler {
				get {
					return false;
				}
			}

			IndentStyle ITextEditorOptions.IndentStyle {
				get {
					return DefaultSourceEditorOptions.Instance.IndentStyle;
				}
			}

			bool ITextEditorOptions.OverrideDocumentEolMarker {
				get {
					return false;
				}
			}

			bool ITextEditorOptions.EnableSyntaxHighlighting {
				get {
					return DefaultSourceEditorOptions.Instance.EnableSyntaxHighlighting;
				}
			}

			bool ITextEditorOptions.RemoveTrailingWhitespaces {
				get {
					return DefaultSourceEditorOptions.Instance.RemoveTrailingWhitespaces;
				}
			}

			bool ITextEditorOptions.WrapLines {
				get {
					return DefaultSourceEditorOptions.Instance.WrapLines;
				}
			}

			string ITextEditorOptions.FontName {
				get {
					return DefaultSourceEditorOptions.Instance.FontName;
				}
			}

			string ITextEditorOptions.GutterFontName {
				get {
					return DefaultSourceEditorOptions.Instance.GutterFontName;
				}
			}

			string ITextEditorOptions.EditorTheme {
				get {
					return DefaultSourceEditorOptions.Instance.EditorTheme;
				}
			}

			string ITextEditorOptions.DefaultEolMarker {
				get {
					return DefaultSourceEditorOptions.Instance.DefaultEolMarker;
				}
			}

			bool ITextEditorOptions.GenerateFormattingUndoStep {
				get {
					return DefaultSourceEditorOptions.Instance.GenerateFormattingUndoStep;
				}
			}

			bool ITextEditorOptions.EnableSelectionWrappingKeys {
				get {
					return DefaultSourceEditorOptions.Instance.EnableSelectionWrappingKeys;
				}
			}

			ShowWhitespaces ITextEditorOptions.ShowWhitespaces {
				get {
					return ShowWhitespaces.Never;
				}
			}

			IncludeWhitespaces ITextEditorOptions.IncludeWhitespaces {
				get {
					return DefaultSourceEditorOptions.Instance.IncludeWhitespaces;
				}
			}
			
			bool ITextEditorOptions.SmartBackspace {
				get {
					return DefaultSourceEditorOptions.Instance.SmartBackspace;
				}
			}

			bool ITextEditorOptions.EnableQuickDiff {
				get {
					return false;
				}
			}
			#endregion


		}

		void HandlePolicyChanged (object sender, MonoDevelop.Projects.Policies.PolicyChangedEventArgs args)
		{
			TextStylePolicy pol = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> ("text/plain");
			UpdateStylePolicy (pol);
		}

		DefaultSourceEditorOptions (TextStylePolicy currentPolicy)
		{
			wordNavigationStyle = ConfigurationProperty.Create ("WordNavigationStyle", WordNavigationStyle.Windows);
			
			UpdateStylePolicy (currentPolicy);
			FontService.RegisterFontChangedCallback ("Editor", UpdateFont);
			FontService.RegisterFontChangedCallback ("MessageBubbles", UpdateFont);

			IdeApp.Preferences.ColorScheme.Changed += OnColorSchemeChanged;
			IdeApp.Preferences.Editor.FollowCodingConventions.Changed += OnFollowCodingConventionsChanged;
		}

		void OnFollowCodingConventionsChanged (object sender, EventArgs e)
		{
			UpdateContextOptions (null, null).Ignore ();
		}


		void UpdateFont ()
		{
			this.OnChanged (EventArgs.Empty);
		}

		internal void UpdateStylePolicy (MonoDevelop.Ide.Gui.Content.TextStylePolicy currentPolicy)
		{
			DefaultEolMarker      = TextStylePolicy.GetEolMarker (currentPolicy.EolMarker);
			TabsToSpaces          = currentPolicy.TabsToSpaces; // PropertyService.Get ("TabsToSpaces", false);
			TabSize               = currentPolicy.TabWidth; //PropertyService.Get ("TabIndent", 4);
			rulerColumn           = currentPolicy.FileWidth; //PropertyService.Get ("RulerColumn", 80);
			allowTabsAfterNonTabs = !currentPolicy.NoTabsAfterNonTabs; //PropertyService.Get ("AllowTabsAfterNonTabs", true);
			RemoveTrailingWhitespaces = currentPolicy.RemoveTrailingWhitespace; //PropertyService.Get ("RemoveTrailingWhitespaces", true);
		}

		internal DefaultSourceEditorOptions Create ()
		{
			var result = (DefaultSourceEditorOptions)MemberwiseClone ();
			result.Changed = null;
			return result;
		}

		public DefaultSourceEditorOptions WithTextStyle (TextStylePolicy policy)
		{
			if (policy == null)
				throw new ArgumentNullException (nameof (policy));
			var result = (DefaultSourceEditorOptions)MemberwiseClone ();
			result.UpdateStylePolicy (policy);
			result.Changed = null;
			return result;
		}

		internal void SetContext (ICodingConventionContext context)
		{
			if (this.context == context)
				return;
			if (this.context != null)
				this.context.CodingConventionsChangedAsync -= UpdateContextOptions;
			this.context = context;
			context.CodingConventionsChangedAsync += UpdateContextOptions;
			UpdateContextOptions (null, null).Ignore ();
		}

		private Task UpdateContextOptions (object sender, CodingConventionsChangedEventArgs arg)
		{
			if (context == null)
				return Task.FromResult (false);

			bool followCodingConventions = IdeApp.Preferences.Editor.FollowCodingConventions;

			defaultEolMarkerFromContext = null;
			if (followCodingConventions && context.CurrentConventions.UniversalConventions.TryGetLineEnding (out string eolMarker))
				defaultEolMarkerFromContext = eolMarker;

			tabsToSpacesFromContext = null;
			if (followCodingConventions && context.CurrentConventions.UniversalConventions.TryGetIndentStyle (out Microsoft.VisualStudio.CodingConventions.IndentStyle result))
				tabsToSpacesFromContext = result == Microsoft.VisualStudio.CodingConventions.IndentStyle.Spaces;

			indentationSizeFromContext = null;
			if (followCodingConventions && context.CurrentConventions.UniversalConventions.TryGetIndentSize (out int indentSize)) 
				indentationSizeFromContext = indentSize;

			removeTrailingWhitespacesFromContext = null;
			if (followCodingConventions && context.CurrentConventions.UniversalConventions.TryGetAllowTrailingWhitespace (out bool allowTrailing))
				removeTrailingWhitespacesFromContext = !allowTrailing;

			tabSizeFromContext = null;
			if (followCodingConventions && context.CurrentConventions.UniversalConventions.TryGetTabWidth (out int tSize))
				tabSizeFromContext = tSize;

			rulerColumnFromContext = null;
			showRulerFromContext = null;
			if (followCodingConventions && context.CurrentConventions.TryGetConventionValue<string> (EditorConfigService.MaxLineLengthConvention, out string maxLineLength)) {
				if (maxLineLength != "off" && int.TryParse (maxLineLength, out int i)) {
					rulerColumnFromContext = i;
					showRulerFromContext = true;
				} else {
					showRulerFromContext = false;
				}
			}

			return Task.FromResult (true);
		}

		#region new options

		public bool EnableAutoCodeCompletion {
			get { return IdeApp.Preferences.EnableAutoCodeCompletion; }
			set { IdeApp.Preferences.EnableAutoCodeCompletion.Set (value); }
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> defaultRegionsFolding = ConfigurationProperty.Create ("DefaultRegionsFolding", false);
		public bool DefaultRegionsFolding {
			get {
				return defaultRegionsFolding;
			}
			set {
				if (defaultRegionsFolding.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> defaultCommentFolding = ConfigurationProperty.Create ("DefaultCommentFolding", true);
		public bool DefaultCommentFolding {
			get {
				return defaultCommentFolding;
			}
			set {
				if (defaultCommentFolding.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> enableNewEditor = ConfigurationProperty.Create ("EnableNewEditor", false);
		public bool EnableNewEditor {
			get {
				return enableNewEditor;
			}
			set {
				if (enableNewEditor.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}


		// TODO: Windows equivalent?
		ConfigurationProperty<bool> enableSemanticHighlighting = ConfigurationProperty.Create ("EnableSemanticHighlighting", true);
		public bool EnableSemanticHighlighting {
			get {
				return enableSemanticHighlighting;
			}
			set {
				if (enableSemanticHighlighting.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}


		// TODO: Windows equivalent?
		ConfigurationProperty<bool> tabIsReindent = ConfigurationProperty.Create ("TabIsReindent", false);
		public bool TabIsReindent {
			get {
				return tabIsReindent;
			}
			set {
				if (tabIsReindent.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> autoInsertMatchingBracket = IdeApp.Preferences.Editor.EnableBraceCompletion;
		public bool AutoInsertMatchingBracket {
			get {
				return autoInsertMatchingBracket;
			}
			set {
				if (autoInsertMatchingBracket.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> smartSemicolonPlacement = ConfigurationProperty.Create ("SmartSemicolonPlacement", false);
		public bool SmartSemicolonPlacement {
			get {
				return smartSemicolonPlacement;
			}
			set {
				if (smartSemicolonPlacement.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<IndentStyle> indentStyle = ConfigurationProperty.Create ("IndentStyle", IndentStyle.Smart);
		public IndentStyle IndentStyle {
			get {
				return indentStyle;
			}
			set {
				if (indentStyle.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> enableHighlightUsages = ConfigurationProperty.Create ("EnableHighlightUsages", true);
		public bool EnableHighlightUsages {
			get {
				return enableHighlightUsages;
			}
			set {
				if (enableHighlightUsages.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<LineEndingConversion> lineEndingConversion = ConfigurationProperty.Create ("LineEndingConversion", LineEndingConversion.LeaveAsIs);
		public LineEndingConversion LineEndingConversion {
			get {
				return lineEndingConversion;
			}
			set {
				if (lineEndingConversion.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> showProcedureLineSeparators = ConfigurationProperty.Create ("ShowProcedureLineSeparators", false);
		public bool ShowProcedureLineSeparators {
			get {
				return showProcedureLineSeparators;
			}
			set {
				if (showProcedureLineSeparators.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		#endregion

		[Obsolete ("Deprecated - use the roslyn FeatureOnOffOptions.FormatXXX per document options.")]
		public bool OnTheFlyFormatting {
			get {
				return true;
			}
			set {
				// unused
			}
		}

		#region ITextEditorOptions
		ConfigurationProperty<string> defaultEolMarker = IdeApp.Preferences.Editor.NewLineCharacter;
		string defaultEolMarkerFromContext = null;

		// TODO: This isn't surfaced in properties, only policies. We have no UI for it.
		public string DefaultEolMarker {
			get {
				return defaultEolMarkerFromContext ?? defaultEolMarker;
			}
			set {
				if (defaultEolMarker.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<WordNavigationStyle> wordNavigationStyle;
		public WordNavigationStyle WordNavigationStyle {
			get {
				return wordNavigationStyle;
			}
			set {
				if (wordNavigationStyle.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		public WordFindStrategy WordFindStrategy {
			get {
				switch (WordNavigationStyle) {
				case WordNavigationStyle.Windows:
					return WordFindStrategy.SharpDevelop;
				default:
					return WordFindStrategy.Emacs;
				}
			}
			set {
				throw new System.NotImplementedException ();
			}
		}

		// TODO: Windows equivalent?
		bool allowTabsAfterNonTabs = true;
		public bool AllowTabsAfterNonTabs {
			get {
				return allowTabsAfterNonTabs;
			}
			set {
				if (allowTabsAfterNonTabs != value) {
					PropertyService.Set ("AllowTabsAfterNonTabs", value);
					allowTabsAfterNonTabs = value;
					OnChanged (EventArgs.Empty);
				}
			}
		}
		
		ConfigurationProperty<bool> tabsToSpaces = IdeApp.Preferences.Editor.ConvertTabsToSpaces;
		bool? tabsToSpacesFromContext;
		public bool TabsToSpaces {
			get {
				return tabsToSpacesFromContext ?? tabsToSpaces;
			}
			set {
				if (tabsToSpaces.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<int> indentationSize = IdeApp.Preferences.Editor.IndentSize;
		int? indentationSizeFromContext;
		public int IndentationSize {
			get {
				return indentationSizeFromContext ?? indentationSize;
			}
			set {
				if (indentationSize.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		public string IndentationString {
			get {
				return TabsToSpaces ? new string (' ', this.TabSize) : "\t";
			}
		}

		ConfigurationProperty<int> tabSize = IdeApp.Preferences.Editor.TabSize;
		int? tabSizeFromContext;
		public int TabSize {
			get {
				return tabSizeFromContext ?? IndentationSize;
			}
			set {
				if (tabSize.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> trimTrailingWhitespace = IdeApp.Preferences.Editor.TrimTrailingWhitespace;
		bool? removeTrailingWhitespacesFromContext;

		public bool RemoveTrailingWhitespaces {
			get {
				return removeTrailingWhitespacesFromContext ?? trimTrailingWhitespace;
			}
			set {
				if (trimTrailingWhitespace.Set(value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> showLineNumberMargin = IdeApp.Preferences.Editor.ShowLineNumberMargin;
		public bool ShowLineNumberMargin {
			get {
				return showLineNumberMargin;
			}
			set {
				if (showLineNumberMargin.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> showFoldMargin = IdeApp.Preferences.Editor.ShowOutliningMargin;
		public bool ShowFoldMargin {
			get {
				return showFoldMargin;
			}
			set {
				if (showFoldMargin.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> showIconMargin = IdeApp.Preferences.Editor.ShowGlyphMargin;
		public bool ShowIconMargin {
			get {
				return showIconMargin;
			}
			set {
				if (showIconMargin.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> highlightCaretLine = IdeApp.Preferences.Editor.EnableHighlightCurrentLine;
		public bool HighlightCaretLine {
			get {
				return highlightCaretLine;
			}
			set {
				if (highlightCaretLine.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> enableSyntaxHighlighting = ConfigurationProperty.Create ("EnableSyntaxHighlighting", true);
		public bool EnableSyntaxHighlighting {
			get {
				return enableSyntaxHighlighting;
			}
			set {
				if (enableSyntaxHighlighting.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		internal ConfigurationProperty<bool> highlightMatchingBracket = IdeApp.Preferences.Editor.EnableHighlightDelimiter;
		public bool HighlightMatchingBracket {
			get {
				return highlightMatchingBracket;
			}
			set {
				if (highlightMatchingBracket.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		int  rulerColumn = 120;
		int? rulerColumnFromContext;


		// TODO: VS equivalent?
		public int RulerColumn {
			get {
				return rulerColumnFromContext ?? rulerColumn;
			}
			set {
				if (rulerColumn != value) {
					PropertyService.Set ("RulerColumn", value);
					rulerColumn = value;
					OnChanged (EventArgs.Empty);
				}
			}
		}

		// TODO: VS equivalent?
		ConfigurationProperty<bool> showRuler = ConfigurationProperty.Create ("ShowRuler", true);
		bool? showRulerFromContext;
		public bool ShowRuler {
			get {
				return showRulerFromContext ?? showRuler;
			}
			set {
				if (showRuler.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: ???
		ConfigurationProperty<bool> enableAnimations = ConfigurationProperty.Create ("EnableAnimations", true);
		public bool EnableAnimations {
			get { 
				return enableAnimations; 
			}
			set {
				if (enableAnimations.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Is this ShowBlockStructure?
		ConfigurationProperty<bool> drawIndentationMarkers = ConfigurationProperty.Create ("DrawIndentationMarkers", false);
		public bool DrawIndentationMarkers {
			get {
				return drawIndentationMarkers;
			}
			set {
				if (drawIndentationMarkers.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: VSWindows has more options than just WordWrap and None. Might need UI changes.
		ConfigurationProperty<Microsoft.VisualStudio.Text.Editor.WordWrapStyles> wrapLines = IdeApp.Preferences.Editor.WordWrapStyle;
		public bool WrapLines {
			get {
				return (wrapLines.Value & Microsoft.VisualStudio.Text.Editor.WordWrapStyles.WordWrap) != 0;
			}
			set {
				var newValue = value ? Microsoft.VisualStudio.Text.Editor.WordWrapStyles.WordWrap : Microsoft.VisualStudio.Text.Editor.WordWrapStyles.None;
				if (wrapLines.Set (newValue))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> enableQuickDiff = IdeApp.Preferences.Editor.ShowChangeTrackingMargin;
		public bool EnableQuickDiff {
			get {
				return enableQuickDiff;
			}
			set {
				if (enableQuickDiff.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		public string FontName {
			get {
				return FontService.FilterFontName (FontService.GetUnderlyingFontName ("Editor"));
			}
			set {
				throw new InvalidOperationException ("Set font through font service");
			}
		}

		public string GutterFontName {
			get {
				return FontService.FilterFontName (FontService.GetUnderlyingFontName ("Editor"));
			}
			set {
				throw new InvalidOperationException ("Set font through font service");
			}
		}
		
		ConfigurationProperty<string> colorScheme = IdeApp.Preferences.ColorScheme;
		public string EditorTheme {
			get {
				return colorScheme;
			}
			set {
				colorScheme.Set (value);
			}
		}

		void OnColorSchemeChanged (object sender, EventArgs e)
		{
			OnChanged (EventArgs.Empty);
		}

		ConfigurationProperty<bool> generateFormattingUndoStep = IdeApp.Preferences.Editor.OutliningUndoStep;
		public bool GenerateFormattingUndoStep {
			get {
				return generateFormattingUndoStep;
			}
			set {
				if (generateFormattingUndoStep.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		ConfigurationProperty<bool> enableSelectionWrappingKeys = ConfigurationProperty.Create ("EnableSelectionWrappingKeys", false);
		public bool EnableSelectionWrappingKeys {
			get {
				return enableSelectionWrappingKeys;
			}
			set {
				if (enableSelectionWrappingKeys.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		bool overrideDocumentEolMarker = false;
		public bool OverrideDocumentEolMarker {
			get {
				return overrideDocumentEolMarker;
			}
			set {
				if (overrideDocumentEolMarker != value) {
					overrideDocumentEolMarker = value;
					OnChanged (EventArgs.Empty);
				}
			}
		}

		// TODO: VS-Editor only has true/false here.
		ConfigurationProperty<ShowWhitespaces> showWhitespaces = ConfigurationProperty.Create ("ShowWhitespaces", ShowWhitespaces.Never);
		public ShowWhitespaces ShowWhitespaces {
			get {
				return showWhitespaces;
			}
			set {
				if (showWhitespaces.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<IncludeWhitespaces> includeWhitespaces = ConfigurationProperty.Create ("IncludeWhitespaces", IncludeWhitespaces.All);
		public IncludeWhitespaces IncludeWhitespaces {
			get {
				return includeWhitespaces;
			}
			set {
				if (includeWhitespaces.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}

		// TODO: Windows equivalent?
		ConfigurationProperty<bool> smartBackspace = ConfigurationProperty.Create ("SmartBackspace", true);
		public bool SmartBackspace{
			get {
				return smartBackspace;

			}
			set {
				if (smartBackspace.Set (value))
					OnChanged (EventArgs.Empty);
			}
		}
		#endregion
		
		public void Dispose ()
		{
			FontService.RemoveCallback (UpdateFont);
			IdeApp.Preferences.ColorScheme.Changed -= OnColorSchemeChanged;
			IdeApp.Preferences.Editor.FollowCodingConventions.Changed -= OnFollowCodingConventionsChanged;
			if (context != null)
				context.CodingConventionsChangedAsync -= UpdateContextOptions;
		}

		void OnChanged (EventArgs args)
		{
			Changed?.Invoke (null, args);
		}

		public event EventHandler Changed;
	}
}

