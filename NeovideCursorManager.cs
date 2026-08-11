using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Editor;

namespace NeovideCursor;

/// <summary>
/// Coordinates the single shared overlay caret across all editor views. It tracks
/// which view holds keyboard focus and feeds the overlay that view's caret position;
/// when focus switches between views, the caret's damped springs animate it across
/// the window (a focus switch is just a regular Move with a far target). Non-focused
/// views render nothing, so no caret is left behind.
/// </summary>
internal sealed class NeovideCursorManager
{
	private const double BlurDeferMs = 60;
	private const double ScrollDebounceMs = 100;

	private static NeovideCursorManager instance;

	private readonly Color themedColor;
	private readonly IServiceProvider serviceProvider;

	private Options currentOptions;
	private NeovideCursorOverlay overlay;
	private bool creatingOverlay;
	private Adornment active;
	private DispatcherTimer blurTimer;
	private DispatcherTimer scrollTimer;

	/// <summary>Current caret width (updated when the user changes it in Tools → Options).</summary>
	public double CaretWidth
	{
		get { return currentOptions.CaretWidth; }
	}

	/// <summary>Gets the shared manager, creating it on first use from the first view's services.</summary>
	public static NeovideCursorManager GetOrCreate(IServiceProvider serviceProvider, Options options, Color themedColor)
	{
		if (instance == null)
		{
			instance = new NeovideCursorManager(serviceProvider, options, themedColor);
		}
		return instance;
	}

	private NeovideCursorManager(IServiceProvider serviceProvider, Options options, Color themedColor)
	{
		this.themedColor = themedColor;
		this.serviceProvider = serviceProvider;
		currentOptions = options;

		// CRITICAL: the overlay is NOT created here. Creating the overlay window
		// (Window.Show) pumps the dispatcher; while the FIRST view is being created that
		// pumping can reentrantly create a SECOND view whose GetOrCreate runs before this
		// ctor returns — at which point the static `instance` is still null, so the second
		// view builds its OWN overlay + manager. Result: two overlapping carets that never
		// travel across views (each sticks to its own view). Deferring overlay creation to
		// the first focus event (EnsureOverlay) guarantees `instance` is assigned before
		// any window is shown, so every view shares the one manager.
		NeovideCursorOptionPage.Applied += OnOptionsApplied;
	}

	/// <summary>
	/// Creates the overlay on first use. Idempotent and reentrancy-safe: the
	/// <paramref name="creatingOverlay"/> guard makes a reentrant call (possible while
	/// <c>Show()</c> pumps the dispatcher) return early instead of recursing.
	/// </summary>
	private void EnsureOverlay()
	{
		if (overlay != null || creatingOverlay) return;
		creatingOverlay = true;
		try
		{
			IntPtr mainHwnd = new VSServices(serviceProvider).GetMainWindowHwnd();
			overlay = new NeovideCursorOverlay(mainHwnd, currentOptions, themedColor);
			overlay.Start();
			Diagnostics.Log("Overlay created via EnsureOverlay");
		}
		catch (Exception ex)
		{
			Diagnostics.Log("Overlay creation FAILED: " + ex);
			ExceptionHandler.UnhandledException(ex);
		}
		finally
		{
			creatingOverlay = false;
		}
	}

	public void Register(Adornment adornment)
	{
		// The overlay is shared; nothing per-view to track. Kept as a hook for symmetry.
	}

	public void Unregister(Adornment adornment)
	{
		if (active != adornment) return;
		active = null;
		if (overlay != null)
		{
			overlay.DesiredVisible = false;
			overlay.UpdateVisibility();
		}
	}

