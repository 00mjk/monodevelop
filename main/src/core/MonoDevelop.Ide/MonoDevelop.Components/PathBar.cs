// 
// PathBar.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
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
using System.Linq;

using Gtk;
using Gdk;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.Components
{
	public enum EntryPosition
	{
		Left,
		Right
	}
	
	public class PathEntry 
	{
		Gdk.Pixbuf darkIcon;

		public Gdk.Pixbuf Icon {
			get;
			private set;
		}
		
		public string Markup {
			get;
			private set;
		}
		
		public object Tag {
			get;
			set;
		}
		
		public bool IsPathEnd {
			get;
			set;
		}
		
		public EntryPosition Position {
			get;
			set;
		}
		
		public PathEntry (Gdk.Pixbuf icon, string markup)
		{
			this.Icon = icon;
			this.Markup = markup;
		}
		
		public PathEntry (string markup)
		{
			this.Markup = markup;
		}
		
		public override bool Equals (object obj)
		{
			if (obj == null)
				return false;
			if (ReferenceEquals (this, obj))
				return true;
			if (obj.GetType () != typeof(PathEntry))
				return false;
			MonoDevelop.Components.PathEntry other = (MonoDevelop.Components.PathEntry)obj;
			return Icon == other.Icon && Markup == other.Markup;
		}

		public override int GetHashCode ()
		{
			unchecked {
				return (Icon != null ? Icon.GetHashCode () : 0) ^ (Markup != null ? Markup.GetHashCode () : 0);
			}
		}

		internal Gdk.Pixbuf DarkIcon {
			get {
				if (darkIcon == null && Icon != null) {
					darkIcon = ImageService.MakeGrayscale (Icon);
					darkIcon = ImageService.MakeInverted (darkIcon);
				}
				return darkIcon;
			}
		}
	}
	
	public class PathBar : Gtk.DrawingArea
	{
		PathEntry[] leftPath  = new PathEntry[0];
		PathEntry[] rightPath = new PathEntry[0];
		Pango.Layout layout;
		Pango.AttrList boldAtts = new Pango.AttrList ();
		
		//HACK: a surrogate widget object to pass to style calls instead of "this" when using "button" hint.
		// This avoids GTK-Criticals in themes which try to cast the widget object to a button.
		Gtk.Button styleButton = new Gtk.Button ();
		
		int[] leftWidths, rightWidths;
		int height;
		int textHeight;

		bool pressed, hovering, menuVisible;
		int hoverIndex = -1;
		int activeIndex = -1;

		const int leftPadding = 6;
		const int rightPadding = 6;
		const int topPadding = 2;
		const int bottomPadding = 4;
		const int iconSpacing = 4;
		const int padding = 3;
		const int buttonPadding = 2;
		const int arrowLeftPadding = 4;
		const int arrowRightPadding = 4;
		const int arrowSize = 10;
		const int spacing = arrowLeftPadding + arrowRightPadding + arrowSize;
		
		Func<int, Widget> createMenuForItem;
		
		public PathBar (Func<int, Widget> createMenuForItem)
		{
			this.Events =  EventMask.ExposureMask | 
				           EventMask.EnterNotifyMask |
				           EventMask.LeaveNotifyMask |
				           EventMask.ButtonPressMask | 
				           EventMask.ButtonReleaseMask | 
				           EventMask.KeyPressMask | 
					       EventMask.PointerMotionMask;
			boldAtts.Insert (new Pango.AttrWeight (Pango.Weight.Bold));
			this.createMenuForItem = createMenuForItem;
			EnsureLayout ();
		}
		
		public new PathEntry[] Path { get; private set; }
		public int ActiveIndex { get { return activeIndex; } }
		
		public void SetPath (PathEntry[] path)
		{
			if (ArrSame (this.leftPath, path))
				return;
			this.Path = path ?? new PathEntry[0];
			this.leftPath = Path.Where (p => p.Position == EntryPosition.Left).ToArray ();
			this.rightPath = Path.Where (p => p.Position == EntryPosition.Right).ToArray ();
			
			activeIndex = -1;
			leftWidths = rightWidths = null;
			EnsureWidths ();
			QueueDraw ();
		}
		
		bool ArrSame (PathEntry[] a, PathEntry[] b)
		{
			if ((a == null || b == null) && a != b)
				return false;
			if (a.Length != b.Length)
				return false;
			for (int i = 0; i < a.Length; i++)
				if (!a[i].Equals(b[i]))
					return false;
			return true;
		}
		
		public void SetActive (int index)
		{
			if (index >= leftPath.Length)
				throw new IndexOutOfRangeException ();
			
			if (activeIndex != index) {
				activeIndex = index;
				leftWidths = rightWidths = null;
				QueueResize ();
			}
		}
		
		protected override void OnSizeRequested (ref Requisition requisition)
		{
			EnsureWidths ();
			requisition.Width = Math.Max (WidthRequest, 0);
			requisition.Height = height + topPadding + bottomPadding;
		}
		
		protected override bool OnExposeEvent (EventExpose evnt)
		{
			var ctx = Gdk.CairoHelper.Create (GdkWindow);

			ctx.Rectangle (0, 0, Allocation.Width, Allocation.Height);
			Cairo.LinearGradient g = new Cairo.LinearGradient (0, 0, 0, Allocation.Height);
			g.AddColorStop (0, Styles.BreadcrumbBackgroundColor);
			g.AddColorStop (1, Styles.BreadcrumbGradientEndColor);
			ctx.Pattern = g;
			ctx.Fill ();

			if (leftWidths == null || rightWidths == null)
				return true;

			int textTopPadding = topPadding + (height - textHeight) / 2;
			int xpos = leftPadding, ypos = topPadding;

			for (int i = 0; i < leftPath.Length; i++) {
				bool last = i == leftPath.Length - 1;
				
				int x = xpos;
				xpos += leftWidths [i];

				if (hoverIndex >= 0 && hoverIndex < Path.Length && leftPath [i] == Path [hoverIndex] && (menuVisible || pressed || hovering))
					DrawButtonBorder (ctx, x - padding, leftWidths [i] + padding + padding);

				int textOffset = 0;
				if (leftPath [i].DarkIcon != null) {
					int iy = (height - leftPath [i].DarkIcon.Height) / 2 + topPadding;
					GdkWindow.DrawPixbuf (Style.BaseGC (State), leftPath [i].DarkIcon, 0, 0, x, iy, -1, -1, RgbDither.None, 0, 0);
					textOffset += leftPath [i].DarkIcon.Width + iconSpacing;
				}
				
				layout.Attributes = (i == activeIndex) ? boldAtts : null;
				layout.SetMarkup (leftPath [i].Markup);

				// Text shadow
				ctx.Color = new Cairo.Color (0,0,0);
				ctx.MoveTo (x + textOffset, textTopPadding + 1);
				PangoCairoHelper.ShowLayout (ctx, layout);

				// Text
				ctx.Color = Styles.BreadcrumbTextColor.ToCairoColor ();
				ctx.MoveTo (x + textOffset, textTopPadding);
				PangoCairoHelper.ShowLayout (ctx, layout);

				if (!last) {
					xpos += arrowLeftPadding;
					if (leftPath [i].IsPathEnd) {
						Style.PaintVline (Style, GdkWindow, State, evnt.Area, this, "", ypos, ypos + height, xpos - arrowSize / 2);
					} else {
						int arrowH = Math.Min (height, arrowSize);
						int arrowY = ypos + (height - arrowH) / 2;
						Style.PaintArrow (Style, GdkWindow, State, ShadowType.None, evnt.Area, this, "", ArrowType.Right,
						                  true, xpos, arrowY, arrowSize, arrowH);
					}
					xpos += arrowSize + arrowRightPadding;
				}
			}
			
			int xposRight = Allocation.Width - rightPadding;
			for (int i = 0; i < rightPath.Length; i++) {
//				bool last = i == rightPath.Length - 1;
				
				xposRight -= rightWidths [i];
				xposRight -= arrowSize;
					
				int x = xposRight;
				
				if (hoverIndex >= 0 && hoverIndex < Path.Length && rightPath [i] == Path [hoverIndex] && (menuVisible || pressed || hovering))
					DrawButtonBorder (ctx, x - padding, rightWidths [i] + padding + padding);
				
				int textOffset = 0;
				if (rightPath [i].DarkIcon != null) {
					GdkWindow.DrawPixbuf (Style.BaseGC (State), rightPath [i].DarkIcon, 0, 0, x, ypos, -1, -1, RgbDither.None, 0, 0);
					textOffset += rightPath [i].DarkIcon.Width + padding;
				}
				
				layout.Attributes = (i == activeIndex) ? boldAtts : null;
				layout.SetMarkup (rightPath [i].Markup);

				// Text shadow
				ctx.Color = new Cairo.Color (0,0,0);
				ctx.MoveTo (x + textOffset, textTopPadding + 1);
				PangoCairoHelper.ShowLayout (ctx, layout);

				// Text
				ctx.Color = Styles.BreadcrumbTextColor.ToCairoColor ();
				ctx.MoveTo (x + textOffset, textTopPadding);
				PangoCairoHelper.ShowLayout (ctx, layout);
			}

			((IDisposable)ctx).Dispose ();

			return true;
		}

		void DrawButtonBorder (Cairo.Context ctx, double x, double width)
		{
			x -= buttonPadding;
			width += buttonPadding;
			double y = topPadding - buttonPadding;
			double height = Allocation.Height - topPadding - bottomPadding + buttonPadding * 2;

			ctx.Rectangle (x, y, width, height);
			ctx.Color = Styles.BreadcrumbButtonFillColor;
			ctx.Fill ();

			ctx.Rectangle (x + 0.5, y + 0.5, width - 1, height - 1);
			ctx.Color = Styles.BreadcrumbButtonBorderColor;
			ctx.LineWidth = 1;
			ctx.Stroke ();
		}
		
		protected override bool OnButtonPressEvent (EventButton evnt)
		{
			if (hovering) {
				pressed = true;
				QueueDraw ();
			}
			return true;
		}
		
		protected override bool OnButtonReleaseEvent (EventButton evnt)
		{
			pressed = false;
			if (hovering) {
				QueueDraw ();
				ShowMenu ();
			}
			return true;
		}
		
		void ShowMenu ()
		{
			if (hoverIndex < 0)
				return;
			
			Gtk.Widget widget = createMenuForItem (hoverIndex);
			if (widget == null)
				return;
			widget.Hidden += delegate {
				
				menuVisible = false;
				QueueDraw ();
				
				//FIXME: for some reason the menu's children don't get activated if we destroy 
				//directly here, so use a timeout to delay it
				GLib.Timeout.Add (100, delegate {
					widget.Destroy ();
					return false;
				});
			};
			menuVisible = true;
			if (widget is Menu) {
				((Menu)widget).Popup (null, null, PositionFunc, 0, Gtk.Global.CurrentEventTime);
			} else {
				PositionWidget (widget);
				widget.ShowAll ();
			}
				
		}
		
		public int GetHoverXPosition (out int w)
		{
			if (Path[hoverIndex].Position == EntryPosition.Left) {
				int idx = leftPath.TakeWhile (p => p != Path[hoverIndex]).Count ();
				
				if (idx >= 0) {
					w = leftWidths[idx];
					return leftWidths.Take (idx).Sum () + idx * spacing;
				}
			} else {
				int idx = rightPath.TakeWhile (p => p != Path[hoverIndex]).Count ();
				if (idx >= 0) {
					w = rightWidths[idx];
					return Allocation.Width - padding - rightWidths[idx] - spacing;
				}
			}
			w = Allocation.Width;
			return 0;
		}

		void PositionWidget (Gtk.Widget widget)
		{
			if (!(widget is Gtk.Window))
				return;
			int ox, oy;
			ParentWindow.GetOrigin (out ox, out oy);
			int w;
			int itemXPosition = GetHoverXPosition (out w);
			int dx = ox + this.Allocation.X + itemXPosition;
			int dy = oy + this.Allocation.Bottom;
			
			var req = widget.SizeRequest ();
			
			Gdk.Rectangle geometry = DesktopService.GetUsableMonitorGeometry (Screen, Screen.GetMonitorAtPoint (dx, dy));
			int width = System.Math.Max (req.Width, w);
			if (width >= geometry.Width - spacing * 2) {
				width = geometry.Width - spacing * 2;
				dx = geometry.Left + spacing;
			}
			widget.WidthRequest = width;
			if (dy + req.Height > geometry.Bottom)
				dy = oy + this.Allocation.Y - req.Height;
			if (dx + width > geometry.Right)
				dx = geometry.Right - width;
			(widget as Gtk.Window).Move (dx, dy);
			(widget as Gtk.Window).Resize (width, req.Height);
			widget.GrabFocus ();
		}
		
		
		
		void PositionFunc (Menu mn, out int x, out int y, out bool push_in)
		{
			this.GdkWindow.GetOrigin (out x, out y);
			int w;
			var rect = this.Allocation;
			y += rect.Height;
			x += GetHoverXPosition (out w);
			//if the menu would be off the bottom of the screen, "drop" it upwards
			if (y + mn.Requisition.Height > this.Screen.Height) {
				y -= mn.Requisition.Height;
				y -= rect.Height;
			}
			
			//let GTK reposition the button if it still doesn't fit on the screen
			push_in = true;
		}
		
		protected override bool OnMotionNotifyEvent (EventMotion evnt)
		{
			SetHover (GetItemAt ((int)evnt.X, (int)evnt.Y));
			return true;
		}
		
		protected override bool OnLeaveNotifyEvent (EventCrossing evnt)
		{
			pressed = false;
			SetHover (-1);
			return true;
		}
		
		protected override bool OnEnterNotifyEvent (EventCrossing evnt)
		{
			SetHover (GetItemAt ((int)evnt.X, (int)evnt.Y));
			return true;
		}
		
		void SetHover (int i)
		{
			bool oldHovering = hovering;
			hovering = i > -1;
			
			if (hoverIndex != i || oldHovering != hovering) {
				if (hovering)
					hoverIndex = i;
				QueueDraw ();
			}
		}
		
		public int IndexOf (PathEntry entry)
		{
			return Path.TakeWhile (p => p != entry).Count ();
		}

		int GetItemAt (int x, int y)
		{
			int xpos = padding, xposRight = Allocation.Width - padding;
			if (leftWidths == null || x < xpos || x > xposRight)
				return -1;
			
			//could do a binary search, but probably not worth it
			for (int i = 0; i < leftPath.Length; i++) {
				xpos += leftWidths[i] + spacing;
				if (x < xpos)
					return IndexOf (leftPath[i]);
			}
			
			for (int i = 0; i < rightPath.Length; i++) {
				xposRight -= rightWidths[i] - spacing;
				if (x > xposRight)
					return IndexOf (rightPath[i]);
			}
			
			return -1;
		}
		
		void EnsureLayout ()
		{
			if (layout != null)
				layout.Dispose ();
			layout = new Pango.Layout (PangoContext);
		}
		
		int[] CreateWidthArray (PathEntry[] path)
		{
			var result = new int[path.Length];
			int maxIconHeight = 0;

			for (int i = 0; i < path.Length; i++) {
				layout.Attributes = (i == activeIndex)? boldAtts : null;
				layout.SetMarkup (path[i].Markup);
				int w, h;
				layout.GetPixelSize (out w, out h);
				textHeight = Math.Max (h, textHeight);
				if (path[i].DarkIcon != null) {
					maxIconHeight = Math.Max (path[i].DarkIcon.Height, maxIconHeight);
					w += path[i].DarkIcon.Width + iconSpacing;
				}
				result[i] = w;
			}
			height = Math.Max (height, maxIconHeight);
			height = Math.Max (height, textHeight);
			return result;
		}

		void EnsureWidths ()
		{
			if (leftWidths != null) 
				return;
			
			layout.SetText ("#");
			int w;
			layout.GetPixelSize (out w, out this.height);
			textHeight = height;

			leftWidths = CreateWidthArray (leftPath);
			rightWidths = CreateWidthArray (rightPath);
		}
		
		protected override void OnStyleSet (Style previous)
		{
			base.OnStyleSet (previous);
			KillLayout ();
			EnsureLayout ();
		}
		
		void KillLayout ()
		{
			if (layout == null)
				return;
			layout.Dispose ();
			layout = null;
			boldAtts.Dispose ();
			
			leftWidths = rightWidths = null;
		}
		
		public override void Destroy ()
		{
			base.Destroy ();
			styleButton.Destroy ();
			KillLayout ();
			this.boldAtts.Dispose ();
		}
	}
}
