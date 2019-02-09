//
// Copyright (c) Microsoft Corp. (https://www.microsoft.com)
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects;

#if WINDOWS
using EditorOperationsInterface = Microsoft.VisualStudio.Text.Operations.IEditorOperations3;
#else
using EditorOperationsInterface = Microsoft.VisualStudio.Text.Operations.IEditorOperations4;
#endif

namespace MonoDevelop.TextEditor
{
	abstract partial class TextViewContent<TView, TImports> : ViewContent, INavigable, IZoomable, ICustomCommandTarget, ICommandHandler, ICommandUpdater
		where TView : ITextView
		where TImports : TextViewImports
	{
		readonly FilePath fileName;
		readonly string mimeType;
		readonly Project ownerProject;
		readonly IEditorCommandHandlerService commandService;
		readonly EditorOperationsInterface editorOperations;
		readonly IEditorOptions editorOptions;
		readonly List<IEditorContentProvider> contentProviders;
		readonly Ide.Editor.DefaultSourceEditorOptions sourceEditorOptions;

		public TImports Imports { get; }
		public TView TextView { get; }
		public ITextDocument TextDocument { get; }
		public ITextBuffer TextBuffer { get; }

		protected TextViewContent (
			TImports imports,
			FilePath fileName,
			string mimeType,
			Project ownerProject)
		{
			this.Imports = imports;
			this.fileName = fileName;
			this.mimeType = mimeType;
			this.ownerProject = ownerProject;
			this.sourceEditorOptions = Ide.Editor.DefaultSourceEditorOptions.Instance;

			// FIXME: move this to the end of the .ctor after fixing margin options responsiveness
			HandleSourceEditorOptionsChanged (this, EventArgs.Empty);

			//TODO: this can change when the file is renamed
			var contentType = GetContentTypeFromMimeType (fileName, mimeType);

			TextDocument = Imports.TextDocumentFactoryService.CreateAndLoadTextDocument (fileName, contentType);
			TextBuffer = TextDocument.TextBuffer;

			var roles = GetAllPredefinedRoles ();
			var dataModel = new VacuousTextDataModel (TextBuffer);
			var viewModel = UIExtensionSelector.InvokeBestMatchingFactory (
				Imports.TextViewModelProviders,
				dataModel.ContentType,
				roles,
				provider => provider.CreateTextViewModel (dataModel, roles),
				Imports.ContentTypeRegistryService,
				Imports.GuardedOperations,
				this) ?? new VacuousTextViewModel (dataModel);

			TextView = CreateTextView (viewModel, roles);
			control = CreateControl ();

			commandService = Imports.EditorCommandHandlerServiceFactory.GetService (TextView);
			editorOperations = (EditorOperationsInterface)Imports.EditorOperationsProvider.GetEditorOperations (TextView);
			editorOptions = Imports.EditorOptionsFactoryService.GetOptions (TextView);
			contentProviders = new List<IEditorContentProvider> (Imports.EditorContentProviderService.GetContentProvidersForView (TextView));

			TextView.Properties [typeof(ViewContent)] = this;
			ContentName = fileName;

			InstallAdditionalEditorOperationsCommands ();

			SubscribeToEvents ();
		}

		protected abstract TView CreateTextView (ITextViewModel viewModel, ITextViewRoleSet roles);

		// FIXME: ideally we could access this via ITextViewFactoryService
		// but it hasn't been upstreamed to Windows yet
		protected abstract ITextViewRoleSet GetAllPredefinedRoles ();

		protected abstract Components.Control CreateControl ();

		Components.Control control;
		public override Components.Control Control => control;

		public override void Dispose ()
		{
			UnsubscribeFromEvents ();
			TextDocument.Dispose ();
			base.Dispose ();
		}

		protected virtual void SubscribeToEvents ()
		{
			sourceEditorOptions.Changed += HandleSourceEditorOptionsChanged;
			TextDocument.DirtyStateChanged += HandleTextDocumentDirtyStateChanged;
			TextView.Caret.PositionChanged += CaretPositionChanged;
			TextView.TextBuffer.Changed += TextBufferChanged;
		}

		protected virtual void UnsubscribeFromEvents ()
		{
			sourceEditorOptions.Changed -= HandleSourceEditorOptionsChanged;
			TextDocument.DirtyStateChanged -= HandleTextDocumentDirtyStateChanged;
			TextView.Caret.PositionChanged -= CaretPositionChanged;
			TextView.TextBuffer.Changed -= TextBufferChanged;
		}

		void HandleSourceEditorOptionsChanged (object sender, EventArgs e)
		{
			Imports.EditorOptionsFactoryService.GlobalOptions.SetOptionValue (
				DefaultTextViewHostOptions.LineNumberMarginId,
				sourceEditorOptions.ShowLineNumberMargin);
		}

		protected override object OnGetContent (Type type)
		{
			foreach (var provider in contentProviders) {
				var content = provider.GetContent (TextView, type);
				if (content != null) {
					return content;
				}
			}
			return GetIntrinsicType (type);
		}

		protected override IEnumerable<object> OnGetContents (Type type)
		{
			foreach (var provider in contentProviders) {
				var contents = provider.GetContents (TextView, type);
				if (contents != null) {
					foreach (var content in contents)
						yield return content;
				}
			}

			var intrinsicType = GetIntrinsicType (type);
			if (intrinsicType != null) {
				yield return intrinsicType;
			}
		}

		object GetIntrinsicType (Type type)
		{
			if (type.IsInstanceOfType (TextBuffer))
				return TextBuffer;
			if (type.IsInstanceOfType (TextDocument))
				return TextDocument;
			if (type.IsInstanceOfType (TextView))
				return TextView;
			if (type.IsInstanceOfType (this))
				return this;
			return null;
		}

		public override Task Save ()
		{
			TextDocument.Save ();
			return Task.CompletedTask;
		}

		public override Task Save (FileSaveInformation fileSaveInformation)
		{
			TextDocument.SaveAs (fileSaveInformation.FileName, overwrite: true);
			return Task.CompletedTask;
		}

		public override bool IsDirty => TextDocument.IsDirty;

		void HandleTextDocumentDirtyStateChanged (object sender, EventArgs e)
			=> OnDirtyChanged ();

		static readonly string[] textContentType = { "text" };

		IContentType GetContentTypeFromMimeType (string filePath, string mimeType)
			=> Ide.MimeTypeCatalog.Instance.GetContentTypeForMimeType (mimeType)
				?? (fileName != null ? Ide.Composition.CompositionManager.GetExportedValue<IFileToContentTypeService> ().GetContentTypeForFilePath (fileName) : null)
				?? Microsoft.VisualStudio.Platform.PlatformCatalog.Instance.ContentTypeRegistryService.UnknownContentType;

		void CaretPositionChanged (object sender, CaretPositionChangedEventArgs e)
		{
			TryLogNavPoint (true);
		}

		void TextBufferChanged (object sender, TextContentChangedEventArgs e)
		{
			TryLogNavPoint (false);
		}
	}
}