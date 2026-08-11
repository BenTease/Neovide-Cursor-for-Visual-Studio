using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace NeovideCursor;

/// <summary>
/// A transparent, always-on-top, click-through overlay window covering the Visual
/// Studio main window, hosting the single shared <see cref="NeovideCaret"/>. Because
/// the caret lives here (not inside any editor's adornment layer), it can travel
/// across the whole VS window when focus switches between editor panes — the caret
/// "flies" from the previously-focused view to the newly-focused one.
/// </summary>
internal sealed class NeovideCursorOverlay
{
	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 0x00000020;
	private const int WS_EX_TOOLWINDOW = 0x00000080;
	private const int WS_EX_NOACTIVATE = 0x08000000;
	private const int WS_EX_LAYERED = 0x00080000;

	private const int WM_MOVE = 0x0003;
	private const int WM_SIZE = 0x0005;
	private const int WM_ACTIVATE = 0x0006;

	private readonly IntPtr mainHwnd;
	private readonly Window window;
	private readonly NeovideCaret caret;

	private HwndSource overlaySource;
	private HwndSource mainSource;
	private bool renderingHooked;
	private bool visible;
	private bool vsForeground = true;
	private double? lastFrameTime;
	private double dpiScale = 1.0;
	private double clientOriginX;
	private double clientOriginY;
	private double clientWidth;
	private double clientHeight;

	/// <summary>Whether the caret should currently be on screen. Set by <see cref="NeovideCursorManager"/>.</summary>
	public bool DesiredVisible { get; set; }

	/// <summary>Whether the caret has ever been placed — used to snap on its very first placement.</summary>
	public bool IsCaretInitialized
	{
		get { return caret.IsPlaced; }
	}

	/// <summary>While true, corner updates snap instead of rubber-banding (during a scroll).</summary>
	public bool IsScrolling { get; set; }

	public NeovideCursorOverlay(IntPtr mainHwnd, Options options, Color themedColor)
	{
		this.mainHwnd = mainHwnd;

		caret = new NeovideCaret(options, themedColor)
		{
			IsHitTestVisible = false,
			Focusable = false
		};

		Grid root = new Grid();
		root.Children.Add(caret);

		window = new Window
		{
			WindowStyle = WindowStyle.None,
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			ShowInTaskbar = false,
			ShowActivated = false,
			Topmost = true,
			Focusable = false,
			IsHitTestVisible = false,
			Content = root
		};

		// Owned by the main VS window: stays above it, minimizes with it, no taskbar entry.
		if (mainHwnd != IntPtr.Zero)
		{
			new WindowInteropHelper(window).Owner = mainHwnd;
		}

		UpdateWindowPlacement();
		window.Show();

		overlaySource = PresentationSource.FromVisual(window) as HwndSource;
		if (overlaySource != null)
		{
			dpiScale = overlaySource.CompositionTarget.TransformToDevice.M11;
			if (dpiScale <= 0) dpiScale = 1.0;

			// Click-through + no activation so the overlay never steals input or focus.
			IntPtr overlayHwnd = new WindowInteropHelper(window).Handle;
			int exStyle = GetWindowLong(overlayHwnd, GWL_EXSTYLE);
			SetWindowLong(overlayHwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);
		}

		// Keep the overlay glued to the main window and hide it when VS is not foreground.
		if (mainHwnd != IntPtr.Zero)
		{
			mainSource = HwndSource.FromHwnd(mainHwnd);
			if (mainSource != null)
			{
				mainSource.AddHook(MainWindowProc);
			}
		}

		UpdateWindowPlacement(); // redo now that the real DPI scale is known
		UpdateVisibility();
		Diagnostics.Log("Overlay created, dpiScale=" + dpiScale + " size=" + window.Width + "x" + window.Height);
	}

