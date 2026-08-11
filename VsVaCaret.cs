using System;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Editor;

namespace NeovideCursor;

/// <summary>
/// Caret visibility for Visual Assist (which replaces the standard caret with its
/// own). Hiding is deferred to a Send-priority dispatcher callback. Ported from
/// Smooth Caret.
/// </summary>
internal class VsVaCaret : ICaretVisibility
{
	private readonly IWpfTextView view;

	public VsVaCaret(IWpfTextView view_)
	{
		view = view_;
	}

	public void Show()
	{
		view.Caret.IsHidden = false;
	}

	public void Hide()
	{
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, (Action)delegate
		{
			HideInternal();
		});
	}

	private void HideInternal()
	{
		view.Caret.IsHidden = true;
	}
}
