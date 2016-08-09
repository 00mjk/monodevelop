﻿using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.RepresentationModel;
using System.Linq;
using MonoDevelop.Ide.Editor.Highlighting.RegexEngine;
using System.Collections.Immutable;
using MonoDevelop.Core;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading;
using MonoDevelop.Core.Text;

namespace MonoDevelop.Ide.Editor.Highlighting
{
	public class SyntaxHighlighting : ISyntaxHighlighting
	{
		readonly SyntaxHighlightingDefinition definition;

		public IReadonlyTextDocument Document {
			get;
			set;
		}

		internal SyntaxHighlightingDefinition Definition {
			get {
				return definition;
			}
		}

		public SyntaxHighlighting (SyntaxHighlightingDefinition definition, IReadonlyTextDocument document)
		{
			this.definition = definition;
			Document = document;
			if (document is ITextDocument)
				((ITextDocument)document).TextChanged += Handle_TextChanged;
		}

		async void Handle_TextChanged (object sender, Core.Text.TextChangeEventArgs e)
		{
			var ln = Document.OffsetToLineNumber (e.Offset);
			if (ln >= stateCache.Count)
				return;
			var line = Document.GetLineByOffset (e.Offset);
			var lastState = GetState (line); 

			var high = new Highlighter (this, lastState);
			await high.GetColoredSegments (line.Offset, line.LengthIncludingDelimiter);

			if (!stateCache [ln].Equals (high.State)) {
				stateCache.RemoveRange (ln - 1, stateCache.Count - ln + 1);
			}
		}

		public Task<HighlightedLine> GetHighlightedLineAsync (IDocumentLine line, CancellationToken cancellationToken)
		{
			if (Document == null) {
				return DefaultSyntaxHighlighting.Instance.GetHighlightedLineAsync (line, cancellationToken);
			}
			var high = new Highlighter (this, GetState (line));
			return high.GetColoredSegments (line.Offset, line.Length);
		}

		public async Task<ImmutableStack<string>> GetScopeStackAsync (int offset, CancellationToken cancellationToken)
		{
			var line = Document.GetLineByOffset (offset);
			var state = GetState (line);

			if (line.Offset == offset) 
				return state.ScopeStack;

			var high = new Highlighter (this, state);
			foreach (var seg in (await high.GetColoredSegments (line.Offset, line.Length)).Segments) {
				if (seg.Contains (offset))
					return seg.ScopeStack;
			}
			return high.State.ScopeStack;
		}

		List<HighlightState> stateCache = new List<HighlightState> ();

		HighlightState GetState (IDocumentLine line)
		{
 			var pl = line.PreviousLine;
			if (pl == null)
				return HighlightState.CreateNewState (this);
			if (stateCache.Count == 0)
				stateCache.Add (HighlightState.CreateNewState (this));
			var ln = line.LineNumber;
			if (ln <= stateCache.Count) {
				return stateCache [ln - 1].Clone ();
			}

			var lastState = stateCache [stateCache.Count - 1];
			var cur = Document.GetLine (stateCache.Count);
			if (cur != null && cur.Offset < line.Offset) {
				do {
					var high = new Highlighter (this, lastState.Clone ());
					high.GetColoredSegments (cur.Offset, cur.LengthIncludingDelimiter).Wait ();
					stateCache.Add (lastState = high.State);
					cur = cur.NextLine;
				} while (cur != null && cur.Offset < line.Offset);
			}

			return lastState.Clone ();
		}

			
		class HighlightState : IEquatable<HighlightState>
		{
			public ImmutableStack<SyntaxContext> ContextStack;
			public ImmutableStack<SyntaxMatch> MatchStack;
			public ImmutableStack<string> ScopeStack;


			public static HighlightState CreateNewState (SyntaxHighlighting highlighting)
			{
				return new HighlightState {
					ContextStack = ImmutableStack<SyntaxContext>.Empty.Push (highlighting.Definition.GetContext ("main")),
					ScopeStack = ImmutableStack<string>.Empty.Push (highlighting.definition.Scope),
					MatchStack = ImmutableStack<SyntaxMatch>.Empty
				};
			}


			internal HighlightState Clone ()
			{
				return new HighlightState {
					ContextStack = this.ContextStack,
					ScopeStack = this.ScopeStack,
					MatchStack = this.MatchStack
				};
			}


