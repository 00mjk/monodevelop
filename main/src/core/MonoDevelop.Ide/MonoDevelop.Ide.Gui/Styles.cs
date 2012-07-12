// 
// Styles.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
using MonoDevelop.Components;

namespace MonoDevelop.Ide.Gui
{
	static class Styles
	{
		public static readonly Cairo.Color BaseBackgroundColor = new Cairo.Color (1, 1, 1);
		public static readonly Cairo.Color BaseForegroundColor = new Cairo.Color (0, 0, 0);

		public static readonly Cairo.Color TabBarBackgroundColor = new Cairo.Color (248d / 255d, 248d / 255d, 248d / 255d);
		public static readonly Cairo.Color BreadcrumbBackgroundColor = new Cairo.Color (77d / 255d, 77d / 255d, 77d / 255d);
		public static readonly Cairo.Color WidgetBorderColor = CairoExtensions.ParseColor ("8c8c8c");


		public static readonly Cairo.Color TabBarGradientStartColor = TabBarBackgroundColor;
		public static readonly Cairo.Color TabBarGradientMidColor = Shift (TabBarBackgroundColor, 0.875);
		public static readonly Cairo.Color TabBarGradientEndColor = Shift (TabBarBackgroundColor, 0.738);

		public static readonly Cairo.Color BreadcrumbGradientStartColor = Shift (BreadcrumbBackgroundColor, 1.299);
		public static readonly Cairo.Color BreadcrumbGradientEndColor = Shift (BreadcrumbBackgroundColor, 0.662);
		public static readonly Cairo.Color BreadcrumbBorderColor = Shift (BreadcrumbBackgroundColor, 0.714);
		public static readonly Cairo.Color BreadcrumbInnerBorderColor = WithAlpha (BaseBackgroundColor, 0.1d);
		public static readonly Gdk.Color BreadcrumbTextColor = Shift (BaseBackgroundColor, 0.8).ToGdkColor ();
		public static readonly Cairo.Color BreadcrumbButtonBorderColor = Shift (BaseBackgroundColor, 0.8);
		public static readonly Cairo.Color BreadcrumbButtonFillColor = WithAlpha (BaseBackgroundColor, 0.1d);

		public static readonly Cairo.Color DockTabBarGradientTop = new Cairo.Color (248d / 255d, 248d / 255d, 248d / 255d);
		public static readonly Cairo.Color DockTabBarGradientStart = new Cairo.Color (242d / 255d, 242d / 255d, 242d / 255d);
		public static readonly Cairo.Color DockTabBarGradientEnd = new Cairo.Color (230d / 255d, 230d / 255d, 230d / 255d);
		public static readonly Cairo.Color DockTabBarShadowGradientStart = new Cairo.Color (154d / 255d, 154d / 255d, 154d / 255d, 1);
		public static readonly Cairo.Color DockTabBarShadowGradientEnd = new Cairo.Color (154d / 255d, 154d / 255d, 154d / 255d, 0);

		public static readonly Gdk.Color PadBackground = new Gdk.Color (240, 240, 240);
		public static readonly Gdk.Color PadLabelColor = new Gdk.Color (92, 99, 102);
		public static readonly Gdk.Color DockFrameBackground = new Gdk.Color (157, 162, 166);

		public static readonly Gdk.Color BrowserPadBackground = new Gdk.Color (219, 224, 231);

		public static readonly Cairo.Color StatusBarBorderColor = Styles.WidgetBorderColor;
		public static readonly Cairo.Color StatusBarFill1Color = CairoExtensions.ParseColor ("eff5f7");
		public static readonly Cairo.Color StatusBarFill2Color = CairoExtensions.ParseColor ("d0d9db");
		public static readonly Cairo.Color StatusBarInnerColor = CairoExtensions.ParseColor ("c4cdcf", 0.5);
		public static readonly Cairo.Color StatusBarTextColor = CairoExtensions.ParseColor ("3a4029");

		static Cairo.Color Shift (Cairo.Color color, double factor)
		{
			return new Cairo.Color (color.R * factor, color.G * factor, color.B * factor, color.A);
		}

		static Cairo.Color WithAlpha (Cairo.Color c, double alpha)
		{
			return new Cairo.Color (c.R, c.G, c.B, alpha);
		}

		static Cairo.Color Blend (Cairo.Color color, Cairo.Color targetColor, double factor)
		{
			return new Cairo.Color (color.R + ((color.R - targetColor.R) * factor),
			                        color.G + ((color.G - targetColor.G) * factor),
			                        color.B + ((color.B - targetColor.B) * factor),
			                        color.A
			                        );
		}

		static Cairo.Color MidColor (double factor)
		{
			return Blend (BaseBackgroundColor, BaseForegroundColor, factor);
		}

		static Cairo.Color ReduceLight (Cairo.Color color, double factor)
		{
			var c = new HslColor (color);
			c.L *= factor;
			return c;
		}

		static Cairo.Color IncreaseLight (Cairo.Color color, double factor)
		{
			var c = new HslColor (color);
			c.L += (1 - c.L) * factor;
			return c;
		}
	}
}

