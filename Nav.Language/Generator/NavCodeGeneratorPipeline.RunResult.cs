#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    /// <summary>
    /// Ergebnis eines <see cref="Run"/>-Laufs. Mangels echter Discriminated Unions als
    /// "geschlossenes" Result modelliert: <see cref="Success"/> trägt die Liste der erzeugten
    /// (bzw. inhaltsgleich übersprungenen) Dateien, <see cref="Failed"/> ist leer. Die Liste ist
    /// damit nur im Erfolgsfall gefüllt.
    /// </summary>
    public sealed class RunResult {

        public static readonly RunResult Failed = new(succeeded: false, ImmutableArray<FileGeneratorResult>.Empty);

        public static RunResult Success(ImmutableArray<FileGeneratorResult> generatedFiles)
            => new(succeeded: true, generatedFiles);

        // ReSharper disable once ConvertToPrimaryConstructor
        RunResult(bool succeeded, ImmutableArray<FileGeneratorResult> generatedFiles) {
            Succeeded      = succeeded;
            GeneratedFiles = generatedFiles;
        }

        public bool                                Succeeded      { get; }
        public ImmutableArray<FileGeneratorResult> GeneratedFiles { get; }

    }

}
