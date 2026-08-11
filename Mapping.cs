using System;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace NeovideCursor;

/// <summary>
/// Maps caret positions to viewport-relative coordinates. Ported from
/// Smooth Caret's Mapping, so the animated caret is anchored to the viewport
/// (like the position:fixed canvas in neovide-cursor.js).
/// </summary>
internal class Mapping
{
	private readonly IWpfTextView view;

	public Mapping(IWpfTextView view_)
	{
		view = view_;
	}

	public Point GetPoint(CaretPosition c)
	{
		IWpfTextViewLine textViewLine = view.GetTextViewLineContainingBufferPosition(c.BufferPosition);
		TextBounds characterBounds = textViewLine.GetCharacterBounds(c.VirtualBufferPosition);
		return new Point(
			characterBounds.Left - view.ViewportLeft,
			textViewLine.TextTop - view.ViewportTop);
	}

	public bool IsValidBufferPosition(CaretPosition c)
	{
		try
		{
			view.GetTextViewLineContainingBufferPosition(c.BufferPosition);
		}
		catch (ArgumentException)
		{
			return false;
		}
		return true;
	}
}
