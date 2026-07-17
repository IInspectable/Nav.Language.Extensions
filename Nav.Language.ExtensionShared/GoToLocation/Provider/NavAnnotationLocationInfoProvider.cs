#region Using Directives

using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Basis der Provider, die aus einer Nav-Annotation im generierten C#-Code zurück in die zugehörige
/// <c>.nav</c>-Quelle springen (Richtung C#→Nav). Sie beschafft den Quelltext der von der Annotation
/// benannten Nav-Datei — bevorzugt aus einem offenen <c>ITextBuffer</c>, sonst per Dateizugriff — und
/// überlässt das eigentliche Auffinden der Nav-Location der abgeleiteten Klasse. Über die Roslyn-Brücke
/// löst diese es typischerweise mit
/// <see cref="Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols.LocationFinder"/> auf.
/// </summary>
/// <typeparam name="TAnnotation">Der konkrete Annotationstyp (Ableitung von <c>NavTaskAnnotation</c>).</typeparam>
abstract class NavAnnotationLocationInfoProvider<TAnnotation> : LocationInfoProvider 
    where TAnnotation: NavTaskAnnotation {

    static readonly Logger Logger = Logger.Create<NavAnnotationLocationInfoProvider<TAnnotation>>();

    /// <summary>Bindet den Provider an die auszuwertende <paramref name="annotation"/>.</summary>
    protected NavAnnotationLocationInfoProvider(TAnnotation annotation) {
        Annotation = annotation ?? throw new ArgumentNullException(nameof(annotation));
    }
        
    /// <summary>Die Nav-Annotation, deren Sprungziel in der Nav-Quelle gesucht wird.</summary>
    public TAnnotation Annotation { get; set; }

    /// <summary>
    /// Beschafft den Nav-Quelltext (offener Buffer oder Datei) und delegiert an
    /// <see cref="GetLocationsAsync(string, CancellationToken)"/>. Schlägt der Dateizugriff mit einem
    /// erwarteten IO-/Zugriffsfehler fehl, wird ein einzelnes Fehler-Sprungziel geliefert.
    /// </summary>
    public sealed override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = default) {

        string sourceText;
        var    textBuffer = NavLanguagePackage.GetOpenTextBufferForFile(Annotation.NavFileName);
        if (textBuffer != null) {
            sourceText = textBuffer.CurrentSnapshot.GetText();
        } else {
            try {
                // TODO true sync read!
                sourceText = await Task.Run(() => File.ReadAllText(Annotation.NavFileName), cancellationToken).ConfigureAwait(false);
            } catch(Exception ex) when(
                ex is FileNotFoundException       ||
                ex is IOException                 ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException) {
                // TODO evtl. detaliertere Fehlermeldungen
                return ToEnumerable(LocationInfo.FromError($"File '{Annotation.NavFileName}' not found"));
            } catch(Exception ex) {
                Logger.Error(ex, "File.ReadAllText failed.");
                throw;
            }
        }

        return await GetLocationsAsync(sourceText, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sucht die Nav-Location(s) im bereits beschafften <paramref name="sourceText"/> der Nav-Datei. Die
    /// Ableitung bestimmt anhand des Annotationstyps, welches Symbol (Task, Init, Exit, Trigger, Choice)
    /// angesprungen wird, und mit welchem Icon/Anzeigenamen.
    /// </summary>
    protected abstract Task<IEnumerable<LocationInfo>> GetLocationsAsync(string sourceText, CancellationToken cancellationToken = default);
}