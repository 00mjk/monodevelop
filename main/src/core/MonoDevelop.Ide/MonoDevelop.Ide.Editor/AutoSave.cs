//
// AutoSave.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MonoDevelop.Core;
using Gtk;
using MonoDevelop.Core.Text;

namespace MonoDevelop.Ide.Editor
{
	/// <summary>
	/// This class handles the auto save mechanism for open files.
	/// It should only be used by editor implementations.
	/// </summary>
	static class AutoSave
	{
		//FIXME: is this path a good one? wouldn't it be better to put autosaves beside the files anyway?
		static string autoSavePath = UserProfile.Current.CacheDir.Combine ("AutoSave");
		static bool autoSaveEnabled;
		
		static AutoSave ()
		{
			try {
				if (!Directory.Exists (autoSavePath))
					Directory.CreateDirectory (autoSavePath);
			} catch (Exception e) {
				LoggingService.LogError ("Can't create auto save path:" + autoSavePath +". Auto save is disabled.", e);
				autoSaveEnabled = false;
				return;
			}
			autoSaveEnabled = true;
			StartAutoSaveThread ();
		}

		static string GetAutoSaveFileName (string fileName)
		{
			if (fileName == null)
				return null;
			string newFileName = Path.Combine (Path.GetDirectoryName (fileName), Path.GetFileNameWithoutExtension (fileName) + Path.GetExtension (fileName) + "~");
			newFileName = Path.Combine (autoSavePath, newFileName.Replace(',','_').Replace(" ","").Replace (":","").Replace (Path.DirectorySeparatorChar, '_').Replace (Path.AltDirectorySeparatorChar, '_'));
			return newFileName;
		}

		/// <summary>
		/// Returns true if an auto save exists for the given file name.
		/// </summary>
		public static bool AutoSaveExists (string fileName)
		{
			if (!autoSaveEnabled)
				return false;
			try {
				var autoSaveFilename = GetAutoSaveFileName (fileName);
				bool autoSaveExists = File.Exists (autoSaveFilename);
				if (autoSaveExists) {
					if (File.GetLastWriteTimeUtc (autoSaveFilename) < File.GetLastWriteTimeUtc (fileName)) {
						File.Delete (autoSaveFilename);
						return false;
					}
				}
				return autoSaveExists;
			} catch (Exception e) {
				LoggingService.LogError ("Error in auto save - disableing.", e);
				DisableAutoSave ();
				return false;
			}
		}

		static void CreateAutoSave (string fileName, ITextSource content)
		{
			if (!autoSaveEnabled)
				return;
			try {
				// Directory may have removed/unmounted. Therefore this operation is not guaranteed to work.
				string tmpFile = Path.GetTempFileName ();
				content.WriteTextTo (tmpFile);
				var autosaveFileName = GetAutoSaveFileName (fileName);
				if (File.Exists (autosaveFileName))
					File.Delete (autosaveFileName);
				File.Move (tmpFile, autosaveFileName);
				Counters.AutoSavedFiles++;
			} catch (Exception e) {
				LoggingService.LogError ("Error in auto save while creating: " + fileName +". Disableing auto save.", e);
				DisableAutoSave ();
			}
		}

		#region AutoSave
		class FileContent
		{
			public string FileName;
			public ITextSource Content;

			public FileContent (string fileName, ITextSource content)
			{
				this.FileName = fileName;
				this.Content = content;
			}
		}
		static readonly AutoResetEvent resetEvent = new AutoResetEvent (false);

		public static bool Running {
			get {
				return autoSaveThreadRunning;
			}
		}
		static bool autoSaveThreadRunning = false;
		static Thread autoSaveThread;
		static Queue<FileContent> queue = new Queue<FileContent> ();
		static object contentLock = new object ();

		static void StartAutoSaveThread ()
		{
			autoSaveThreadRunning = true;
			if (autoSaveThread == null) {
				autoSaveThread = new Thread (AutoSaveThread);
				autoSaveThread.Name = "Autosave";
				autoSaveThread.IsBackground = true;
				autoSaveThread.Start ();
			}
		}

		static void AutoSaveThread ()
		{
			while (autoSaveThreadRunning) {
				resetEvent.WaitOne ();
				while (queue.Count > 0) {
					var content = queue.Dequeue ();
					// Don't create an auto save for unsaved files.
					if (string.IsNullOrEmpty (content.FileName))
						continue;
					CreateAutoSave (content.FileName, content.Content);
				}
			}
		}

		/// <summary>
		/// Loads the content from an auto save file and removes the auto save file.
		/// </summary>
		public static ITextSource LoadAndRemoveAutoSave (string fileName)
		{
			string autoSaveFileName = GetAutoSaveFileName (fileName);
			var result = StringTextSource.ReadFrom (autoSaveFileName);
			AutoSave.RemoveAutoSaveFile (fileName);
			return result;
		}

		/// <summary>
		/// Loads the content from an auto save file.
		/// </summary>
		public static ITextSource LoadAutoSave (string fileName)
		{
			string autoSaveFileName = GetAutoSaveFileName (fileName);
			return StringTextSource.ReadFrom (autoSaveFileName);
		}

		/// <summary>
		/// Removes the auto save file.
		/// </summary>
		/// <param name="fileName">The file name for which the auto save file should be removed.</param>
		public static void RemoveAutoSaveFile (string fileName)
		{
			if (!autoSaveEnabled)
				return;
			if (AutoSaveExists (fileName)) {
				string autoSaveFileName = GetAutoSaveFileName (fileName);
				try {
					lock (contentLock) {
						File.Delete (autoSaveFileName);
					}
				} catch (Exception e) {
					LoggingService.LogError ("Can't delete auto save file: " + autoSaveFileName +". Disableing auto save.", e);
					DisableAutoSave ();
				}
			}
		}

		internal static void InformAutoSaveThread (ITextSource content, string fileName, bool isDirty)
		{
			if (content == null || !autoSaveEnabled)
				return;
			if (isDirty) {
				queue.Enqueue (new FileContent (fileName, content));
				resetEvent.Set ();
			} else {
				RemoveAutoSaveFile (fileName);
			}
		}

		static void DisableAutoSave ()
		{
			autoSaveThreadRunning = false;
			if (autoSaveThread != null) {
				resetEvent.Set ();
				autoSaveThread.Join ();
				autoSaveThread = null;
			}
			autoSaveEnabled = false;
		}
#endregion
	}
}
