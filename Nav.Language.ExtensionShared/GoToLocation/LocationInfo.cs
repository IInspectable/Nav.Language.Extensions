using Microsoft.VisualStudio.Imaging.Interop;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation; 

public struct LocationInfo {

    readonly string _errorMessage;
    readonly string _displayName;

    public bool IsValid => Location != null;

    public Location     Location     { get; private init; }
    public ImageMoniker ImageMoniker { get; private init; }

    public string DisplayName {
        get {
            if(string.IsNullOrEmpty(_displayName)) {
                return Location?.FilePath ?? string.Empty;
            }
            return _displayName;
        }
        private init => _displayName = value;
    }

    public string ErrorMessage {
        get => _errorMessage ??string.Empty;
        private init => _errorMessage = value;
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