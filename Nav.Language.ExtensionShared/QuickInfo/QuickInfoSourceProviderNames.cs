
namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// Die MEF-Namen (<c>[Name]</c>) der QuickInfo-Provider — als Konstanten, damit die <c>[Order]</c>-Verweise
/// (relativ zum Standard-Presenter von VS) ohne Zeichenketten-Literale auskommen.
/// </summary>
static class QuickInfoSourceProviderNames {

    /// <summary>Name des von VS gestellten Standard-QuickInfo-Presenters (Anker für <c>[Order]</c>).</summary>
    public const string DefaultQuickInfoPresenter         = "Default Quick Info Presenter";
    /// <summary>MEF-Name des <see cref="SymbolQuickInfoSourceProvider"/>.</summary>
    public const string SymbolQuickInfoSourceProvider     = nameof(SymbolQuickInfoSourceProvider);
    /// <summary>MEF-Name des Diagnostics-QuickInfo-Providers.</summary>
    public const string DiagnosticQuickInfoSourceProvider = nameof(DiagnosticQuickInfoSourceProvider);
    /// <summary>MEF-Name des <see cref="DebugQuickInfoSourceProvider"/>.</summary>
    public const string DebugQuickInfoSourceProvider      = nameof(DebugQuickInfoSourceProvider);

}