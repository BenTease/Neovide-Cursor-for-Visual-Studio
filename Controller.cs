using System;
using Microsoft.Win32;

namespace NeovideCursor;

internal class Controller
{
	private readonly VSServices vs;

	private readonly Options options;

	public Controller(IServiceProvider serviceProvider)
	{
		vs = new VSServices(serviceProvider);
		options = LoadOptions();
	}

	public VSServices GetVS()
	{
		return vs;
	}

	public Options GetOptions()
	{
		return options;
	}

	private static Options LoadOptions()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Vlasov Studio\Neovide Cursor"))
			{
				if (key != null)
				{
					return new Options(
						cursorColor: ReadString(key, "CursorColor", "#FFC0CB"),
						useShadow: ReadInt(key, "UseShadow", 1) != 0,
						shadowBlur: ReadInt(key, "ShadowBlur", 20),
						animationLength: ReadInt(key, "AnimationLength", 100),
						shortAnimationLength: ReadInt(key, "ShortAnimationLength", 40),
						trailSize: ReadInt(key, "TrailSize", 100),
						caretWidth: ReadInt(key, "CaretWidth", 2));
				}
			}
		}
		catch (Exception)
		{
			// fall back to defaults
		}
		return new Options();
	}

	private static int ReadInt(RegistryKey key, string name, int defaultValue)
	{
		try
		{
			return Convert.ToInt32(key.GetValue(name, defaultValue));
		}
		catch (Exception)
		{
			return defaultValue;
		}
	}

	private static string ReadString(RegistryKey key, string name, string defaultValue)
	{
		try
		{
			return Convert.ToString(key.GetValue(name, defaultValue));
		}
		catch (Exception)
		{
			return defaultValue;
		}
	}
}
