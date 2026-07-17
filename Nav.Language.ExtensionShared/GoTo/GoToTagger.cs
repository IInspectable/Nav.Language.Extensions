#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der <see cref="ITagger{T}"/> von <see cref="GoToTag"/>: markiert im Nav-Editor alle Symbole mit einem
/// Sprungziel. Für jedes Symbol im angefragten Bereich lässt er vom <see cref="GoToSymbolBuilder"/> — sofern
/// navigierbar — einen <see cref="GoToTag"/> bauen, der die zugehörigen Sprungziel-Provider trägt. Über
/// <see cref="SemanticModelServiceDependent"/> hängt er am aktuellen Semantikmodell und meldet dessen
/// Änderungen als <see cref="TagsChanged"/> weiter. Die Tags konsumiert der <see cref="GoToMouseProcessor"/>
/// beim Ctrl-Klick.
/// </summary>
sealed class GoToTagger : SemanticModelServiceDependent, ITagger<GoToTag> {
        
    GoToTagger(ITextBuffer textBuffer) : base(textBuffer) {

    }

    /// <summary>Erzeugt den Tagger für <paramref name="textBuffer"/> (Fabrikmethode für den Provider).</summary>
    public static ITagger<T> Create<T>(ITextBuffer textBuffer) where T : ITag {
        return new GoToTagger(textBuffer) as ITagger<T>;
    }

    /// <summary><see cref="ITagger{T}"/>-Ereignis: signalisiert dem Editor, dass Tags neu abzufragen sind.</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>Reicht eine Semantikmodell-Änderung als <see cref="TagsChanged"/> an den Editor weiter.</summary>
    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs snapshotSpanEventArgs) {
        TagsChanged?.Invoke(this, snapshotSpanEventArgs);
    }

    /// <summary>
    /// Liefert für die angefragten <paramref name="spans"/> je navigierbarem Symbol einen
    /// <see cref="GoToTag"/>. Die Symbole werden aus dem aktuellen Semantikmodell über den überlappenden
    /// Textbereich ermittelt; liegt kein Semantikmodell vor, bleibt das Ergebnis leer.
    /// </summary>
    public IEnumerable<ITagSpan<GoToTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if (codeGenerationUnitAndSnapshot == null) {
            yield break;
        }
            
        foreach (var span in spans) {
                
            var extent  = TextExtent.FromBounds(span.Start, span.End);
            var symbols = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Symbols[extent, includeOverlapping: true];

            foreach (var symbol in symbols) {

                var goToTag = GoToSymbolBuilder.Build(codeGenerationUnitAndSnapshot, symbol, TextBuffer);
                if(goToTag != null) {
                    yield return goToTag;
                }
            }
        }
    }
}
