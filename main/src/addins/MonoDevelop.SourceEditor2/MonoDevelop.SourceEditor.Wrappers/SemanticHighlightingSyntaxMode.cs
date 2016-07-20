﻿//
// SemanticHighlightingSyntaxMode.cs
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
using Mono.TextEditor.Highlighting;
using MonoDevelop.Ide.Editor.Highlighting;
using Mono.TextEditor;
using System.Collections.Generic;
using MonoDevelop.Ide.Editor;
using System.Linq;
using Gtk;

namespace MonoDevelop.SourceEditor.Wrappers
{
	sealed class SemanticHighlightingSyntaxMode : ISyntaxHighlighting, IDisposable
	{
		readonly ExtensibleTextEditor editor;
		readonly ISyntaxHighlighting syntaxMode;
		SemanticHighlighting semanticHighlighting;

		public ISyntaxHighlighting UnderlyingSyntaxMode {
			get {
				return this.syntaxMode;
			}
		}

		internal class StyledTreeSegment : Mono.TextEditor.TreeSegment
		{
			public string Style {
				get;
				private set;
			}

			public StyledTreeSegment (int offset, int length, string style) : base (offset, length)
			{
				Style = style;
			}

			public override string ToString ()
			{
				return string.Format ($"[StyledTreeSegment: Offset={Offset}, Length={Length}, Style={Style}]");
			}
		}

		class HighlightingSegmentTree : Mono.TextEditor.SegmentTree<StyledTreeSegment>
		{
			public bool GetStyle (Chunk chunk, ref int endOffset, out string style)
			{
				var segment = GetSegmentsAt (chunk.Offset).FirstOrDefault (s => s.EndOffset > chunk.Offset);
				if (segment == null) {
					style = null;
					return false;
				}
				endOffset = segment.EndOffset;
				style = segment.Style;
				return true;
			}

			public void AddStyle (MonoDevelop.Core.Text.ISegment segment, string style)
			{
				if (IsDirty)
					return;
				Add (new StyledTreeSegment (segment.Offset, segment.Length, style));
			}
		}

		bool isDisposed;
		Queue<Tuple<IDocumentLine, HighlightingSegmentTree>> lineSegments = new Queue<Tuple<IDocumentLine, HighlightingSegmentTree>> ();

		public SemanticHighlightingSyntaxMode (ExtensibleTextEditor editor, ISyntaxHighlighting syntaxMode, SemanticHighlighting semanticHighlighting)
		{
			if (editor == null)
				throw new ArgumentNullException ("editor");
			if (syntaxMode == null)
				throw new ArgumentNullException ("syntaxMode");
			if (semanticHighlighting == null)
				throw new ArgumentNullException ("semanticHighlighting");
			this.editor = editor;
			this.semanticHighlighting = semanticHighlighting;
			this.syntaxMode = syntaxMode;
			semanticHighlighting.SemanticHighlightingUpdated += SemanticHighlighting_SemanticHighlightingUpdated;
		}

		public void UpdateSemanticHighlighting (SemanticHighlighting newHighlighting)
		{
			if (isDisposed)
				return;
			if (semanticHighlighting !=null)
				semanticHighlighting.SemanticHighlightingUpdated -= SemanticHighlighting_SemanticHighlightingUpdated;
			semanticHighlighting = newHighlighting;
			if (semanticHighlighting !=null)
				semanticHighlighting.SemanticHighlightingUpdated += SemanticHighlighting_SemanticHighlightingUpdated;
		}

		void SemanticHighlighting_SemanticHighlightingUpdated (object sender, EventArgs e)
		{
			Application.Invoke (delegate {
				if (isDisposed)
					return;
				UnregisterLineSegmentTrees ();
				lineSegments.Clear ();

				var margin = editor.TextViewMargin;
				if (margin == null)
					return;
				margin.PurgeLayoutCache ();
				editor.QueueDraw ();
			});
		}

		void UnregisterLineSegmentTrees ()
		{
			if (isDisposed)
				return;
			foreach (var kv in lineSegments) {
				try {
					kv.Item2.RemoveListener ();
				} catch (Exception) {
				}
			}
		}

		public void Dispose()
		{
			if (isDisposed)
				return;
			isDisposed = true;
			UnregisterLineSegmentTrees ();
			lineSegments = null;
			semanticHighlighting.SemanticHighlightingUpdated -= SemanticHighlighting_SemanticHighlightingUpdated;
		}

		const int MaximumCachedLineSegments = 200;

		IEnumerable<ColoredSegment> ISyntaxHighlighting.GetColoredSegments (IDocumentLine line, int offset, int length)
		{
			if (!DefaultSourceEditorOptions.Instance.EnableSemanticHighlighting) {
				foreach (var chunk in syntaxMode.GetColoredSegments (line, offset, length)) {
					yield return chunk;
				}
				yield break;
			}

			foreach (var seg in syntaxMode.GetColoredSegments (line, offset, length)) {
				StyledTreeSegment treeseg = null;
				try {
					var tree = lineSegments.FirstOrDefault (t => t.Item1 == line);
					if (tree == null) {
						tree = Tuple.Create (line, new HighlightingSegmentTree ());
						tree.Item2.InstallListener (editor.Document);
						int lineOffset = line.Offset;
						foreach (var seg2 in semanticHighlighting.GetColoredSegments (new MonoDevelop.Core.Text.TextSegment (lineOffset, line.Length))) {
							tree.Item2.AddStyle (seg2, seg2.ColorStyleKey);
						}
						while (lineSegments.Count > MaximumCachedLineSegments) {
							var removed = lineSegments.Dequeue ();
							try {
								removed.Item2.RemoveListener ();
							} catch (Exception) { }
						}
						lineSegments.Enqueue (tree);
					}
					treeseg = tree.Item2.GetSegmentsOverlapping (seg).FirstOrDefault (s => s.Offset < seg.EndOffset && s.EndOffset > seg.Offset);
				} catch (Exception e) {
					Console.WriteLine ("Error in semantic highlighting: " + e);
				}
				if (treeseg != null) {
					if (seg.Offset <= treeseg.Offset) {
						var lengthBefore = treeseg.Offset - seg.Offset;
						if (lengthBefore > 0)
							yield return new ColoredSegment (seg.Offset, lengthBefore, seg.ScopeStack);
						yield return new ColoredSegment (treeseg.Offset, treeseg.Length, seg.ScopeStack.Push (treeseg.Style));
						var lengthAfter = seg.EndOffset - treeseg.EndOffset;
						if (lengthAfter > 0)
							yield return new ColoredSegment (treeseg.EndOffset, lengthAfter, seg.ScopeStack);
					} else if (seg.EndOffset < treeseg.EndOffset) {
						continue;
					} else {
						var lengthAfter = seg.EndOffset - treeseg.EndOffset;
						if (lengthAfter > 0)
							yield return new ColoredSegment (treeseg.EndOffset, lengthAfter, seg.ScopeStack);
					}
				} else {
					yield return seg;
				}
			}
		}
	}
}