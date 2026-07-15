#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    /// <summary>
    /// Zählwerk eines <see cref="Run"/>-Durchlaufs: verarbeitete <c>.nav</c>-Dateien und Task-Definitionen
    /// sowie geschriebene bzw. übersprungene Ausgabedateien. Die Werte fließen am Laufende in die
    /// Abschluss-Statistik (siehe <c>LoggerAdapter.LogProcessEnd</c>).
    /// </summary>
    sealed class Statistic {

        /// <summary>Anzahl der verarbeiteten <c>.nav</c>-Eingabedateien.</summary>
        public int FileCount   { get; private set; }
        /// <summary>Anzahl der verarbeiteten Task-Definitionen (je erzeugte Ausgabe-Gruppe).</summary>
        public int TaskCount   { get; private set; }
        /// <summary>Anzahl der tatsächlich geschriebenen Ausgabedateien
        /// (<see cref="FileGeneratorAction.Updated"/>).</summary>
        public int FilesUpated { get; private set; }
        /// <summary>Anzahl der inhaltsgleich übersprungenen Ausgabedateien
        /// (<see cref="FileGeneratorAction.Skiped"/>).</summary>
        public int FilesSkiped { get; private set; }

        /// <summary>Zählt eine weitere verarbeitete Eingabedatei (<see cref="FileCount"/>).</summary>
        public void UpdatePerFile() {
            FileCount++;
        }

        /// <summary>
        /// Zählt eine weitere Task-Definition (<see cref="TaskCount"/>) und bucht deren Ausgabedateien
        /// je nach <see cref="FileGeneratorResult.Action"/> auf <see cref="FilesUpated"/> bzw.
        /// <see cref="FilesSkiped"/>.
        /// </summary>
        /// <param name="fileGeneratorResults">Die Ausgabeergebnisse der Task-Definition.</param>
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