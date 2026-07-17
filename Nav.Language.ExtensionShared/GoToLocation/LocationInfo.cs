using Microsoft.VisualStudio.Imaging.Interop;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation; 

/// <summary>
/// Ein aufgelöstes (oder fehlgeschlagenes) Sprungziel für das „Go To…"-Menü: entweder eine gültige
/// <see cref="Location"/> nebst Anzeigename und Icon oder — bei Fehlschlag — eine Fehlermeldung. Instanzen
/// werden ausschließlich über die Fabrikmethoden <see cref="FromLocation"/> und <see cref="FromError(string)"/>
/// erzeugt.
/// </summary>
public struct LocationInfo {

    readonly string _errorMessage;
    readonly string _displayName;

    /// <summary>Gibt an, ob ein gültiges Sprungziel vorliegt (also eine <see cref="Location"/> gesetzt ist).</summary>
    public bool IsValid => Location != null;

    /// <summary>Das aufgelöste Sprungziel, oder <c>null</c> im Fehlerfall.</summary>
    public Location     Location     { get; private init; }
    /// <summary>Das im Auswahlmenü angezeigte Icon (VS-<see cref="ImageMoniker"/>).</summary>
    public ImageMoniker ImageMoniker { get; private init; }

    /// <summary>
    /// Der im Menü angezeigte Text. Fällt auf den Dateipfad der <see cref="Location"/> (bzw. leeren String)
    /// zurück, wenn kein expliziter Anzeigename gesetzt wurde.
    /// </summary>
    public string DisplayName {
        get {
            if(string.IsNullOrEmpty(_displayName)) {
                return Location?.FilePath ?? string.Empty;
            }
            return _displayName;
        }
        private init => _displayName = value;
    }

    /// <summary>Die Fehlermeldung im Fehlerfall; sonst der leere String.</summary>
    public string ErrorMessage {
        get => _errorMessage ??string.Empty;
        private init => _errorMessage = value;
    }

    /// <summary>Erzeugt ein ungültiges Sprungziel, das nur die Fehlermeldung <paramref name="errorMessage"/> trägt.</summary>
    public static LocationInfo FromError(string errorMessage) {
        return new LocationInfo {
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Erzeugt ein ungültiges Sprungziel aus einer <see cref="LocationNotFoundException"/>; die Meldung der
    /// Ausnahme wird angezeigt, das <paramref name="imageMoniker"/> als (ausgegrautes) Icon übernommen.
    /// </summary>
    public static LocationInfo FromError(LocationNotFoundException ex, ImageMoniker imageMoniker) {
        return new LocationInfo {
            ErrorMessage = ex?.Message,
            ImageMoniker = imageMoniker
        };
    }

    /// <summary>
    /// Erzeugt ein gültiges Sprungziel für <paramref name="location"/> mit dem Anzeigenamen
    /// <paramref name="displayName"/> und dem Icon <paramref name="imageMoniker"/>.
    /// </summary>
    public static LocationInfo FromLocation(Location location, string displayName, ImageMoniker imageMoniker) {
        return new LocationInfo {
            Location     = location,
            DisplayName  = displayName,
            ImageMoniker = imageMoniker,
        };
    }

        

}