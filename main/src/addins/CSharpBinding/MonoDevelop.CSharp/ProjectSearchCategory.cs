// 
// ProjectSearchCategory.cs
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
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using System.Collections.Generic;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Core.Text;
using Gtk;
using System.Linq;
using ICSharpCode.NRefactory6.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Collections.Concurrent;
using MonoDevelop.Components.MainToolbar;

namespace MonoDevelop.CSharp
{
	class ProjectSearchCategory : SearchCategory
	{
		internal static void Init ()
		{
			MonoDevelopWorkspace.LoadingFinished += async delegate {
				await UpdateSymbolInfos ();
			};
			if (IdeApp.IsInitialized) {
				IdeApp.Workspace.LastWorkspaceItemClosed += async delegate {
					await DisposeSymbolInfoTask ();
				};
			}
		}

		public ProjectSearchCategory () : base (GettextCatalog.GetString ("Solution"))
		{
			sortOrder = FirstCategory;
		}

		public override void Initialize (Components.PopoverWindow popupWindow)
		{
			lastResult = new WorkerResult ();
		}

		internal static Task<SymbolCache> SymbolInfoTask;

		static TimerCounter getTypesTimer = InstrumentationService.CreateTimerCounter ("Time to get all types", "NavigateToDialog");

		static CancellationTokenSource symbolInfoTokenSrc = new CancellationTokenSource();
		public static async Task UpdateSymbolInfos ()
		{
			await DisposeSymbolInfoTask ();
			CancellationToken token = symbolInfoTokenSrc.Token;
			SymbolInfoTask = Task.Run (delegate {
				return GetSymbolInfos (token);
			}, token);
		}

		static async Task DisposeSymbolInfoTask ()
		{
			symbolInfoTokenSrc.Cancel ();
			if (SymbolInfoTask != null) {
				try {
					var old = await SymbolInfoTask;
					if (old != null)
						old.Dispose ();
				} catch (OperationCanceledException) {
					// Ignore
				} catch (Exception ex) {
					LoggingService.LogError ("UpdateSymbolInfos failed", ex);
				}
			}
			symbolInfoTokenSrc = new CancellationTokenSource();
			lastResult = new WorkerResult ();
			SymbolInfoTask = null;
		}

		internal class SymbolCache : IDisposable
		{
			public static readonly SymbolCache Empty = new SymbolCache ();

			List<Microsoft.CodeAnalysis.Workspace> workspaces = new List<Microsoft.CodeAnalysis.Workspace> ();
			ConcurrentDictionary<Microsoft.CodeAnalysis.DocumentId, List<DeclaredSymbolInfo>> documentInfos = new ConcurrentDictionary<Microsoft.CodeAnalysis.DocumentId, List<DeclaredSymbolInfo>> ();

			public void AddWorkspace (Microsoft.CodeAnalysis.Workspace ws, CancellationToken token)
			{
				workspaces.Add (ws);
				ws.WorkspaceChanged += Ws_WorkspaceChanged;

				foreach (var p in ws.CurrentSolution.Projects) {
					if (p.FilePath.EndsWith ("csproj", StringComparison.Ordinal))
						SearchAsync (documentInfos, p, token);
				}
			}

