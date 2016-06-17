#region Using Directives

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation {

    static class GoToImageMonikers {

        public static ImageMoniker GoToBeginLogic {
            get { return KnownMonikers.GoToDefinition; }
        }

        public static ImageMoniker GoToWfs {
            get { return KnownMonikers.GoToDefinition; }
        }

        public static ImageMoniker GoToTriggerDefinition {
            get { return KnownMonikers.GoToDeclaration; }
        }

        public static ImageMoniker GoToTaskDefinition {
            get { return KnownMonikers.GoToDeclaration; }
        }
    }
}