using System;
using System.Drawing;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell.Interop;

namespace NeovideCursor;

public class VSServices
{
	private readonly IServiceProvider serviceProvider;

	private readonly IVsUIShell uiShell;

	private readonly IVsShell vsShell;

	public VSServices(IServiceProvider serviceProvider)
	{
		this.serviceProvider = serviceProvider;
		uiShell = serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
		vsShell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
	}

	public bool IsPackageInstalled(Guid guid)
	{
		int num = 0;
		if (vsShell.IsPackageInstalled(ref guid, out num) == 0)
		{
			return num != 0;
		}
		return false;
	}

	/// <summary>HWND of the main Visual Studio window (used to anchor the global caret overlay).</summary>
	public IntPtr GetMainWindowHwnd()
	{
		try
		{
			IntPtr hwnd;
			uiShell.GetDialogOwnerHwnd(out hwnd);
			return hwnd;
		}
		catch (Exception)
		{
			return IntPtr.Zero;
		}
	}

	/// <summary>The current theme's text color, used as the fallback cursor color.</summary>
	public System.Windows.Media.Color GetThemedTextColor()
	{
		Guid colorCategory = new Guid("624ed9c3-bdfd-41fa-96c3-7c824ea32e3d");
		uint value = GetThemedColor(uiShell, ref colorCategory, "WindowText", 0);
		System.Drawing.Color color = ColorTranslator.FromWin32((int)value);
		return System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
	}

	private static uint GetThemedColor(IVsUIShell shell, ref Guid colorCategory, string colorName, uint colorType)
	{
		IVsUIShell5 obj = shell as IVsUIShell5;
		return obj.GetThemedColor(ref colorCategory, colorName, colorType);
	}
}
