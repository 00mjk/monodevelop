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

using System.Windows;
using AppKit;
using Microsoft.VisualStudio.Text.Classification;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;

namespace MonoDevelop.TextEditor
{
	class CocoaTextViewDisplayBinding : TextViewDisplayBinding<CocoaTextViewImports>
	{
		protected override ViewContent CreateContent (CocoaTextViewImports imports, FilePath fileName, string mimeType, Project ownerProject)
		{
			return new CocoaTextViewContent (imports, fileName, mimeType, ownerProject);
		}

		protected override ThemeToClassification CreateThemeToClassification (IEditorFormatMapService editorFormatMapService)
			=> new CocoaThemeToClassification (editorFormatMapService);

		class CocoaThemeToClassification : ThemeToClassification
		{
			public CocoaThemeToClassification (IEditorFormatMapService editorFormatMapService) : base (editorFormatMapService) {}

			protected override void AddFontToDictionary (ResourceDictionary resourceDictionary, string fontName, int fontSize)
			{
				resourceDictionary[ClassificationFormatDefinition.TypefaceId] = NSFontWorkarounds.FromFontName (fontName, fontSize);
			}
		}
	}
}