	/// <summary>The given view just gained keyboard focus.</summary>
	public void OnViewFocused(Adornment adornment)
	{
		if (blurTimer != null) blurTimer.Stop();
		EnsureOverlay();
		if (overlay == null) return;

		Diagnostics.Log("OnViewFocused id=" + adornment.Id + " prevActive=" + (active != null ? active.Id : -1) + " caretInitialized=" + overlay.IsCaretInitialized);

		active = adornment;
		overlay.IsScrolling = false;
		overlay.DesiredVisible = true;

		Point screenPos;
		Size caretSize;
		if (TryGetCaretScreenPosition(adornment, out screenPos, out caretSize))
		{
			// Always animate (Move) on focus: the caret's damped springs carry it from
			// wherever it currently is (the previously-focused view) to this view, which
			// is exactly the cross-view fly we want. Snap (SetPosition) only on the very
			// first placement, when there is no valid current position to animate from.
			// The old "travel = (a different view held focus)" heuristic proved unreliable
			// in real event flows (it snapped on every switch), so the decision now lives
			// in the caret itself via its initialized flag.
			overlay.MoveCaretToScreen(screenPos, caretSize, immediate: !overlay.IsCaretInitialized);
		}
		else
		{
			Diagnostics.Log("OnViewFocused id=" + adornment.Id + ": TryGetCaretScreenPosition FAILED");
			overlay.DesiredVisible = false;
		}
		overlay.UpdateVisibility();
	}

	/// <summary>The given view lost keyboard focus.</summary>
	public void OnViewBlurred(Adornment adornment)
	{
		Diagnostics.Log("OnViewBlurred id=" + adornment.Id + " active=" + (active != null ? active.Id : -1));
		if (active != adornment) return; // focus already moved to another view

		// Focus may be moving to another editor view in the same WPF focus cycle; wait
		// briefly before hiding so a view-to-view switch still travels.
		if (blurTimer == null)
		{
			blurTimer = new DispatcherTimer();
			blurTimer.Interval = TimeSpan.FromMilliseconds(BlurDeferMs);
			blurTimer.Tick += OnBlurTimerTick;
		}
		blurTimer.Stop();
		blurTimer.Start();
	}

	private void OnBlurTimerTick(object sender, EventArgs e)
	{
		blurTimer.Stop();
		Diagnostics.Log("OnBlurTimerTick fired; active=" + (active != null ? active.Id : -1));
		active = null;
		if (overlay != null)
		{
			overlay.DesiredVisible = false;
			overlay.UpdateVisibility();
		}
	}

	/// <summary>The active view's caret moved (caret position / layout changes).</summary>
	public void OnCaretMoved(Adornment adornment, bool immediate)
	{
		EnsureOverlay();
		if (overlay == null || active != adornment) return;

		Point screenPos;
		Size caretSize;
		if (!TryGetCaretScreenPosition(adornment, out screenPos, out caretSize)) return;

		overlay.MoveCaretToScreen(screenPos, caretSize, immediate);
	}

	/// <summary>The active view's viewport scrolled; snap the caret and debounce.</summary>
	public void OnViewScrolled(Adornment adornment)
	{
		EnsureOverlay();
		if (overlay == null || active != adornment) return;
		overlay.IsScrolling = true;
		if (scrollTimer == null)
		{
			scrollTimer = new DispatcherTimer();
			scrollTimer.Interval = TimeSpan.FromMilliseconds(ScrollDebounceMs);
			scrollTimer.Tick += OnScrollTimerTick;
		}
		scrollTimer.Stop();
		scrollTimer.Start();
	}

	private void OnScrollTimerTick(object sender, EventArgs e)
	{
		scrollTimer.Stop();
		if (overlay != null) overlay.IsScrolling = false;
	}

	/// <summary>Converts the view's caret to a screen position (physical px) + caret size.</summary>
	public static bool TryGetCaretScreenPosition(Adornment adornment, out Point screenPos, out Size caretSize)
	{
		return adornment.TryGetCaretScreenPosition(out screenPos, out caretSize);
	}

	private void OnOptionsApplied(Options newOptions)
	{
		try
		{
			currentOptions = newOptions;
			if (overlay != null) overlay.ApplyOptions(newOptions, themedColor);
		}
		catch (Exception ex)
		{
			ExceptionHandler.UnhandledException(ex);
		}
	}
}