			public IReadOnlyList<DeclaredSymbolInfo> GetAllTypes(string tag, CancellationToken token)
			{
				int count = 0;
				foreach (var infos in documentInfos.Values)
					count += infos.Count;

				var result = new List<DeclaredSymbolInfo>(count);
				foreach (var infos in documentInfos.Values) {
					foreach (var type in infos) {
						if (token.IsCancellationRequested)
							return new DeclaredSymbolInfo [0];
						
						var kind = type.Kind;
						if (kind == DeclaredSymbolInfoKind.Constructor ||
							kind == DeclaredSymbolInfoKind.Module ||
							kind == DeclaredSymbolInfoKind.Indexer)
							continue;

						if (tag != null) {
							if ((tag == "type" || tag == "t") && kind != DeclaredSymbolInfoKind.Class && kind != DeclaredSymbolInfoKind.Struct && kind != DeclaredSymbolInfoKind.Interface && kind != DeclaredSymbolInfoKind.Enum && kind != DeclaredSymbolInfoKind.Delegate)
								continue;

							if (tag == "class" && kind != DeclaredSymbolInfoKind.Class)
								continue;
							if (tag == "struct" && kind != DeclaredSymbolInfoKind.Struct)
								continue;
							if (tag == "interface" && kind != DeclaredSymbolInfoKind.Interface)
								continue;
							if (tag == "enum" && kind != DeclaredSymbolInfoKind.Enum)
								continue;
							if (tag == "delegate" && kind != DeclaredSymbolInfoKind.Delegate)
								continue;

							if ((tag == "member" || tag == "m") && kind != DeclaredSymbolInfoKind.Method && kind != DeclaredSymbolInfoKind.Property && kind != DeclaredSymbolInfoKind.Field && kind != DeclaredSymbolInfoKind.Event)
								continue;
							if (tag == "method" && kind != DeclaredSymbolInfoKind.Method)
								continue;
							if (tag == "property" && kind != DeclaredSymbolInfoKind.Property)
								continue;
							if (tag == "field" && kind != DeclaredSymbolInfoKind.Field)
								continue;
							if (tag == "event" && kind != DeclaredSymbolInfoKind.Event)
								continue;
						}
						result.Add (type);
					}
				}
				return result;
			}

			static async void SearchAsync (ConcurrentDictionary<Microsoft.CodeAnalysis.DocumentId, List<DeclaredSymbolInfo>> result, Microsoft.CodeAnalysis.Project project, CancellationToken cancellationToken)
			{
				if (project == null)
					throw new ArgumentNullException (nameof (project));
				try {
					foreach (var document in project.Documents) {
						cancellationToken.ThrowIfCancellationRequested ();
						await UpdateDocument (result, document, cancellationToken);
					}
				} catch (AggregateException ae) {
					ae.Flatten ().Handle (ex => ex is OperationCanceledException);
				} catch (OperationCanceledException) {
				}
			}

			static async Task UpdateDocument (ConcurrentDictionary<DocumentId, List<DeclaredSymbolInfo>> result, Microsoft.CodeAnalysis.Document document, CancellationToken cancellationToken)
			{
				var root = await document.GetSyntaxRootAsync (cancellationToken).ConfigureAwait (false);
				var infos = new List<DeclaredSymbolInfo> ();
				foreach (var current in root.DescendantNodesAndSelf (CSharpSyntaxFactsService.DescentIntoSymbolForDeclarationSearch)) {
					cancellationToken.ThrowIfCancellationRequested ();
					DeclaredSymbolInfo declaredSymbolInfo;
					if (current.TryGetDeclaredSymbolInfo (out declaredSymbolInfo)) {
						declaredSymbolInfo.DocumentId = document.Id;
						infos.Add (declaredSymbolInfo);
					}
				}
				RemoveDocument (result, document.Id);
				result.TryAdd (document.Id, infos);
			}

			static void RemoveDocument (ConcurrentDictionary<DocumentId, List<DeclaredSymbolInfo>> result, Microsoft.CodeAnalysis.DocumentId documentId)
			{
				if (result.ContainsKey (documentId)) {
					List<DeclaredSymbolInfo> val;
					result.TryRemove (documentId, out val);
				}
			}

