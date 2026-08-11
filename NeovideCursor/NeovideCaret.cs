using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace NeovideCursor;

/// <summary>
/// The neovide-style animated caret: a filled quad whose four corners are each
/// driven by an independent damped spring, producing the signature rubber-band /
/// trailing effect. Faithful port of createNeovideCursor from neovide-cursor.js.
/// The element spans the whole viewport and draws using viewport coordinates
/// (the WPF equivalent of the full-window fixed-position canvas in the JS).
/// </summary>
internal class NeovideCaret : FrameworkElement
{
	// The four corner offsets around the cursor center (port of STANDARD_CORNERS).
	private static readonly Point[] CornerOffsets =
	{
		new Point(-0.5, -0.5), // top-left
		new Point(0.5, -0.5),  // top-right
		new Point(0.5, 0.5),   // bottom-right
		new Point(-0.5, 0.5),  // bottom-left
	};

	private readonly Corner[] corners;
	private Color color;
	private bool useShadow;
	private SolidColorBrush brush;

	/// <summary>Whether the caret has been placed at least once (its corner state is valid).</summary>
	public bool IsPlaced
	{
		get { return initialized; }
	}

	private Size cursorDimensions;
	private Point destination;
	private Point centerDestination;
	private bool initialized;
	private bool jumped;

	public NeovideCaret(Options options, Color themedTextColor)
	{
		color = ResolveColor(options.CursorColor, themedTextColor);
		useShadow = options.UseShadow;

		brush = new SolidColorBrush(color);
		brush.Freeze();

		corners = new Corner[CornerOffsets.Length];
		for (int i = 0; i < corners.Length; i++)
		{
			corners[i] = new Corner(
				CornerOffsets[i],
				options.AnimationLength,
				options.ShortAnimationLength,
				options.TrailSize);
		}

		cursorDimensions = new Size(options.CaretWidth, 18);

		if (useShadow)
		{
			Effect = new DropShadowEffect
			{
				Color = color,
				BlurRadius = options.ShadowBlur,
				ShadowDepth = 0,
				Direction = 0,
				Opacity = 1,
				RenderingBias = RenderingBias.Performance
			};
		}
	}

	/// <summary>
	/// Updates the cursor dimensions. Returns <c>true</c> if any dimension actually
	/// changed — the caller uses that to force a re-target (e.g. overwrite mode
	/// toggles the width while the position stays put).
	/// </summary>
	public bool UpdateCursorSize(double width, double height)
	{
		bool changed = false;
		if (width > 0 && width != cursorDimensions.Width)
		{
			cursorDimensions.Width = width;
			changed = true;
		}
		if (height > 0 && height != cursorDimensions.Height)
		{
			cursorDimensions.Height = height;
			changed = true;
		}
		return changed;
	}

	/// <summary>
	/// Re-applies configuration (color / glow / animation lengths / caret width) to a
	/// live caret without disturbing its on-screen position. Called when the user
	/// changes settings in Tools → Options while an editor view is open.
	/// </summary>
	public void ApplyOptions(Options options, Color themedTextColor)
	{
		color = ResolveColor(options.CursorColor, themedTextColor);
		useShadow = options.UseShadow;

		brush = new SolidColorBrush(color);
		brush.Freeze();

		// Keep each corner where it is on screen; only re-anchor its destination to the
		// new geometry so the next move animates from the visible position (no jump).
		Point[] positions = new Point[corners.Length];
		for (int i = 0; i < corners.Length; i++)
		{
			positions[i] = corners[i].CurrentPosition;
		}

		cursorDimensions.Width = options.CaretWidth;
		centerDestination = new Point(
			destination.X + cursorDimensions.Width / 2,
			destination.Y + cursorDimensions.Height / 2);

		for (int i = 0; i < corners.Length; i++)
		{
			corners[i] = new Corner(
				CornerOffsets[i],
				options.AnimationLength,
				options.ShortAnimationLength,
				options.TrailSize)
			{
				CurrentPosition = positions[i],
				PreviousDestination = new Point(
					centerDestination.X + CornerOffsets[i].X * cursorDimensions.Width,
					centerDestination.Y + CornerOffsets[i].Y * cursorDimensions.Height)
			};
		}

		if (useShadow)
		{
			Effect = new DropShadowEffect
			{
				Color = color,
				BlurRadius = options.ShadowBlur,
				ShadowDepth = 0,
				Direction = 0,
				Opacity = 1,
				RenderingBias = RenderingBias.Performance
			};
		}
		else
		{
			Effect = null;
		}

		Diagnostics.Log("ApplyOptions: color=" + color + " shadow=" + useShadow + " blur=" + options.ShadowBlur);
		InvalidateVisual();
	}

