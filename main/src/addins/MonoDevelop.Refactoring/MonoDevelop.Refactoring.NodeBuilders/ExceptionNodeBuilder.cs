﻿//
// ExceptionNodeBuilder.cs
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
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using MonoDevelop.CodeIssues;

namespace MonoDevelop.Refactoring.NodeBuilders
{
	class ExceptionNodeBuilder : TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof (Exception); }
		}

		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return "Exception";
		}

		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, NodeInfo nodeInfo)
		{
			var e = (Exception)dataObject;
			nodeInfo.Label = string.Format (GettextCatalog.GetString ("Error: {0}"), e.Message);
			nodeInfo.Icon = Context.GetIcon ("md-error");
		}
	}
}

