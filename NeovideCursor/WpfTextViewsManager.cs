using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace NeovideCursor;

/// <summary>
/// Creates an <see cref="Adornment"/> for every document text view. Mirrors
/// Smooth Caret's WpfTextViewsManager. The global service provider comes from
/// the editor MEF composition (see <see cref="ServiceProvider"/>), so the
/// adornment never depends on when the AsyncPackage finishes loading — its
/// InitializeAsync runs in the background and can race the first text view.
/// </summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("text")]
[TextViewRole("DOCUMENT")]
internal sealed class WpfTextViewsManager : IWpfTextViewCreationListener
{
	// The adornment layer definition is imported by MEF, not assigned in code.
#pragma warning disable 0649
	[Export(typeof(AdornmentLayerDefinition))]
	[Name("NeovideCursorLayer")]
	[Order(After = "Caret")]
	internal AdornmentLayerDefinition neovideCursorAdornmentLayer;

	// MEF provides the global service provider directly (the standard way MEF
	// editor extensions reach VS services without a package).
	[Import(typeof(SVsServiceProvider))]
	internal IServiceProvider ServiceProvider;
#pragma warning restore 0649

	static WpfTextViewsManager()
	{
		// Best-effort warm-up only: loading the package early (via the service it
		// provides) keeps the InstalledProductRegistration populated and pre-reads
		// the registry options. MEF already supplies the service provider, so a
		// failure here must never break the type.
		try
		{
			Package.GetGlobalService(typeof(ServiceForPackageInitialization));
		}
		catch (Exception)
		{
		}
	}

	public void TextViewCreated(IWpfTextView textView)
	{
		try
		{
			Diagnostics.Log("TextViewCreated fired, ServiceProvider null=" + (ServiceProvider == null) + ", viewport=" + textView.ViewportWidth + "x" + textView.ViewportHeight);
			new Adornment(textView, ServiceProvider);
			Diagnostics.Log("Adornment created OK");
		}
		catch (Exception e)
		{
			Diagnostics.Log("TextViewCreated EXCEPTION: " + e);
			ExceptionHandler.UnhandledException(e);
		}
	}
}
