#region Using Directives

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

class IntraTextGoToTag : GoToTag {

    public IntraTextGoToTag(ILocationInfoProvider provider, ImageMoniker imageMoniker, string toolTip) 
        : base(provider) {
        ImageMoniker = imageMoniker;
        ToolTip      = toolTip;
    }

    public ImageMoniker ImageMoniker { get;}
    public object       ToolTip      { get; }
}