	/// <summary>
	/// Moves the caret to the given screen position (physical pixels). When
	/// <paramref name="immediate"/> is false the damped springs animate from the
	/// current position — which is how a focus switch travels across the window.
	/// </summary>
	public void MoveCaretToScreen(Point screenPx, Size caretSize, bool immediate)
	{
		// A dimension change (e.g. overwrite mode widens the caret to a block) must
		// re-target the springs even when the position is unchanged, so force the move.
		bool sizeChanged = caret.UpdateCursorSize(caretSize.Width, caretSize.Height);

		double dipPerPx = 1.0 / dpiScale;
		Point local = new Point(
			(screenPx.X - clientOriginX) * dipPerPx,
			(screenPx.Y - clientOriginY) * dipPerPx);

		if (immediate)
		{
			caret.SetPosition(local);
		}
		else
		{
			caret.Move(local, force: sizeChanged);
		}

		UpdateVisibility();
	}

	public void ApplyOptions(Options options, Color themedColor)
	{
		caret.ApplyOptions(options, themedColor);
	}

	public void Start()
	{
		if (renderingHooked) return;
		renderingHooked = true;
		CompositionTarget.Rendering += OnRendering;
	}

	public void Stop()
	{
		if (!renderingHooked) return;
		renderingHooked = false;
		CompositionTarget.Rendering -= OnRendering;
	}

	private void OnRendering(object sender, EventArgs e)
	{
		try
		{
			double now = (e as RenderingEventArgs)?.RenderingTime.TotalSeconds ?? 0;
			if (lastFrameTime == null)
			{
				lastFrameTime = now;
				return;
			}

			double dt = Math.Min(now - lastFrameTime.Value, 1.0 / 30.0);
			lastFrameTime = now;

			bool animating = caret.Update(dt, IsScrolling);
			if (animating)
			{
				caret.InvalidateVisual();
			}
		}
		catch (Exception ex)
		{
			Stop();
			ExceptionHandler.UnhandledException(ex);
		}
	}

	public void UpdateVisibility()
	{
		bool show = DesiredVisible && vsForeground && mainHwnd != IntPtr.Zero;
		if (show && !visible)
		{
			if (!window.IsVisible) window.Show();
			visible = true;
		}
		else if (!show && visible)
		{
			window.Hide();
			visible = false;
		}
	}

	/// <summary>Re-aligns the overlay over the main window's client area. Safe to call any time.</summary>
	public void UpdateWindowPlacement()
	{
		RECT rect = new RECT();
		GetClientRect(mainHwnd, out rect);
		POINT origin = new POINT { x = rect.left, y = rect.top };
		ClientToScreen(mainHwnd, ref origin);

		clientOriginX = origin.x;
		clientOriginY = origin.y;
		clientWidth = rect.right - rect.left;
		clientHeight = rect.bottom - rect.top;

		double dipPerPx = 1.0 / dpiScale;
		window.Left = clientOriginX * dipPerPx;
		window.Top = clientOriginY * dipPerPx;
		window.Width = Math.Max(1, clientWidth * dipPerPx);
		window.Height = Math.Max(1, clientHeight * dipPerPx);

		caret.Width = window.Width;
		caret.Height = window.Height;
	}

	private IntPtr MainWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		try
		{
			if (msg == WM_MOVE || msg == WM_SIZE)
			{
				UpdateWindowPlacement();
			}
			else if (msg == WM_ACTIVATE)
			{
				// WA_INACTIVE == 0; WA_ACTIVE / WA_CLICKACTIVE hide-on-deactivate covers
				// ALT+TAB away and modal dialogs taking activation from the main window.
				vsForeground = (wParam.ToInt32() & 0xFFFF) != 0;
				UpdateVisibility();
			}
		}
		catch (Exception)
		{
			// Never let a hook failure break VS.
		}
		return IntPtr.Zero;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;
		public int y;
	}

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	private static void SetWindowLong(IntPtr hWnd, int nIndex, int value)
	{
		if (IntPtr.Size == 8)
		{
			SetWindowLongPtr64(hWnd, nIndex, new IntPtr(value));
		}
		else
		{
			SetWindowLong32(hWnd, nIndex, value);
		}
	}
}
