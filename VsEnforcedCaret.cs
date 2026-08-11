using System;
using Microsoft.VisualStudio.Text.Editor;

namespace NeovideCursor;

/// <summary>
/// Hides the caret for a plain Visual Studio installation. In addition to
/// IsHidden it also drops the opacity of the "Caret" adornment layer so the
/// caret cannot reappear. Ported from Smooth Caret.
/// </summary>
internal class VsEnforcedCaret : ICaretVisibility
{
	private readonly IWpfTextView view;

	private IAdornmentLayer vsCaretAdornmentLayer;

	private bool notSupported;

	public VsEnforcedCaret(IWpfTextView view_)
	{
		view = view_;
	}

	public void Show()
	{
		view.Caret.IsHidden = false;
		EnforceCaretVisibility();
	}

	public void Hide()
	{
		Initialize();
		view.Caret.IsHidden = true;
	}

	private void Initialize()
	{
		if (notSupported || IsInitialized())
		{
			return;
		}
		try
		{
			vsCaretAdornmentLayer = view.GetAdornmentLayer("Caret");
		}
		catch (Exception)
		{
			notSupported = true;
		}
	}

	private void EnforceCaretVisibility()
	{
		if (IsInitialized())
		{
			vsCaretAdornmentLayer.Opacity = 1.0;
		}
	}

	private bool IsInitialized()
	{
		return vsCaretAdornmentLayer != null;
	}
}
