// 
// AbstractRefactoringCommandHandler.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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

using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide;
using ICSharpCode.NRefactory.CSharp.Refactoring;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;

namespace MonoDevelop.Refactoring
{
	public abstract class AbstractRefactoringCommandHandler : CommandHandler
	{
		protected abstract void Run (RefactoringOptions options);
		public void Start (object data)
		{
			Run (data);
		}
		protected override void Run (object data)
		{
			Document doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return;
			
			ITextBuffer editor = doc.GetContent<ITextBuffer> ();
			if (editor == null)
				return;
			
			ResolveResult result;
			var item = CurrentRefactoryOperationsHandler.GetItem (doc, out result);
			
			RefactoringOptions options = new RefactoringOptions () {
				Document = doc,
				ResolveResult = result,
				SelectedItem = item
			};
			Run (options);
		}
	}
}
