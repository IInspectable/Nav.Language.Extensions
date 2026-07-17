#region Using Directives

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

/// <summary>
/// Ein GoTo-Datentag im generierten C#-Code: markiert die Stelle eines Nav-Bezeichners (Task, Init,
/// Exit, Trigger, Choice …) und trägt zusätzlich zum geerbten <see cref="ILocationInfoProvider"/> das
/// anzuzeigende Icon (<see cref="ImageMoniker"/>) sowie den Tooltip des daraus gebauten
/// <see cref="IntraTextGoToAdornment"/>. Erzeugt vom <see cref="IntraTextGoToTagSpanBuilder"/>.
/// </summary>
class IntraTextGoToTag : GoToTag {

    public IntraTextGoToTag(ILocationInfoProvider provider, ImageMoniker imageMoniker, string toolTip) 
        : base(provider) {
        ImageMoniker = imageMoniker;
        ToolTip      = toolTip;
    }

    /// <summary>Icon des GoTo-Symbols (z.B. „Gehe zu Definition"/„Gehe zu Deklaration").</summary>
    public ImageMoniker ImageMoniker { get;}
    /// <summary>Tooltip des Symbols (beschreibt das Sprungziel, z.B. „Go To Task Definition").</summary>
    public object       ToolTip      { get; }
}