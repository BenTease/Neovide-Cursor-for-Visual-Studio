using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NeovideCursor;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Neovide Cursor", "Neovide-style smooth cursor animation for the Visual Studio text editor.", "1.0")]
// Load the package at startup (mirrors the installed Smooth Caret pkgdef: the same
// two UI contexts, background load). Without this, the package is only loaded on a
// service query, which fails before the managed global service provider is up — the
// first text view then finds the controller uninitialized.
[ProvideAutoLoad("adfc4e64-0397-11d1-9f4e-00a0c911004f", PackageAutoLoadFlags.BackgroundLoad)] // SolutionBuilding
[ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82", PackageAutoLoadFlags.BackgroundLoad)] // SolutionExists
[ProvideService(typeof(ServiceForPackageInitialization))]
[ProvideOptionPage(typeof(NeovideCursorOptionPage), "Neovide Cursor", "General", 0, 0, true)]
[Guid("4F43B2D1-0C53-4E4B-9A2D-7C1B5E8F2A91")]
public sealed class NeovideCursorPackage : AsyncPackage
{
	private static readonly object SyncRoot = new object();

	private static Controller controller;

	protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
	{
		await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		try
		{
			// Warm-up path: if the package loads before any text view opens, build
			// the controller from the package's service provider. GetController is
			// the authoritative source, so this is purely an early construction.
			lock (SyncRoot)
			{
				controller ??= new Controller(this);
			}
		}
		catch (Exception e)
		{
			ExceptionHandler.UnhandledException(e);
		}
	}

	public static VSServices GetVS()
	{
		return GetController().GetVS();
	}

	public static Options GetOptions()
	{
		return GetController().GetOptions();
	}

	/// <summary>
	/// Returns the controller, creating it on first use. An AsyncPackage's
	/// InitializeAsync runs in the background, so the first text view can be
	/// created before the package finishes loading — the editor integration must
	/// not depend on that timing. Building the controller from the global service
	/// provider makes GetVS/GetOptions safe on the very first view.
	/// </summary>
	private static Controller GetController()
	{
		if (controller == null)
		{
			lock (SyncRoot)
			{
				if (controller == null)
				{
					IServiceProvider global = Package.GetGlobalService(typeof(SVsServiceProvider)) as IServiceProvider;
					if (global != null)
					{
						controller = new Controller(global);
					}
					else
					{
						// Global provider unavailable — force the package to load so
						// its InitializeAsync populates controller.
						Package.GetGlobalService(typeof(ServiceForPackageInitialization));
					}
				}
			}
		}

		if (controller == null)
		{
			throw new InvalidOperationException("Neovide Cursor could not obtain the Visual Studio service provider.");
		}
		return controller;
	}
}
