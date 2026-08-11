namespace NeovideCursor;

/// <summary>
/// Marker service that triggers package load on demand: the first time a text view
/// is created, WpfTextViewsManager's static constructor queries this service via
/// Package.GetGlobalService, which loads the package before the adornment runs.
/// </summary>
internal interface ServiceForPackageInitialization
{
}
