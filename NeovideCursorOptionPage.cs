using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace NeovideCursor;

/// <summary>
/// Tools → Options → "Neovide Cursor" settings page. Persists to the same registry
/// key that <see cref="Controller"/> reads (HKCU\Software\Vlasov Studio\Neovide Cursor),
/// so the dialog page and direct registry edits (the Smooth Caret way) are fully
/// interchangeable. Hitting OK / Apply writes the registry and then re-applies the
/// settings to every open editor view through <see cref="Applied"/>.
/// </summary>
[Guid("8A2F1C3E-6B4D-4E9F-9A2C-1D5E7B3F6A08")]
public class NeovideCursorOptionPage : DialogPage
{
	/// <summary>Fired after the user applies the page; carries the new settings.</summary>
	public static event Action<Options> Applied;

	private string cursorColor = "#FFC0CB";
	private bool useShadow = true;
	private int shadowBlur = 20;
	private int animationLength = 100;
	private int shortAnimationLength = 40;
	private int trailSize = 100;
	private int caretWidth = 2;

	[Category("General")]
	[DisplayName("Cursor color")]
	[Description("Hex color (#RGB / #RRGGBB / #AARRGGBB) or 'default' to follow the theme text color.")]
	public string CursorColor
	{
		get { return cursorColor; }
		set { cursorColor = value; }
	}

	[Category("General")]
	[DisplayName("Glow")]
	[Description("Draw a soft glow (blurred shadow) behind the animated cursor.")]
	public bool UseShadow
	{
		get { return useShadow; }
		set { useShadow = value; }
	}

	[Category("General")]
	[DisplayName("Glow blur")]
	[Description("Radius of the glow behind the cursor, in pixels.")]
	public int ShadowBlur
	{
		get { return shadowBlur; }
		set { shadowBlur = value; }
	}

	[Category("Animation")]
	[DisplayName("Animation length (ms)")]
	[Description("Damped-spring duration for long jumps (cross-line moves, distant clicks).")]
	public int AnimationLength
	{
		get { return animationLength; }
		set { animationLength = value; }
	}

	[Category("Animation")]
	[DisplayName("Short animation length (ms)")]
	[Description("Damped-spring duration for short same-line moves while typing.")]
	public int ShortAnimationLength
	{
		get { return shortAnimationLength; }
		set { shortAnimationLength = value; }
	}

	[Category("Animation")]
	[DisplayName("Trail size (%)")]
	[Description("0–100. How much the trailing corners lag behind the leading corner.")]
	public int TrailSize
	{
		get { return trailSize; }
		set { trailSize = value; }
	}

	[Category("Animation")]
	[DisplayName("Caret width (px)")]
	[Description("Width of the cursor in device-independent pixels.")]
	public int CaretWidth
	{
		get { return caretWidth; }
		set { caretWidth = value; }
	}

	public override void SaveSettingsToStorage()
	{
		// Deliberately not calling base: the properties live in our own registry key
		// (the one Controller reads), not in VS's DialogPage settings storage.
		WriteRegistry();

		// Re-apply to every open editor view so the change is visible immediately.
		Applied?.Invoke(new Options(
			cursorColor, useShadow, shadowBlur,
			animationLength, shortAnimationLength, trailSize, caretWidth));
	}

	public override void LoadSettingsFromStorage()
	{
		ReadRegistry();
	}

	private static string KeyPath
	{
		get { return @"Software\Vlasov Studio\Neovide Cursor"; }
	}

	private void WriteRegistry()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
			{
				key.SetValue("CursorColor", CursorColor, RegistryValueKind.String);
				key.SetValue("UseShadow", UseShadow ? 1 : 0, RegistryValueKind.DWord);
				key.SetValue("ShadowBlur", ShadowBlur, RegistryValueKind.DWord);
				key.SetValue("AnimationLength", AnimationLength, RegistryValueKind.DWord);
				key.SetValue("ShortAnimationLength", ShortAnimationLength, RegistryValueKind.DWord);
				key.SetValue("TrailSize", TrailSize, RegistryValueKind.DWord);
				key.SetValue("CaretWidth", CaretWidth, RegistryValueKind.DWord);
			}
		}
		catch (Exception)
		{
			// Registry unavailable — ignore; defaults still apply.
		}
	}

	private void ReadRegistry()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
			{
				if (key == null)
				{
					return;
				}
				CursorColor = ReadString(key, "CursorColor", CursorColor);
				UseShadow = ReadInt(key, "UseShadow", UseShadow ? 1 : 0) != 0;
				ShadowBlur = ReadInt(key, "ShadowBlur", ShadowBlur);
				AnimationLength = ReadInt(key, "AnimationLength", AnimationLength);
				ShortAnimationLength = ReadInt(key, "ShortAnimationLength", ShortAnimationLength);
				TrailSize = ReadInt(key, "TrailSize", TrailSize);
				CaretWidth = ReadInt(key, "CaretWidth", CaretWidth);
			}
		}
		catch (Exception)
		{
		}
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
