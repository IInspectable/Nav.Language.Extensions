#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    /// <summary>
    /// Ergebnis eines <see cref="Run"/>-Durchlaufs. Mangels echter Discriminated Unions als
    /// "geschlossenes" Result modelliert: <see cref="Success"/> trägt die Liste der erzeugten
    /// (bzw. inhaltsgleich übersprungenen) Dateien sowie die Menge der per <c>taskref</c>
    /// eingelesenen Abhängigkeitsdateien, <see cref="Failed"/> ist leer. Beide Listen sind
    /// damit nur im Erfolgsfall gefüllt.
    /// </summary>
    public sealed class RunResult {

        /// <summary>Das gemeinsame Fehlschlag-Ergebnis: <see cref="Succeeded"/> ist
        /// <see langword="false"/>, beide Dateilisten sind leer.</summary>
        public static readonly RunResult Failed = new(succeeded: false, ImmutableArray<FileGeneratorResult>.Empty, ImmutableArray<string>.Empty);

        /// <summary>Erzeugt ein Erfolgs-Ergebnis mit den erzeugten und den per <c>taskref</c>
        /// eingelesenen Abhängigkeitsdateien.</summary>
        /// <param name="generatedFiles">Die erzeugten (bzw. inhaltsgleich übersprungenen)
        /// Ausgabedateien.</param>
        /// <param name="includedFiles">Die per <c>taskref</c> eingelesenen Abhängigkeitsdateien.</param>
        /// <returns>Das Erfolgs-Ergebnis.</returns>
        public static RunResult Success(ImmutableArray<FileGeneratorResult> generatedFiles, ImmutableArray<string> includedFiles)
            => new(succeeded: true, generatedFiles, includedFiles);

        // ReSharper disable once ConvertToPrimaryConstructor
        RunResult(bool succeeded, ImmutableArray<FileGeneratorResult> generatedFiles, ImmutableArray<string> includedFiles) {
            Succeeded      = succeeded;
            GeneratedFiles = generatedFiles;
            IncludedFiles  = includedFiles;
        }

        /// <summary><see langword="true"/>, wenn der Durchlauf ohne Fehler durchlief.</summary>
        public bool                                Succeeded      { get; }
        /// <summary>Die erzeugten Ausgabedateien — inkl. der inhaltsgleich übersprungenen, deren
        /// <see cref="FileGeneratorResult.Action"/> das kennzeichnet. Nur im Erfolgsfall
        /// gefüllt.</summary>
        public ImmutableArray<FileGeneratorResult> GeneratedFiles { get; }

        /// <summary>
        /// Die per <c>taskref</c> eingelesenen Abhängigkeitsdateien (absolute Pfade) aller verarbeiteten
        /// Quelldateien. Sie sind selbst keine Eingabedateien des Durchlaufs, beeinflussen aber den erzeugten
        /// Code — der inkrementelle Build muss sie als zusätzliche Inputs tracken, um sie bei Änderung
        /// nicht fälschlich zu überspringen.
        /// </summary>
        public ImmutableArray<string> IncludedFiles { get; }

    }

}