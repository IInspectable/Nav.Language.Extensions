using Microsoft.VisualStudio.Imaging.Interop;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation {

    public struct LocationInfo {

        string _errorMessage;
        string _displayName;

        public bool IsValid {
            get { return Location != null; }
        }

        public Location Location { get; private set; }
        public ImageMoniker ImageMoniker { get; private set; }

        public string DisplayName {
            get {
                if(string.IsNullOrEmpty(_displayName)) {
                    return Location?.FilePath ?? string.Empty;
                }
                return _displayName;
            }
            private set { _displayName = value; }
        }

        public string ErrorMessage {
            get { return _errorMessage??string.Empty; }
            private set { _errorMessage = value; }
        }

        public static LocationInfo FromError(string errorMessage) {
            return new LocationInfo {
                ErrorMessage = errorMessage
            };
        }

        public static LocationInfo FromError(LocationNotFoundException ex, ImageMoniker imageMoniker) {
            return new LocationInfo {
                ErrorMessage = ex?.Message,
                ImageMoniker = imageMoniker
            };
        }

        public static LocationInfo FromLocation(Location location, string displayName, ImageMoniker imageMoniker) {
            return new LocationInfo {
                Location     = location,
                DisplayName  = displayName,
                ImageMoniker = imageMoniker,
            };
        }

        

    }
}