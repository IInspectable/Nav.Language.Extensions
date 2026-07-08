#region Using Directives

using System.Collections.Immutable;

using Microsoft.VisualStudio.Shell;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.Extension.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.NavigationBar;

class NavigationBarProjectItemBuilder {

    public static ImmutableList<NavigationBarItem> Build(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        // GetContainingProject muss auf dem Main Thread aufgerufen werden.
        ThreadHelper.ThrowIfNotOnUIThread();

        if (codeGenerationUnitAndSnapshot == null) {
            return ImmutableList<NavigationBarItem>.Empty;
        }

        return new[] {
            new NavigationBarItem(
                displayName: codeGenerationUnitAndSnapshot.Snapshot.TextBuffer.GetContainingProject()?.Name ?? ProjectMapper.MiscellaneousFiles,
                imageMoniker: ImageMonikers.ProjectNode)
        }.ToImmutableList();
    }

}