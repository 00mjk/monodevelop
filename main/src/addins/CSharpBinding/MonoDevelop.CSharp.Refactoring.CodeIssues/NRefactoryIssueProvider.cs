// 
// NRefactoryIssueWrapper.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
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
using ICSharpCode.NRefactory.CSharp;
using MonoDevelop.Ide.Gui;
using System.Threading;
using MonoDevelop.CodeIssues;
using MonoDevelop.CSharp.Refactoring.CodeActions;
using MonoDevelop.Core;

namespace MonoDevelop.CSharp.Refactoring.CodeIssues
{
	class NRefactoryIssueProvider : CodeIssueProvider
	{
		ICSharpCode.NRefactory.CSharp.Refactoring.ICodeIssueProvider issueProvider;

		public override string IdString {
			get {
				return "refactoring.inspectors." + MimeType + "." + issueProvider.GetType ().FullName;
			}
		}

		public NRefactoryIssueProvider (ICSharpCode.NRefactory.CSharp.Refactoring.ICodeIssueProvider issue, IssueDescriptionAttribute attr)
		{
			issueProvider = issue;
			MimeType = "text/x-csharp";
			Category = GettextCatalog.GetString (attr.Category ?? "");
			Title = GettextCatalog.GetString (attr.Title ?? "");
			Description = GettextCatalog.GetString (attr.Description ?? "");
			DefaultSeverity = attr.Severity;
			IssueMarker = attr.IssueMarker;
		}

		public override IEnumerable<CodeIssue> GetIssues (Document document, CancellationToken cancellationToken)
		{
			var context = new MDRefactoringContext (document, document.Editor.Caret.Location);
			foreach (var action in issueProvider.GetIssues (context)) {
				if (action.Actions == null) {
					LoggingService.LogError ("NRefactory actions == null in :" + Title);
					continue;
				}

				var issue = new CodeIssue (
					GettextCatalog.GetString (action.Desription ?? ""),
					action.Start,
					action.End,
					action.Actions.Select (act => {
						if (act == null) {
							LoggingService.LogError ("NRefactory issue action was null in :" + Title);
							return null;
						}
						return new NRefactoryCodeAction (act.Description, act);
					}
				));
				yield return issue;
			}
		}
	}
}