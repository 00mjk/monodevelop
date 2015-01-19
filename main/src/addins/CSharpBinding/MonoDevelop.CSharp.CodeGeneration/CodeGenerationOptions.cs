// 
// CodeGenerationOptions.cs
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
using System.Linq;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Core;
using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Simplification;
using MonoDevelop.Ide.Editor;
using Microsoft.CodeAnalysis.Options;
using MonoDevelop.Ide.Gui.Content;
using Microsoft.CodeAnalysis.Formatting;
using System.Diagnostics;

namespace MonoDevelop.CodeGeneration
{
	public class CodeGenerationOptions
	{
		readonly int offset;

		public TextEditor Editor {
			get;
			private set;
		}

		public DocumentContext DocumentContext {
			get;
			private set;
		}

		public ITypeSymbol EnclosingType {
			get;
			private set;
		}

		public SyntaxNode EnclosingMemberSyntax {
			get;
			private set;
		}

		public TypeDeclarationSyntax EnclosingPart {
			get;
			private set;
		}
		
		public ISymbol EnclosingMember {
			get;
			private set;
		}
		
		public string MimeType {
			get {
				return DesktopService.GetMimeTypeForUri (DocumentContext.Name);
			}
		}
		
		public OptionSet FormattingOptions {
			get {
				var doc = DocumentContext;
				var policyParent = doc.Project != null ? doc.Project.Policies : null;
				var types = DesktopService.GetMimeTypeInheritanceChain (Editor.MimeType);
				var codePolicy = policyParent != null ? policyParent.Get<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types) : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<MonoDevelop.CSharp.Formatting.CSharpFormattingPolicy> (types);
				var textPolicy = policyParent != null ? policyParent.Get<TextStylePolicy> (types) : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> (types);
				return codePolicy.CreateOptions (textPolicy);
			}
		}

		public SemanticModel CurrentState {
			get;
			private set;
		}

		internal CodeGenerationOptions (TextEditor editor,  DocumentContext ctx)
		{
			Editor = editor;
			DocumentContext = ctx;
			var analysisDocument = ctx.AnalysisDocument;
			if (analysisDocument != null)
				CurrentState = analysisDocument.GetSemanticModelAsync ().Result;
			offset = editor.CaretOffset;
			var node = CurrentState.SyntaxTree.GetRoot ().FindNode (TextSpan.FromBounds (offset, offset));
			EnclosingMemberSyntax = node.AncestorsAndSelf ().OfType<MemberDeclarationSyntax> ().FirstOrDefault ();
			if (EnclosingMemberSyntax != null)
				EnclosingMember = CurrentState.GetDeclaredSymbol (EnclosingMemberSyntax);

			EnclosingPart = node.AncestorsAndSelf ().OfType<TypeDeclarationSyntax> ().FirstOrDefault ();
			if (EnclosingPart != null)
				EnclosingType = CurrentState.GetDeclaredSymbol (EnclosingPart) as ITypeSymbol;
		}
		
		public string CreateShortType (ITypeSymbol fullType)
		{
			return fullType.ToMinimalDisplayString (CurrentState, offset);
		}
		
		public CodeGenerator CreateCodeGenerator ()
		{
			var result = CodeGenerator.CreateGenerator (Editor, DocumentContext);
			if (result == null)
				LoggingService.LogError ("Generator can't be generated for : " + Editor.MimeType);
			return result;
		}
		
		public static CodeGenerationOptions CreateCodeGenerationOptions (TextEditor document, DocumentContext ctx)
		{
			return new CodeGenerationOptions (document, ctx);
		}
		
		public async Task<string> OutputNode (SyntaxNode node, CancellationToken cancellationToken = default(CancellationToken))
		{
			node = node.WithAdditionalAnnotations (Formatter.Annotation, Simplifier.Annotation);

			var text = Editor.Text;
			string nodeText = node.ToString ();
			text = text.Insert (offset, nodeText);


			var backgroundDocument = DocumentContext.AnalysisDocument.WithText (SourceText.From (text));

			var currentRoot = await backgroundDocument.GetSyntaxRootAsync (cancellationToken);

			node = currentRoot.FindNode (TextSpan.FromBounds(offset, offset + nodeText.Length));

			currentRoot = currentRoot.TrackNodes (node);
			backgroundDocument = backgroundDocument.WithSyntaxRoot (currentRoot);
			backgroundDocument = await Simplifier.ReduceAsync (backgroundDocument, TextSpan.FromBounds (offset, offset + nodeText.Length), FormattingOptions, cancellationToken).ConfigureAwait(false);
			backgroundDocument = await Formatter.FormatAsync (backgroundDocument, Formatter.Annotation, FormattingOptions, cancellationToken).ConfigureAwait(false);

			var newRoot = await backgroundDocument.GetSyntaxRootAsync (cancellationToken);

			var formattedNode = newRoot.GetCurrentNode (node);
			if (formattedNode == null) {
				LoggingService.LogError ("Fatal error: Can't find current formatted node in code generator document.");
				return nodeText;
			}
			return formattedNode.ToString ();
		}
	}
}
