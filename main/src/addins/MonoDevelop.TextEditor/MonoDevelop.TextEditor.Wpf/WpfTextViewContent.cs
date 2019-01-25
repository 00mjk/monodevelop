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

using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Components;

namespace MonoDevelop.TextEditor
{
	class WpfTextViewContent : TextViewContent<IWpfTextView, WpfTextViewImports>
	{
		IWpfTextViewHost wpfTextViewHost;

		public WpfTextViewContent (WpfTextViewImports imports, Core.FilePath fileName, string mimeType, Projects.Project ownerProject)
			: base (imports, fileName, mimeType, ownerProject)
		{
		}

		protected override IWpfTextView CreateTextView (ITextViewModel viewModel, ITextViewRoleSet roles)
			=> Imports.TextEditorFactoryService.CreateTextView (viewModel, roles, Imports.EditorOptionsFactoryService.GlobalOptions);

		protected override Control CreateControl ()
		{
			wpfTextViewHost = Imports.TextEditorFactoryService.CreateTextViewHost (TextView, setFocus: true);
			var wpfControl = wpfTextViewHost.HostControl;

			Gtk.Widget widget = new RootWpfWidget (wpfControl) {
				HeightRequest = 50,
				WidthRequest = 100
			};

			TextView.VisualElement.Tag = widget;

			var xwtWidget = Xwt.Toolkit.CurrentEngine.WrapWidget (widget, Xwt.NativeWidgetSizing.External);
			xwtWidget.Show ();

			return new XwtControl (xwtWidget);
		}

		protected override ITextViewRoleSet GetAllPredefinedRoles () => Imports.TextEditorFactoryService.AllPredefinedRoles;

		protected override void SubscribeToEvents ()
		{
			base.SubscribeToEvents ();
			TextView.VisualElement.LostKeyboardFocus += HandleWpfLostKeyboardFocus;
		}

		protected override void UnsubscribeFromEvents ()
		{
			base.UnsubscribeFromEvents ();
			TextView.VisualElement.LostKeyboardFocus -= HandleWpfLostKeyboardFocus;
		}

		void HandleWpfLostKeyboardFocus (object sender, KeyboardFocusChangedEventArgs e)
			=> Components.Commands.CommandManager.LastFocusedWpfElement = TextView.VisualElement;

		public override void Dispose ()
		{
			base.Dispose ();
			if (wpfTextViewHost != null) {
				wpfTextViewHost.Close ();
				wpfTextViewHost = null;
			}
		}
	}
}