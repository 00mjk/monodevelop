//
// IOImpl.cs
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
using System.IO;
using Mono.TextEditor.Utils;
using System.Text;
using Jurassic.Library;
using Jurassic;

namespace TypeScriptBinding.Hosting
{
	class ResolvedFileImpl : ObjectInstance, IResolvedFile
	{
		public ResolvedFileImpl (ScriptEngine engine, string path) : base (engine)
		{
			this.path = path;
			this.PopulateFunctions ();
		}
		
		#region IResolvedFile implementation
		FileInformation fileInformation;
		public FileInformation FileInformation {
			get {
				if (fileInformation == null)
					fileInformation = FileInformation.Read (Engine, path, Encoding.UTF8.CodePage);
				return fileInformation;
			}
		}
		public string path {
			get;
			private set;
		}
		#endregion
	}

}