			public bool Equals (HighlightState other)
			{
				return ContextStack.SequenceEqual (other.ContextStack) && ScopeStack.SequenceEqual (other.ScopeStack) && MatchStack.SequenceEqual (other.MatchStack);
			}
		}

		class Highlighter
		{
			HighlightState state;
			SyntaxHighlighting highlighting;
			ImmutableStack<SyntaxContext> ContextStack { get { return state.ContextStack; } set { state.ContextStack = value; } }
			ImmutableStack<SyntaxMatch> MatchStack { get { return state.MatchStack; } set { state.MatchStack = value; } }
			ImmutableStack<string> ScopeStack { get { return state.ScopeStack; } set { state.ScopeStack = value; } }

			public HighlightState State {
				get {
					return state;
				}
			}

			public Highlighter (SyntaxHighlighting highlighting, HighlightState state)
			{
				this.highlighting = highlighting;
				this.state = state;
			}

			public Task<HighlightedLine> GetColoredSegments (int offset, int length)
			{
				SyntaxContext currentContext = null;
				List<SyntaxContext> lastContexts = new List<SyntaxContext> ();
				Match match = null;
				SyntaxMatch curMatch = null;
				var segments = new List<ColoredSegment> ();
				int curSegmentOffset = offset;
				int endOffset = offset + length;
				int lastMatch = -1;
			restart:
				if (lastMatch == offset) {
					if (lastContexts.Contains (currentContext)) {
						offset++;
						length--;
						if (length <= 0)
							goto end;
					} else {
						lastContexts.Add (currentContext);
					}
				} else {
					lastContexts.Clear ();
					lastContexts.Add (currentContext);
				}
				lastMatch = offset;

				currentContext = ContextStack.Peek ();
				match = null;
				curMatch = null;
				foreach (var m in currentContext.Matches) {
					var r = m.GetRegex ();
					if (r == null)
						continue;
					var possibleMatch = r.Match (highlighting.Document, offset, length);
					if (possibleMatch.Success) {
						if (match == null || possibleMatch.Index < match.Index) {
							match = possibleMatch;
							curMatch = m;
							// Console.WriteLine (match.Index + " possible match : " + m+ "/" + possibleMatch.Index + "-" + possibleMatch.Length);
						} else {
							// Console.WriteLine (match.Index + " skip match : " + m + "/" + possibleMatch.Index + "-" + possibleMatch.Length);
						}
					} else {
						// Console.WriteLine ("fail match : " + m);
					}
				}

				if (match != null) {
					Console.WriteLine (match.Index + " match : " + curMatch + "/" + match.Index + "-" + match.Length);
					var matchEndOffset = match.Index + match.Length;
					if (curSegmentOffset < match.Index) {
						segments.Add (new ColoredSegment (curSegmentOffset, match.Index - curSegmentOffset, ScopeStack));
						curSegmentOffset = match.Index;
					}
					PushScopeStack (curMatch.Scope);

					if (curMatch.Captures.Count > 0) {
						foreach (var capture in curMatch.Captures) {
							var grp = match.Groups [capture.Item1];
							if (grp.Length == 0)
								continue;
							if (curSegmentOffset < grp.Index) {
								Insert (segments, new ColoredSegment (curSegmentOffset, grp.Index - curSegmentOffset, ScopeStack));
							}
							Insert (segments, new ColoredSegment (grp.Index, grp.Length, ScopeStack.Push (capture.Item2)));
							curSegmentOffset = grp.Index + grp.Length;
						}
					}

					if (curMatch.Scope.Count > 0 && curSegmentOffset < matchEndOffset) {
						segments.Add (new ColoredSegment (curSegmentOffset, matchEndOffset - curSegmentOffset, ScopeStack));
						curSegmentOffset = matchEndOffset;
					}

					if (curMatch.Pop) {
						if (matchEndOffset - curSegmentOffset > 0)
							segments.Add (new ColoredSegment (curSegmentOffset, matchEndOffset - curSegmentOffset, ScopeStack));
						//if (curMatch.Scope != null)
						//	scopeStack = scopeStack.Pop ();
						PopStack (currentContext, curMatch);

						curSegmentOffset = matchEndOffset;
					} else if (curMatch.Set != null) {
						// if (matchEndOffset - curSegmentOffset > 0)
						//	segments.Add (new ColoredSegment (curSegmentOffset, matchEndOffset - curSegmentOffset, ScopeStack));
						//if (curMatch.Scope != null)
						//	scopeStack = scopeStack.Pop ();
						PopStack (currentContext, curMatch);
						curSegmentOffset = matchEndOffset;
						var nextContexts = curMatch.Set.GetContexts (highlighting.Definition);
						PushStack (curMatch, nextContexts);
					} else if (curMatch.Push != null) {
						var nextContexts = curMatch.Push.GetContexts (highlighting.Definition);
						PushStack (curMatch, nextContexts);
					} else {
						if (curMatch.Scope.Count > 0) {
							for (int i = 0; i < curMatch.Scope.Count; i++)
								ScopeStack = ScopeStack.Pop ();
						}
					}

					if (curSegmentOffset < matchEndOffset) {
						segments.Add (new ColoredSegment (curSegmentOffset, matchEndOffset - curSegmentOffset, ScopeStack));
						curSegmentOffset = matchEndOffset;
					}

					length -= curSegmentOffset - offset;
					offset = curSegmentOffset;
					goto restart;
				}

				end:
				if (endOffset - curSegmentOffset > 0) {
					segments.Add (new ColoredSegment (curSegmentOffset, endOffset - curSegmentOffset, ScopeStack));
				}
				return Task.FromResult (new HighlightedLine (segments));
			}

