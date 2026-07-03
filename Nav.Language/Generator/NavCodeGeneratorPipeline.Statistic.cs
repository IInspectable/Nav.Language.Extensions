#nullable enable

#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    sealed class Statistic {

        public int FileCount   { get; private set; }
        public int TaskCount   { get; private set; }
        public int FilesUpated { get; private set; }
        public int FilesSkiped { get; private set; }

        public void UpdatePerFile() {
            FileCount++;
        }

        public void UpdatePerTask(IImmutableList<FileGeneratorResult> fileGeneratorResults) {
            TaskCount++;

            foreach (var fileResult in fileGeneratorResults) {
                switch (fileResult.Action) {
                    case FileGeneratorAction.Skiped:
                        FilesSkiped++;
                        break;
                    case FileGeneratorAction.Updated:
                        FilesUpated++;
                        break;
                }
            }
        }

    }

}