	/// <summary>
	/// Starts a (possibly animated) move of the cursor to the given viewport top-left.
	/// Mirrors move() from neovide-cursor.js.
	/// </summary>
	public void Move(Point topLeft, Point? fromSource = null, bool force = false)
	{
		if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y)) return;

		// The JS only calls move() when the cursor actually moved (hasMoved).
		// VS fires PositionChanged AND LayoutChanged for the same caret position,
		// so Move() can be re-entered with the same destination; that re-arms the
		// jump, the next Update's Jump resets the springs, and since the
		// destination is unchanged the corners snap to it — killing the in-flight
		// animation mid-way (the "teleport on space at line end" bug). Skip no-ops
		// unless forced — a size change (overwrite mode) must re-target even when
		// the position is unchanged.
		if (!force && initialized && !fromSource.HasValue
			&& Math.Abs(topLeft.X - destination.X) < 0.5
			&& Math.Abs(topLeft.Y - destination.Y) < 0.5)
		{
			return;
		}

		Diagnostics.Log("NeovideCaret.Move to " + topLeft);
		destination = topLeft;
		centerDestination = new Point(
			destination.X + cursorDimensions.Width / 2,
			destination.Y + cursorDimensions.Height / 2);

		if (!initialized || fromSource.HasValue)
		{
			if (fromSource.HasValue)
			{
				Size oldDim = cursorDimensions;
				foreach (Corner corner in corners)
				{
					corner.PreviousDestination = corner.GetDestination(fromSource.Value, oldDim);
					corner.CurrentPosition = corner.PreviousDestination;
				}
			}
			else
			{
				foreach (Corner corner in corners)
				{
					Point cornerDest = corner.GetDestination(centerDestination, cursorDimensions);
					corner.CurrentPosition = cornerDest;
					corner.PreviousDestination = cornerDest;
				}
			}
			initialized = true;
		}

		jumped = true;
	}

	/// <summary>
	/// Immediately places the cursor at the given viewport top-left with no animation.
	/// Mirrors setPosition() from neovide-cursor.js.
	/// </summary>
	public void SetPosition(Point topLeft)
	{
		destination = topLeft;
		centerDestination = new Point(
			destination.X + cursorDimensions.Width / 2,
			destination.Y + cursorDimensions.Height / 2);

		foreach (Corner corner in corners)
		{
			Point dest = corner.GetDestination(centerDestination, cursorDimensions);
			corner.CurrentPosition = dest;
			corner.PreviousDestination = dest;
			corner.AnimationX.Reset();
			corner.AnimationY.Reset();
		}

		initialized = true;
		jumped = false;
		Diagnostics.Log("SetPosition at " + topLeft + ", cursorDims=" + cursorDimensions.Width + "x" + cursorDimensions.Height);
		InvalidateVisual();
	}

	/// <summary>
	/// Advances the animation by <paramref name="dt"/> seconds. Mirrors
	/// updateLoopLogic() from neovide-cursor.js. Returns <c>true</c> while the
	/// cursor is still moving (caller should redraw), <c>false</c> when settled.
	/// </summary>
	public bool Update(double dt, bool isScrolling)
	{
		if (!initialized) return false;

		bool immediateMovement = isScrolling;

		if (jumped)
		{
			int[] ranks = CornerRanks.Compute(corners, cursorDimensions, centerDestination);
			for (int i = 0; i < corners.Length; i++)
			{
				corners[i].Jump(centerDestination, cursorDimensions, ranks[i]);
			}
		}

		bool animating = false;
		foreach (Corner corner in corners)
		{
			if (corner.Update(cursorDimensions, centerDestination, dt, immediateMovement))
			{
				animating = true;
			}
		}

		jumped = false;
		return animating;
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (!initialized)
		{
			Diagnostics.Log("OnRender skipped: not initialized");
			return;
		}

		Diagnostics.Log("OnRender: size=" + ActualWidth + "x" + ActualHeight + " color=" + color + " corner0=" + corners[0].CurrentPosition);

		StreamGeometry geometry = new StreamGeometry();
		using (StreamGeometryContext ctx = geometry.Open())
		{
			ctx.BeginFigure(corners[0].CurrentPosition, true, true);
			for (int i = 1; i < corners.Length; i++)
			{
				ctx.LineTo(corners[i].CurrentPosition, false, false);
			}
		}
		geometry.Freeze();

		dc.DrawGeometry(brush, null, geometry);
	}

	/// <summary>
	/// Resolves the configured cursor color. The special value "default" (or "Default")
	/// uses the current theme's text color. Supports #RGB, #RRGGBB and #AARRGGBB hex.
	/// </summary>
	private static Color ResolveColor(string colorString, Color themedTextColor)
	{
		if (string.Equals(colorString, "default", StringComparison.OrdinalIgnoreCase))
		{
			return themedTextColor;
		}
		if (string.IsNullOrEmpty(colorString) || !colorString.StartsWith("#"))
		{
			return Colors.White;
		}

		string hex = colorString.Substring(1);
		try
		{
			if (hex.Length == 3)
			{
				return Color.FromRgb(
					(byte)(Convert.ToInt32(hex.Substring(0, 1), 16) * 17),
					(byte)(Convert.ToInt32(hex.Substring(1, 1), 16) * 17),
					(byte)(Convert.ToInt32(hex.Substring(2, 1), 16) * 17));
			}
			if (hex.Length == 6)
			{
				return Color.FromRgb(
					(byte)Convert.ToInt32(hex.Substring(0, 2), 16),
					(byte)Convert.ToInt32(hex.Substring(2, 2), 16),
					(byte)Convert.ToInt32(hex.Substring(4, 2), 16));
			}
			if (hex.Length == 8)
			{
				return Color.FromArgb(
					(byte)Convert.ToInt32(hex.Substring(0, 2), 16),
					(byte)Convert.ToInt32(hex.Substring(2, 2), 16),
					(byte)Convert.ToInt32(hex.Substring(4, 2), 16),
					(byte)Convert.ToInt32(hex.Substring(6, 2), 16));
			}
		}
		catch (Exception)
		{
			// fall through to the default below
		}
		return Colors.White;
	}
}
