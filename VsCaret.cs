using Microsoft.VisualStudio.Text.Editor;

namespace NeovideCursor;

/// <summary>
/// Default caret visibility: simply toggles ITextCaret.IsHidden. Ported from Smooth Caret.
/// </summary>
internal class VsCaret : ICaretVisibility
{
	private readonly IWpfTextView view;

	public VsCaret(IWpfTextView view_)
	{
		view = view_;
	}

	public void Show()
	{
		view.Caret.IsHidden = false;
	}

	public void Hide()
	{
		view.Caret.IsHidden = true;
	}
}
