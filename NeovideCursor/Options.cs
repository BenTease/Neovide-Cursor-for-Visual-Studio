namespace NeovideCursor;

/// <summary>
/// Configuration loaded from the registry key
/// <c>HKCU\Software\Vlasov Studio\Neovide Cursor</c>.
/// Defaults mirror the constants in neovide-cursor.js.
/// </summary>
public class Options
{
	/// <summary>Hex color (#RGB / #RRGGBB / #AARRGGBB) or the special value "default" for the theme text color.</summary>
	public string CursorColor { get; }

	public bool UseShadow { get; }

	public double ShadowBlur { get; }

	/// <summary>Damped spring animation length in seconds (long jump).</summary>
	public double AnimationLength { get; }

	/// <summary>Damped spring animation length in seconds (short jump on a single line).</summary>
	public double ShortAnimationLength { get; }

	/// <summary>Trail density 0.0..1.0 — controls how much the trailing corners lag.</summary>
	public double TrailSize { get; }

	/// <summary>Cursor width in device-independent pixels.</summary>
	public double CaretWidth { get; }

	public Options(
		string cursorColor = "#FFC0CB",
		bool useShadow = true,
		int shadowBlur = 20,
		int animationLength = 100,
		int shortAnimationLength = 40,
		int trailSize = 100,
		int caretWidth = 2)
	{
		CursorColor = cursorColor;
		UseShadow = useShadow;
		ShadowBlur = shadowBlur;
		AnimationLength = animationLength / 1000.0;
		ShortAnimationLength = shortAnimationLength / 1000.0;
		TrailSize = Clamp(trailSize / 100.0, 0, 1);
		CaretWidth = caretWidth;
	}

	private static double Clamp(double val, double min, double max)
	{
		if (val < min) return min;
		if (val > max) return max;
		return val;
	}
}
