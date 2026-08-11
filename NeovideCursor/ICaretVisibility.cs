namespace NeovideCursor;

/// <summary>
/// Abstraction over the native caret, so it can be hidden even when other
/// extensions (VsVim / Visual Assist) render their own caret. Ported from
/// Smooth Caret.
/// </summary>
internal interface ICaretVisibility
{
	void Show();

	void Hide();
}
