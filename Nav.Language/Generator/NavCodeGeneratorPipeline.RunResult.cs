#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    /// <summary>
    /// Ergebnis eines <see cref="Run"/>-Laufs. Mangels echter Discriminated Unions als
    /// "geschlossenes" Result modelliert: <see cref="Success"/> trägt die Liste der erzeugten
    /// (bzw. inhaltsgleich übersprungenen) Dateien sowie die Menge der per <c>taskref</c>
    /// eingelesenen Abhängigkeitsdateien, <see cref="Failed"/> ist leer. Beide Listen sind
    /// damit nur im Erfolgsfall gefüllt.
    /// </summary>
    public sealed class RunResult {

        public static readonly RunResult Failed = new(succeeded: false, ImmutableArray<FileGeneratorResult>.Empty, ImmutableArray<string>.Empty);

        public static RunResult Success(ImmutableArray<FileGeneratorResult> generatedFiles, ImmutableArray<string> includedFiles)
            => new(succeeded: true, generatedFiles, includedFiles);

        // ReSharper disable once ConvertToPrimaryConstructor
        RunResult(bool succeeded, ImmutableArray<FileGeneratorResult> generatedFiles, ImmutableArray<string> includedFiles) {
            Succeeded      = succeeded;
            GeneratedFiles = generatedFiles;
            IncludedFiles  = includedFiles;
        }

        public bool                                Succeeded      { get; }
        public ImmutableArray<FileGeneratorResult> GeneratedFiles { get; }

        /// <summary>
        /// Die per <c>taskref</c> eingelesenen Abhängigkeitsdateien (absolute Pfade) aller verarbeiteten
        /// Quelldateien. Sie sind selbst keine Eingabedateien des Laufs, beeinflussen aber den erzeugten
        /// Code — der inkrementelle Build muss sie als zusätzliche Inputs tracken, um sie bei Änderung
        /// nicht fälschlich zu überspringen.
        /// </summary>
        public ImmutableArray<string> IncludedFiles { get; }

    }

}