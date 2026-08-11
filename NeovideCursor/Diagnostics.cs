using System;
using System.IO;

namespace NeovideCursor;

/// <summary>
/// Minimal file logger used only while diagnosing the "nothing renders" issue.
/// Appends to %TEMP%\NeovideCursor.log so the user can inspect what the
/// extension did without digging through the VS ActivityLog.
/// </summary>
internal static class Diagnostics
{
	private static readonly object Sync = new object();

	private static string path;

	private static string Path
	{
		get
		{
			if (path == null)
			{
				path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NeovideCursor.log");
			}
			return path;
		}
	}

	public static void Log(string message)
	{
		try
		{
			lock (Sync)
			{
				File.AppendAllText(Path, string.Format("{0:HH:mm:ss.fff} [{1}] {2}\r\n", DateTime.Now, System.Threading.Thread.CurrentThread.ManagedThreadId, message));
			}
		}
		catch (Exception)
		{
			// logging must never break the extension
		}
	}

	public static void Clear()
	{
		try
		{
			lock (Sync)
			{
				if (File.Exists(Path))
				{
					File.Delete(Path);
				}
			}
		}
		catch (Exception)
		{
		}
	}
}