			public void Dispose ()
			{
				if (workspaces == null)
					return;
				foreach (var ws in workspaces)
					ws.WorkspaceChanged -= Ws_WorkspaceChanged;
				workspaces = null;
				documentInfos = null;
			}
			Dictionary<DocumentId, CancellationTokenSource> documentChangedCts = new Dictionary<DocumentId, CancellationTokenSource> ();
			async void Ws_WorkspaceChanged (object sender, WorkspaceChangeEventArgs e)
			{
				var ws = (Microsoft.CodeAnalysis.Workspace)sender;
				var currentSolution = ws.CurrentSolution;
				if (currentSolution == null)
					return;
				try {
					switch (e.Kind) {
					case WorkspaceChangeKind.ProjectAdded:
						var project1 = currentSolution.GetProject (e.ProjectId);
						if (project1 != null)
							SearchAsync (documentInfos, project1, default (CancellationToken));
						break;
					case WorkspaceChangeKind.ProjectRemoved:
						var project = currentSolution.GetProject (e.ProjectId);
						if (project != null) {
							foreach (var docId in project.DocumentIds)
								RemoveDocument (documentInfos, docId);
						}
						break;
					case WorkspaceChangeKind.DocumentAdded:
						var document = currentSolution.GetDocument (e.DocumentId);
						if (document != null)
							await UpdateDocument (documentInfos, document, default (CancellationToken));
						break;
					case WorkspaceChangeKind.DocumentRemoved:
						RemoveDocument (documentInfos, e.DocumentId);
						break;
					case WorkspaceChangeKind.DocumentChanged:
						var doc = currentSolution.GetDocument (e.DocumentId);
						if (doc != null) {
							CancellationTokenSource tcs;
							lock(documentChangedCts) {
								CancellationTokenSource oldTcs;
								if (documentChangedCts.TryGetValue (e.DocumentId, out oldTcs)) {
									oldTcs.Cancel ();
								}
								tcs = new CancellationTokenSource ();
								documentChangedCts [e.DocumentId] = tcs;
							}
							try {
								//Delaying parsing of new content for 1 second shouldn't be noticable by user
								//since he would have to edit file and instantlly go to search for newly written member...
								await Task.Delay (1000, tcs.Token).ConfigureAwait (false);
								await Task.Run (delegate {
									return UpdateDocument (documentInfos, doc, tcs.Token);
								}, tcs.Token).ConfigureAwait (false);
							} finally {
								lock (documentChangedCts) {
									//cts might be replaced by newer call cts
									CancellationTokenSource existingCts;
									if (documentChangedCts.TryGetValue (e.DocumentId, out existingCts) && tcs == existingCts)
										documentChangedCts.Remove (e.DocumentId);
								}
							}
						}
						break;
					}
				} catch (AggregateException ae) {
					ae.Flatten ().Handle (ex => ex is OperationCanceledException);
				} catch (OperationCanceledException) {
				} catch (Exception ex) {
					LoggingService.LogError ("Error while updating navigation symbol cache.", ex);
				}
			}
		}

		static SymbolCache GetSymbolInfos (CancellationToken token)
		{
			getTypesTimer.BeginTiming ();
			try {
				var result = new SymbolCache ();
				foreach (var workspace in TypeSystemService.AllWorkspaces) {
					result.AddWorkspace (workspace, token);
				}
				return result;
			} catch (AggregateException ae) {
				ae.Flatten ().Handle (ex => ex is OperationCanceledException);
				return SymbolCache.Empty;
			} catch (OperationCanceledException) {
				return SymbolCache.Empty;
			} finally {
				getTypesTimer.EndTiming ();
			}
		}


		static WorkerResult lastResult;
		static readonly string[] typeTags = new [] { "type", "t", "class", "struct", "interface", "enum", "delegate" };
		static readonly string[] memberTags = new [] { "member", "m", "method", "property", "field", "event" };
		static readonly string[] tags = typeTags.Concat(memberTags).ToArray();

		public override string[] Tags {
			get {
				return tags;
			}
		}

		public override bool IsValidTag (string tag)
		{
			return typeTags.Any (t => t == tag) || memberTags.Any (t => t == tag);
		}

		public override Task GetResults (ISearchResultCallback searchResultCallback, SearchPopupSearchPattern searchPattern, CancellationToken token)
		{
			return Task.Run (async delegate {
				if (searchPattern.Tag != null && !(typeTags.Contains (searchPattern.Tag) || memberTags.Contains (searchPattern.Tag)) || searchPattern.HasLineNumber)
					return;
				try {
					if (SymbolInfoTask == null)
						SymbolInfoTask = Task.FromResult (default(SymbolCache)).ContinueWith(t => GetSymbolInfos (token));
					var cache = await SymbolInfoTask.ConfigureAwait (false);
					var allTypes = cache.GetAllTypes (searchPattern.Tag, token);
					if (token.IsCancellationRequested)
						return;
					string toMatch = searchPattern.Pattern;
					var newResult = new WorkerResult ();
					newResult.pattern = searchPattern.Pattern;
					newResult.Tag = searchPattern.Tag;
					newResult.matcher = StringMatcher.GetMatcher (toMatch, false);
					newResult.FullSearch = toMatch.IndexOf ('.') > 0;
					var oldLastResult = lastResult;
					if (newResult.FullSearch && oldLastResult != null && !oldLastResult.FullSearch)
						oldLastResult = new WorkerResult ();
//					var now = DateTime.Now;

					AllResults (searchResultCallback, oldLastResult, newResult, allTypes, token);
					//newResult.results.SortUpToN (new DataItemComparer (token), resultsCount);
					lastResult = newResult;
					//					Console.WriteLine ((now - DateTime.Now).TotalMilliseconds);
				} catch {
					token.ThrowIfCancellationRequested ();
					throw;
				}
			}, token);
		}

