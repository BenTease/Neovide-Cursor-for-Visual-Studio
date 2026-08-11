using System;
using Microsoft.VisualStudio.Text.Editor;

namespace NeovideCursor;

/// <summary>
/// Caret visibility when VsVim is installed (Vim's block caret lives in its own
/// adornment layer). Ported from Smooth Caret.
/// </summary>
internal class VsVimCaret : ICaretVisibility
{
	private readonly IWpfTextView view;

	private IAdornmentLayer layer;

	private readonly ICaretVisibility vsCaret;

	public VsVimCaret(IWpfTextView view_, ICaretVisibility vsCaret_)
	{
		view = view_;
		vsCaret = vsCaret_;
	}

	public void Hide()
	{
		if (IsVsCaretVisible())
		{
			HideVsCaret();
		}
		else
		{
			HideBlockCaret();
		}
	}

	public void Show()
	{
		if (!IsBlockCaretLayerVisible())
		{
			ShowBlockCaret();
		}
		else if (IsBlockCaretLayerEmpty())
		{
			ShowVsCaret();
		}
	}

	private void ShowBlockCaret()
	{
		GetAdornmentLayer();
		if (layer != null)
		{
			layer.Opacity = 1.0;
		}
	}

	private bool IsVsCaretVisible()
	{
		return !view.Caret.IsHidden;
	}

	private void HideVsCaret()
	{
		vsCaret.Hide();
	}

	private void ShowVsCaret()
	{
		vsCaret.Show();
	}

	private void HideBlockCaret()
	{
		GetAdornmentLayer();
		if (layer != null)
		{
			layer.Opacity = 0.0;
		}
	}

	private bool IsBlockCaretLayerVisible()
	{
		GetAdornmentLayer();
		if (layer != null)
		{
			return layer.Opacity == 1.0;
		}
		return false;
	}

	private bool IsBlockCaretLayerEmpty()
	{
		GetAdornmentLayer();
		if (layer != null)
		{
			return layer.IsEmpty;
		}
		return true;
	}

	private void GetAdornmentLayer()
	{
		if (layer != null)
		{
			return;
		}
		try
		{
			layer = view.GetAdornmentLayer("BlockCaretAdornmentLayer");
		}
		catch (Exception)
		{
		}
	}
}
