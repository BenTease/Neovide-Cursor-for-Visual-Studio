using System;
using System.Windows;

namespace NeovideCursor;

/// <summary>
/// Damped spring animation on a single axis. Faithful port of the
/// <c>DampedSpringAnimation</c> class from neovide-cursor.js.
/// </summary>
internal class DampedSpringAnimation
{
	public double Position;
	public double Velocity;

	/// <summary>
	/// Advances the spring by <paramref name="dt"/> seconds.
	/// Returns <c>false</c> once the spring has settled (position &lt; 0.01).
	/// </summary>
	public bool Update(double dt, double animationLength)
	{
		if (animationLength <= dt || Position == 0.0)
		{
			Reset();
			return false;
		}

		double omega = 4.0 / animationLength;
		double a = Position;
		double b = Position * omega + Velocity;
		double c = Math.Exp(-omega * dt);
		Position = (a + b * dt) * c;
		Velocity = c * (-a * omega - b * dt * omega + b);

		if (Math.Abs(Position) < 0.01)
		{
			Reset();
			return false;
		}
		return true;
	}

	public void Reset()
	{
		Position = 0;
		Velocity = 0;
	}
}

/// <summary>
/// One corner of the animated cursor quad. Faithful port of the <c>Corner</c>
/// class from neovide-cursor.js.
/// </summary>
internal class Corner
{
	private readonly Point relativePosition;
	private readonly double animationLength;
	private readonly double shortAnimationLength;
	private readonly double trailSize;

	private readonly DampedSpringAnimation animationX = new DampedSpringAnimation();
	private readonly DampedSpringAnimation animationY = new DampedSpringAnimation();

	public Corner(Point relativePosition, double animationLength, double shortAnimationLength, double trailSize)
	{
		this.relativePosition = relativePosition;
		this.animationLength = animationLength;
		this.shortAnimationLength = shortAnimationLength;
		this.trailSize = trailSize;
		CurrentPosition = new Point(0, 0);
		PreviousDestination = new Point(-1000, -1000);
		CurrentAnimationLength = animationLength;
	}

	public Point CurrentPosition { get; set; }

	public Point PreviousDestination { get; set; }

	/// <summary>The per-corner animation length, assigned by <see cref="Jump"/>.</summary>
	public double CurrentAnimationLength { get; private set; }

	public DampedSpringAnimation AnimationX => animationX;

	public DampedSpringAnimation AnimationY => animationY;

	public Point GetDestination(Point center, Size cursorDimensions)
	{
		return new Point(
			center.X + relativePosition.X * cursorDimensions.Width,
			center.Y + relativePosition.Y * cursorDimensions.Height);
	}

	public double CalculateDirectionAlignment(Size cursorDimensions, Point destination)
	{
		Point relativeScaled = new Point(
			relativePosition.X * cursorDimensions.Width,
			relativePosition.Y * cursorDimensions.Height);
		Point cornerDestination = new Point(
			destination.X + relativeScaled.X,
			destination.Y + relativeScaled.Y);

		Vector travelDirection = Normalize(cornerDestination - CurrentPosition);
		Vector cornerDirection = Normalize(new Vector(relativePosition.X, relativePosition.Y));
		return travelDirection.X * cornerDirection.X + travelDirection.Y * cornerDirection.Y;
	}

	public void Jump(Point destination, Size cursorDimensions, int rank)
	{
		Point target = GetDestination(destination, cursorDimensions);
		Vector jumpVec = new Vector(
			(target.X - PreviousDestination.X) / cursorDimensions.Width,
			(target.Y - PreviousDestination.Y) / cursorDimensions.Height);

		// The JS's threshold (<=2.001 x caret width) is tuned for VSCode's ~8px
		// caret: a one-character move there is jumpVec.X = 8/8 = 1 -> short. With
		// VS's 2px caret the same move is jumpVec.X = 8/2 = 4 -> long, so every
		// typed character would take the trail animation and the trailing corners
		// would accumulate lag under fast typing. Judge in absolute pixels instead:
		// a same-line move up to ~2 characters is a quick "short" move; anything
		// else (line jumps, distant clicks) gets the rubber-band trail.
		double pixelX = Math.Abs(jumpVec.X) * cursorDimensions.Width;
		double pixelY = Math.Abs(jumpVec.Y) * cursorDimensions.Height;
		bool isShortJump = pixelX <= 16 && pixelY <= 0.5;

		if (isShortJump)
		{
			CurrentAnimationLength = Math.Min(animationLength, shortAnimationLength);
		}
		else
		{
			double leading = animationLength * Clamp(1 - trailSize, 0, 1);
			double trailing = animationLength;
			if (rank >= 2)
			{
				CurrentAnimationLength = leading;
			}
			else if (rank == 1)
			{
				CurrentAnimationLength = (leading + trailing) / 2;
			}
			else
			{
				CurrentAnimationLength = trailing;
			}
		}
		animationX.Reset();
		animationY.Reset();
	}

	/// <summary>
	/// Advances this corner's springs. Returns <c>true</c> while still animating,
	/// <c>false</c> once settled (or when <paramref name="immediate"/> is set).
	/// </summary>
	public bool Update(Size cursorDimensions, Point destination, double dt, bool immediate)
	{
		Point cornerDestination = GetDestination(destination, cursorDimensions);

		if (cornerDestination.X != PreviousDestination.X || cornerDestination.Y != PreviousDestination.Y)
		{
			Vector delta = cornerDestination - CurrentPosition;
			animationX.Position = delta.X;
			animationY.Position = delta.Y;
			PreviousDestination = cornerDestination;
		}

		if (immediate)
		{
			CurrentPosition = cornerDestination;
			animationX.Reset();
			animationY.Reset();
			return false;
		}

		bool animX = animationX.Update(dt, CurrentAnimationLength);
		bool animY = animationY.Update(dt, CurrentAnimationLength);

		CurrentPosition = new Point(
			cornerDestination.X - animationX.Position,
			cornerDestination.Y - animationY.Position);
		return animX || animY;
	}

	private static double Clamp(double val, double min, double max)
	{
		return Math.Min(Math.Max(val, min), max);
	}

	private static Vector Normalize(Vector vec)
	{
		double len = Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
		if (len == 0) return new Vector(0, 0);
		return new Vector(vec.X / len, vec.Y / len);
	}
}

/// <summary>
/// Ranks corners by direction alignment (port of computeCornerRanks from neovide-cursor.js).
/// The corner pointing against the direction of travel gets rank 0 (trailing / slowest),
/// the corner pointing along it gets the highest rank (leading / fastest).
/// </summary>
internal static class CornerRanks
{
	public static int[] Compute(Corner[] corners, Size cursorDimensions, Point destination)
	{
		int n = corners.Length;
		(int index, double value)[] aligned = new (int index, double value)[n];
		for (int i = 0; i < n; i++)
		{
			aligned[i] = (i, corners[i].CalculateDirectionAlignment(cursorDimensions, destination));
		}
		Array.Sort(aligned, (x, y) =>
		{
			if (x.value == y.value) return x.index - y.index;
			return x.value.CompareTo(y.value);
		});

		int[] ranks = new int[n];
		for (int rank = 0; rank < n; rank++)
		{
			ranks[aligned[rank].index] = rank;
		}
		return ranks;
	}
}
