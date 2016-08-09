//
// SyntaxHighlightingDefinition.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
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
using System.IO;
using YamlDotNet.RepresentationModel;
using System.Linq;
using MonoDevelop.Ide.Editor.Highlighting.RegexEngine;
using System.Collections.Immutable;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.Editor.Highlighting
{
	public class SyntaxHighlightingDefinition
	{
		public string Name { get; internal set; }

		readonly List<string> extensions;
		public IReadOnlyList<string> FileExtensions { get { return extensions; } }

		public string Scope { get; internal set; }

		public bool Hidden { get; internal set; }

		public string FirstLineMatch { get; internal set; }

		readonly List<SyntaxContext> contexts;
		public IReadOnlyList<SyntaxContext> Contexts { get { return contexts; } }

		internal SyntaxHighlightingDefinition (string name, string scope, string firstLineMatch, bool hidden, List<string> extensions, List<SyntaxContext> contexts)
		{
			this.extensions = extensions;
			this.contexts = contexts;
			Name = name;
			Scope = scope;
			FirstLineMatch = firstLineMatch;
			Hidden = hidden;

			foreach (var ctx in Contexts) {
				ctx.PrepareMatches (this);
			}
		}

		internal SyntaxContext GetContext (string name)
		{
			foreach (var ctx in Contexts) {
				if (ctx.Name == name)
					return ctx;
			}
			return null;
		}
	}

	public class SyntaxContext
	{
		List<SyntaxMatch> matches;

		public string Name { get; private set; }

		List<string> metaScope = new List<string> ();
		public IReadOnlyList<string> MetaScope { get { return metaScope; } }

		List<string> metaContentScope = new List<string> ();
		public IReadOnlyList<string> MetaContentScope { get { return metaContentScope; } }


		public bool MetaIncludePrototype { get; private set; }

		public IEnumerable<SyntaxMatch> Matches { get { return matches; } }

		readonly List<object> includesAndMatches;

		internal void ParseMapping (YamlSequenceNode seqNode, Dictionary<string, string> variables)
		{
			if (seqNode != null) {
				foreach (var node in seqNode.Children.OfType<YamlMappingNode> ()) {
					ParseMapping (node, variables);
				}
			}

			//var scalarNode = mapping.Value as YamlScalarNode;
			//if (scalarNode != null) {
			//	Console.WriteLine (mapping.Key +"/"+scalarNode.Value);
			//}
		}

		internal void ParseMapping (YamlMappingNode node, Dictionary<string, string> variables)
		{
			var children = node.Children;
			if (children.ContainsKey (new YamlScalarNode ("match"))) {
				includesAndMatches.Add (Sublime3Format.ReadMatch (node, variables));
				return;
			}

			YamlNode val;
			if (children.TryGetValue (new YamlScalarNode ("meta_scope"), out val)) {
				Sublime3Format.ParseScopes (metaScope, ((YamlScalarNode)val).Value);
			}
			if (children.TryGetValue (new YamlScalarNode ("meta_content_scope"), out val)) {
				Sublime3Format.ParseScopes (metaContentScope, ((YamlScalarNode)val).Value);
			}
			if (children.TryGetValue (new YamlScalarNode ("meta_include_prototype"), out val)) {
				MetaIncludePrototype = ((YamlScalarNode)val).Value != "false";
			}
			if (children.TryGetValue (new YamlScalarNode ("include"), out val)) {
				includesAndMatches.Add (((YamlScalarNode)val).Value);
			}
		}

		internal SyntaxContext (string name)
		{
			Name = name;
			includesAndMatches = new List<object> ();
			MetaIncludePrototype = true;
		}

		internal SyntaxContext (string name, List<object> includesAndMatches, IReadOnlyList<string> metaScope = null, IReadOnlyList<string> metaContentScope = null, bool metaIncludePrototype = true)
		{
			this.includesAndMatches = includesAndMatches;
			Name = name;
			if (metaScope != null)
				this.metaScope.AddRange (metaScope);
			if (metaContentScope !=  null)
				this.metaContentScope.AddRange (metaScope);
			
			MetaIncludePrototype = metaIncludePrototype;
		}

		IEnumerable<SyntaxMatch> GetMatches (SyntaxHighlightingDefinition definition)
		{
			foreach (var o in includesAndMatches) {
				var match = o as SyntaxMatch;
				if (match != null) {
					yield return match;
					continue;
				}
				var include = o as string;
				var ctx = definition.GetContext (include);
				if (ctx == null) {
					LoggingService.LogWarning ($"highlighting {definition.Name} can't find include {include}.");
					continue;
				}
				foreach (var match2 in ctx.GetMatches (definition))
					yield return match2;
			}
		}

		internal void PrepareMatches(SyntaxHighlightingDefinition definiton)
		{
			var preparedMatches = new List<SyntaxMatch> ();
			IEnumerable<object> list = includesAndMatches;
			if (MetaIncludePrototype &&  Name != "prototype") {
				var prototypeContext = definiton.GetContext ("prototype");
				if (prototypeContext != null)
					list = list.Concat (prototypeContext.GetMatches (definiton));
			}
			foreach (var o in list) {
				var match = o as SyntaxMatch;
				if (match != null) {
					if (match.Push is AnonymousMatchContextReference)
						match.Push.GetContexts (definiton).First ().PrepareMatches (definiton);
					if (match.Set is AnonymousMatchContextReference)
						match.Set.GetContexts (definiton).First ().PrepareMatches (definiton);
					preparedMatches.Add (match);
					continue;
				}
				var include = o as string;
				var ctx = definiton.GetContext (include);
				if (ctx == null) {
					LoggingService.LogWarning ($"highlighting {definiton.Name} can't find include {include}.");
					continue;
				}
				preparedMatches.AddRange (ctx.GetMatches (definiton));
			}
			this.matches = preparedMatches;
		}

		public override string ToString ()
		{
			return string.Format ("[SyntaxContext: Name={0}, MetaScope={1}, MetaContentScope={2}, MetaIncludePrototype={3}]", Name, MetaScope, MetaContentScope, MetaIncludePrototype);
		}
	}

	public class SyntaxMatch
	{
		public string Match { get; private set; }
		public IReadOnlyList<string> Scope { get; private set; }
		public IReadOnlyList<Tuple<int, string>> Captures { get; private set; }
		public ContextReference Push { get; private set; }
		public bool Pop { get; private set; }
		public ContextReference Set { get; private set; }

		internal SyntaxMatch (string match, IReadOnlyList<string> scope, IReadOnlyList<Tuple<int, string>> captures, ContextReference push, bool pop, ContextReference set)
		{
			Match = match;
			Scope = scope;
			Captures = captures ?? new List<Tuple<int, string>> ();
			Push = push;
			Pop = pop;
			Set = set;
		}

		public override string ToString ()
		{
			return string.Format ("[SyntaxMatch: Match={0}, Scope={1}]", Match, Scope);
		}

		bool hasRegex;
		Regex cachedRegex;

		internal Regex GetRegex ()
		{
			if (hasRegex)
				return cachedRegex;
			hasRegex = true;
			try {
				cachedRegex = new Regex (Match);
			} catch (Exception e) {
				LoggingService.LogWarning ("Warning regex : '" + Match + "' can't be parsed.", e);
			}
			return cachedRegex;
		}
	}

	public abstract class ContextReference
	{
		public abstract IEnumerable<SyntaxContext> GetContexts (SyntaxHighlightingDefinition definiton);
	}

	public class ContextNameContextReference : ContextReference
	{

		public string Name { get; private set; }

		internal ContextNameContextReference (string value)
		{
			this.Name = value;
		}

		public override IEnumerable<SyntaxContext> GetContexts (SyntaxHighlightingDefinition definiton)
		{
			yield return definiton.GetContext (Name);
		}
	}

	public class ContextNameListContextReference : ContextReference
	{
		public ContextNameListContextReference (IReadOnlyList<string> names)
		{
			this.Names = names;
		}

		public IReadOnlyList<string> Names { get; private set; }

		public override IEnumerable<SyntaxContext> GetContexts (SyntaxHighlightingDefinition definiton)
		{
			foreach (var name in Names)
				yield return definiton.GetContext (name);
		}
	}

	public class AnonymousMatchContextReference : ContextReference
	{
		public SyntaxContext Context { get; private set; }

		internal AnonymousMatchContextReference (SyntaxContext context)
		{
			Context = context;
		}

		public override IEnumerable<SyntaxContext> GetContexts (SyntaxHighlightingDefinition definiton)
		{
			yield return Context;
		}
	}
}