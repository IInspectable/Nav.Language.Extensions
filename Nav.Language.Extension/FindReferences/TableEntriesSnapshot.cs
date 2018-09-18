﻿#region Using Directives

using System;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.FindReferences;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences {

    partial class TableEntriesSnapshot: WpfTableEntriesSnapshotBase {

        readonly FindReferencesContext          _context;
        readonly ImmutableArray<ReferenceEntry> _entries;

        public TableEntriesSnapshot(FindReferencesContext context, int versionNumber, ImmutableArray<ReferenceEntry> entries) {
            _context       = context;
            _entries       = entries;
            VersionNumber  = versionNumber;
            HighlightBrush = Presenter.HighlightBackgroundBrush;

        }

        Brush HighlightBrush { get; }

        public FindReferencesPresenter Presenter => _context.Presenter;

        public override int VersionNumber { get; }

        public override int Count => _entries.Length;

        public override int IndexOf(int currentIndex, ITableEntriesSnapshot newSnapshot) {
            // We only add items to the end of our list, and we never reorder.
            // As such, any index in us will map to the same index in any newer snapshot.
            return currentIndex;
        }

        public override bool TryGetValue(int index, string keyName, out object content) {
            content = GetValue(_entries[index], keyName);

            return content != null;
        }

        private object GetValue(ReferenceEntry entry, string keyName) {
            switch (keyName) {
                case StandardTableKeyNames2.Definition:
                    return new NavDefinitionBucket(Presenter, entry.Definition, _context.SourceTypeIdentifier, _context.Identifier);
                case StandardTableKeyNames2.DefinitionIcon:
                    return ImageMonikers.TaskDefinition;
                case StandardTableColumnDefinitions.DocumentName:
                    return entry.Location.FilePath;
                case StandardTableKeyNames.Line:
                    return entry.Location.StartLine;
                case StandardTableKeyNames.Column:
                    return entry.Location.StartCharacter;
                case StandardTableKeyNames.ProjectName:
                    return NavLanguagePackage.GetContainingProject(entry.Location.FilePath)?.Name ?? "Miscellaneous Files";
                case StandardTableKeyNames.ProjectGuid:
                    return Guid.NewGuid();
                case StandardTableKeyNames.Text:
                    return entry.LineText;
            }

            return null;
        }

        public override bool TryCreateColumnContent(int index, string columnName, bool singleColumnView, out FrameworkElement content) {

            if (columnName == StandardTableColumnDefinitions2.LineText) {

                var entry = _entries[index];

                content = LinePartsToTextBlock(entry);
                LazyTooltip.AttachTo(content, () => CreateToolTip(entry));
                
                return true;
            }

            return base.TryCreateColumnContent(index, columnName, singleColumnView, out content);

        }

        ToolTip CreateToolTip(ReferenceEntry entry) {

            return new ToolTip {
                Background = (Brush) Application.Current.Resources[EnvironmentColors.ToolWindowBackgroundBrushKey],
                Content    = PreviewPartsToTextBlock(entry)
            };
        }

        TextBlock LinePartsToTextBlock(ReferenceEntry entry) {

            return Presenter.ToTextBlock(entry.LineParts, (run, part, position) => {

                if (position       == entry.LineHighlightExtent.Start &&
                    HighlightBrush != null) {

                    run.SetValue(
                        TextElement.BackgroundProperty,
                        HighlightBrush);
                }

            });
        }

        TextBlock PreviewPartsToTextBlock(ReferenceEntry entry) {

            return Presenter.ToTextBlock(entry.PreviewParts, (run, part, position) => {

                if (position       == entry.PreviewHighlightExtent.Start &&
                    HighlightBrush != null) {

                    run.SetValue(
                        TextElement.BackgroundProperty,
                        HighlightBrush);
                }

            });
        }

    }

}