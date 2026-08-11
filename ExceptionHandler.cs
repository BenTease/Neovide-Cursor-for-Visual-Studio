using System;
using System.Windows;

namespace NeovideCursor;

internal class ExceptionHandler
{
	public static void UnhandledException(Exception e)
	{
		MessageBox.Show(e.ToString(), "[Neovide Cursor] Unhandled exception.", MessageBoxButton.OK, MessageBoxImage.Hand);
	}
}
