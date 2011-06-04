// 
// ReplaceEmptyString.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
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
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.PatternMatching;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Core;

namespace MonoDevelop.CSharp.ContextAction
{
	public class ReplaceEmptyString : CSharpContextAction
	{
		protected override string GetMenuText (CSharpContext context)
		{
			return GettextCatalog.GetString ("Use string.Empty");
		}
		
		protected override bool IsValid (CSharpContext context)
		{
			return GetEmptyString (context) != null;
		}
		
		protected override void Run (CSharpContext context)
		{
			var expr = GetEmptyString (context);
			
			int offset = context.Document.Editor.LocationToOffset (expr.StartLocation.Line, expr.StartLocation.Column);
			int endOffset = context.Document.Editor.LocationToOffset (expr.EndLocation.Line, expr.EndLocation.Column);
			
			string text = "string.Empty";
			context.Document.Editor.Replace (offset, endOffset - offset, text);
			context.Document.Editor.Caret.Offset = offset + text.Length;
		}
		
		PrimitiveExpression GetEmptyString (CSharpContext context)
		{
			var astNode = context.GetNode<PrimitiveExpression> ();
			if (astNode == null || !(astNode.Value is string) || astNode.Value.ToString () != "")
				return null;
			return  astNode;
		}
	}
}