			void PushStack (SyntaxMatch curMatch, IEnumerable<SyntaxContext> nextContexts)
			{
				if (nextContexts != null) {
					bool first = true;
					foreach (var nextContext in nextContexts) {
						if (first) {
							MatchStack = MatchStack.Push (curMatch);
							first = false;
						} else {
							MatchStack = MatchStack.Push (null);
						}
						ContextStack = ContextStack.Push (nextContext);
						PushScopeStack (nextContext.MetaScope);
						PushScopeStack (nextContext.MetaContentScope);
					}
				}
			}

			void PushScopeStack (IReadOnlyList<string> scopeList)
			{
				if (scopeList == null)
					return;
				foreach (var scope in scopeList)
					ScopeStack = ScopeStack.Push (scope);
			}

			void PopScopeStack (IReadOnlyList<string> scopeList)
			{
				if (scopeList == null)
					return;
				for (int i = 0; !ScopeStack.IsEmpty && i < scopeList.Count; i++)
					ScopeStack = ScopeStack.Pop ();
			}
			


			void PopStack (SyntaxContext currentContext, SyntaxMatch curMatch)
			{
				if (ContextStack.Count () == 1) {
					MatchStack = MatchStack.Clear ();
					ScopeStack = ImmutableStack<string>.Empty.Push (highlighting.definition.Scope);
					return;
				}
 				ContextStack = ContextStack.Pop ();
				if (!MatchStack.IsEmpty) {
					PopScopeStack (MatchStack.Peek ()?.Scope); 
					MatchStack = MatchStack.Pop ();
				}
				PopScopeStack (currentContext.MetaScope);
				PopScopeStack (currentContext.MetaContentScope);

				if (curMatch.Scope.Count > 0 && !ScopeStack.IsEmpty) {
					for (int i = 0; i < curMatch.Scope.Count; i++)
						ScopeStack = ScopeStack.Pop ();
				}
			}

	}

		static void Insert (List<ColoredSegment> list, ColoredSegment newSegment)
		{
			if (list.Count == 0) {
				list.Add (newSegment);
				return;
			}
			int i = list.Count;
			while (i > 0 && list [i - 1].EndOffset > newSegment.Offset) {
				i--;
			}
			if (i >= list.Count) {
				list.Add (newSegment);
				return;
			}
			var item = list [i];


			if (newSegment.EndOffset - item.EndOffset > 0)
				list.Insert (i + 1, new ColoredSegment(newSegment.EndOffset, newSegment.EndOffset - item.EndOffset, item.ScopeStack));
			
			list.Insert (i + 1, newSegment);
			list [i] = new ColoredSegment (item.Offset, newSegment.Offset - item.Offset, item.ScopeStack);
		}

		internal SyntaxContext GetContext (string name)
		{
			return definition.GetContext (name);
		}
	}
}