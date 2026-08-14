using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace NeovideCursor;

/// <summary>
/// Per-view integration between the VS editor and the shared global caret. Each view
/// hides its native caret, tracks caret / layout / focus events, and reports its caret
/// screen position to <see cref="NeovideCursorManager"/>, which drives the single
/// overlay caret. Rendering is NOT per-view anymore — that is what lets the caret
/// travel across views when focus switches.
/// </summary>
internal class Adornment
{
	private readonly IWpfTextView view;

	private readonly Mapping mapping;

	private readonly Options options;

	private readonly VSServices vs;

	private readonly NeovideCursorManager manager;

	private readonly ICaretVisibility vsCaret;

	private static int nextId;

	private readonly int id = System.Threading.Interlocked.Increment(ref nextId);

	/// <summary>Unique per-instance id, used to correlate focus events in the diagnostic log.</summary>
	public int Id
	{
		get { return id; }
	}

	private double viewportLeft;

	private double viewportTop;

	private bool lastOverwriteMode;

	public Adornment(IWpfTextView view_, IServiceProvider serviceProvider)
	{
		view = view_;
		Diagnostics.Log("Adornment ctor id=" + id + ": viewport=" + view.ViewportWidth + "x" + view.ViewportHeight + ", caret pos=" + view.Caret.Position.BufferPosition.Position);
		mapping = new Mapping(view);
		viewportLeft = view.ViewportLeft;
		viewportTop = view.ViewportTop;

		// The service provider comes from the editor MEF composition (imported in
		// WpfTextViewsManager), so this never waits on the AsyncPackage having loaded.
		Controller controller = new Controller(serviceProvider);
		options = controller.GetOptions();
		vs = controller.GetVS();

		manager = NeovideCursorManager.GetOrCreate(serviceProvider, options, vs.GetThemedTextColor());
		manager.Register(this);

		vsCaret = CreateVsCaret();
		vsCaret.Hide();

		// Overwrite mode (Insert key) widens the caret into a block, like the original
		// neovide-cursor mirrors VSCode's overtype caret width.
		lastOverwriteMode = IsOverwriteMode();
		view.Options.OptionChanged += OnOptionChanged;

		view.Caret.PositionChanged += OnCaretPositionChanged;
		view.LayoutChanged += OnLayoutChanged;
		view.Closed += OnViewClosed;

		// Keyboard focus events bubble from whatever element inside the editor actually
		// holds focus, so these fire reliably when the user clicks/tabs into this view.
		view.VisualElement.AddHandler(
			Keyboard.GotKeyboardFocusEvent,
			new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus),
			true);
		view.VisualElement.AddHandler(
			Keyboard.LostKeyboardFocusEvent,
			new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus),
			true);

		// The view may already hold focus (e.g. a file open takes focus while the
		// adornment is being created) — make sure the caret is placed.
		if (view.VisualElement.IsKeyboardFocusWithin)
		{
			manager.OnViewFocused(this);
		}

		Diagnostics.Log("Adornment ctor done");
	}

	private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		try
		{
			Diagnostics.Log("OnGotKeyboardFocus id=" + id);
			manager.OnViewFocused(this);
		}
		catch (Exception ex)
		{
			ExceptionHandler.UnhandledException(ex);
		}
	}

	private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		try
		{
			Diagnostics.Log("OnLostKeyboardFocus id=" + id);
			// Ensure the defocused view never leaves its native caret visible while the
			// global caret is flying to the newly-focused view.
			vsCaret.Hide();
			manager.OnViewBlurred(this);
		}
		catch (Exception ex)
		{
			ExceptionHandler.UnhandledException(ex);
		}
	}

	/// <summary>
	/// Fires on any editor-option change. We only care about overwrite mode (toggled
	/// by the Insert key): when it flips, re-report the caret so the overlay widens
	/// into a block (or back to the thin line).
	/// </summary>
	private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e)
	{
		try
		{
			bool over = IsOverwriteMode();
			if (over != lastOverwriteMode)
			{
				lastOverwriteMode = over;
				Diagnostics.Log("OverwriteMode -> " + over);
				vsCaret.Hide();
				manager.OnCaretMoved(this, immediate: false);
			}
		}
		catch (Exception ex)
		{
			ExceptionHandler.UnhandledException(ex);
		}
	}

	/// <summary>Whether this view is in overwrite mode (Insert key toggles it).</summary>
	private bool IsOverwriteMode()
	{
		try
		{
			return view.Options.GetOptionValue(DefaultTextViewOptions.OverwriteModeId);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>The width of one character cell — the block the caret widens to in overwrite mode.</summary>
	private double GetOverwriteWidth()
	{
		try
		{
			IFormattedLineSource source = view.FormattedLineSource;
			return source != null ? source.ColumnWidth : 8.0;
		}
		catch (Exception)
		{
			return 8.0;
		}
	}

	private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs args)
	{
		try
		{
			Diagnostics.Log("OnCaretPositionChanged bufferPos=" + args.NewPosition.BufferPosition.Position);
			if (mapping.IsValidBufferPosition(args.NewPosition))
			{
				manager.OnCaretMoved(this, immediate: false);
			}
			else
			{
				Diagnostics.Log("OnCaretPositionChanged: INVALID position, skipping");
			}
			// Some operations re-show the native caret; keep it hidden.
			vsCaret.Hide();
		}
		catch (Exception e)
		{
			ExceptionHandler.UnhandledException(e);
		}
	}

	private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs args)
	{
		try
		{
			Diagnostics.Log("OnLayoutChanged viewport=" + view.ViewportLeft + "," + view.ViewportTop + " cached=" + viewportLeft + "," + viewportTop);
			// A viewport change means the user is scrolling: snap the caret so it does
			// not rubber-band behind the scroll (like neovide-cursor's isScrolling flag).
			if (viewportLeft != view.ViewportLeft || viewportTop != view.ViewportTop)
			{
				viewportLeft = view.ViewportLeft;
				viewportTop = view.ViewportTop;
				manager.OnViewScrolled(this);
				manager.OnCaretMoved(this, immediate: true);
			}
			else if (mapping.IsValidBufferPosition(view.Caret.Position))
			{
				manager.OnCaretMoved(this, immediate: false);
			}
			vsCaret.Hide();
		}
		catch (Exception e)
		{
			ExceptionHandler.UnhandledException(e);
		}
	}

	private void OnViewClosed(object sender, EventArgs e)
	{
		view.Caret.PositionChanged -= OnCaretPositionChanged;
		view.LayoutChanged -= OnLayoutChanged;
		view.Closed -= OnViewClosed;
		view.Options.OptionChanged -= OnOptionChanged;
		view.VisualElement.RemoveHandler(
			Keyboard.GotKeyboardFocusEvent,
			new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus));
		view.VisualElement.RemoveHandler(
			Keyboard.LostKeyboardFocusEvent,
			new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus));
		manager.Unregister(this);
	}

	/// <summary>
	/// Converts the view's caret to a screen position (physical pixels) and the caret
	/// size (line height). Returns <c>false</c> when the position can't be resolved.
	/// </summary>
	public bool TryGetCaretScreenPosition(out Point screenPos, out Size caretSize)
	{
		screenPos = default(Point);
		caretSize = default(Size);
		try
		{
			CaretPosition position = view.Caret.Position;
			if (!mapping.IsValidBufferPosition(position))
			{
				return false;
			}

			IWpfTextViewLine line = view.GetTextViewLineContainingBufferPosition(position.BufferPosition);

			// VS applies zoom (Ctrl+wheel) as a render transform: PointToScreen below already
			// scales the position, but the line metrics (TextHeight / ColumnWidth) stay
			// unscaled. Multiply the caret quad by the zoom factor so it grows/shrinks with
			// the font instead of staying pinned at the line top at the old size.
			double zoom = GetZoomFactor();
			double height = (line != null ? line.TextHeight : 18) * zoom;
			double width = (IsOverwriteMode() ? GetOverwriteWidth() : manager.CaretWidth) * zoom;
			caretSize = new Size(width, height);

			if (zoom != 1.0)
			{
				Diagnostics.Log("caret size (zoomed) zoom=" + zoom + " width=" + width + " height=" + height);
			}

			Point viewportPoint = mapping.GetPoint(position);
			Point screen = view.VisualElement.PointToScreen(viewportPoint);
			if (double.IsNaN(screen.X) || double.IsNaN(screen.Y))
			{
				return false;
			}
			screenPos = screen;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Zoom factor (1.0 = 100%). VS zoom scales the rendered position but not the line metrics.</summary>
	private double GetZoomFactor()
	{
		try
		{
			double z = view.ZoomLevel / 100.0;
			if (z > 0 && !double.IsNaN(z) && !double.IsInfinity(z))
			{
				return z;
			}
		}
		catch (Exception)
		{
		}
		return 1.0;
	}

	private ICaretVisibility CreateVsCaret()
	{
		Guid vimGuid = new Guid("a284d12c-1e96-451b-a3b0-5486a1beb6ca");
		Guid vaGuid = new Guid("44630d46-96b5-488c-8df9-26e21db8c1a3");

		ICaretVisibility baseCaret;
		if (!vs.IsPackageInstalled(vaGuid))
		{
			baseCaret = new VsEnforcedCaret(view);
		}
		else
		{
			baseCaret = new VsVaCaret(view);
		}

		if (vs.IsPackageInstalled(vimGuid))
		{
			return new VsVimCaret(view, baseCaret);
		}
		return baseCaret;
	}
}