		void AllResults (ISearchResultCallback searchResultCallback, WorkerResult lastResult, WorkerResult newResult, IReadOnlyList<DeclaredSymbolInfo> completeTypeList, CancellationToken token)
		{
			// Search Types
			newResult.filteredSymbols = new List<DeclaredSymbolInfo> ();
			bool startsWithLastFilter = lastResult.pattern != null && newResult.pattern.StartsWith (lastResult.pattern, StringComparison.Ordinal) && lastResult.filteredSymbols != null;
			var allTypes = startsWithLastFilter ? lastResult.filteredSymbols : completeTypeList;
			foreach (var type in allTypes) {
				if (token.IsCancellationRequested) {
					newResult.filteredSymbols = null;
					return;
				}
				SearchResult curResult = newResult.CheckType (type);
				if (curResult != null) {
					newResult.filteredSymbols.Add (type);
					searchResultCallback.ReportResult (curResult);
				}
			}
		}

		class WorkerResult
		{
			public string Tag {
				get;
				set;
			}

			public List<DeclaredSymbolInfo> filteredSymbols;

			string pattern2;
			char firstChar;
			char[] firstChars;

			public string pattern {
				get {
					return pattern2;
				}
				set {
					pattern2 = value;
					if (pattern2.Length == 1) {
						firstChar = pattern2 [0];
						firstChars = new [] { char.ToUpper (firstChar), char.ToLower (firstChar) };
					} else {
						firstChars = null;
					}
				}
			}

			public bool FullSearch;
			public StringMatcher matcher;

			public WorkerResult ()
			{
			}

			internal SearchResult CheckType (DeclaredSymbolInfo symbol)
			{
				int rank;
				var name = symbol.Name;
				if (MatchName(name, out rank)) {
//					if (type.ContainerDisplayName != null)
//						rank--;
					return new DeclaredSymbolInfoResult (pattern, symbol.Name, rank, symbol, false);
				}
				if (!FullSearch)
					return null;
				name = symbol.FullyQualifiedContainerName;
				if (MatchName(name, out rank)) {
//					if (type.ContainingType != null)
//						rank--;
					return new DeclaredSymbolInfoResult (pattern, name, rank, symbol, true);
				}
				return null;
			}

			Dictionary<string, MatchResult> savedMatches = new Dictionary<string, MatchResult> (StringComparer.Ordinal);

			bool MatchName (string name, out int matchRank)
			{
				if (name == null) {
					matchRank = -1;
					return false;
				}

				MatchResult savedMatch;
				if (!savedMatches.TryGetValue (name, out savedMatch)) {
					bool doesMatch;
					if (firstChars != null) {
						int idx = name.IndexOfAny (firstChars);
						doesMatch = idx >= 0;
						if (doesMatch) {
							matchRank = int.MaxValue - (name.Length - 1) * 10 - idx;
							if (name [idx] != firstChar)
								matchRank /= 2;
							savedMatches [name] = savedMatch = new MatchResult (true, matchRank);
							return true;
						}
						matchRank = -1;
						savedMatches [name] = savedMatch = new MatchResult (false, -1);
						return false;
					}
					doesMatch = matcher.CalcMatchRank (name, out matchRank);
					savedMatches [name] = savedMatch = new MatchResult (doesMatch, matchRank);
				}
				
				matchRank = savedMatch.Rank;
				return savedMatch.Match;
			}
		}
	}
}
