﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#region Using Directives
using Nav.Language.Tests.Regression.Test1.IWFL;
using Pharmatechnik.Apotheke.XTplus.Common.WFL;
using Pharmatechnik.Apotheke.XTplus.Common.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.Test1.WFL {

    // Redeklarationen von Methoden ohne new sind ok - um in manuell erstellten Oberinterfaces Begins definieren zu können
    #pragma warning disable 0108

    #region Nav Annotations
    /// <NavFile>..\..\ConcatSample.nav</NavFile>
    /// <NavTask>ConcatSample</NavTask>
    #endregion
    public interface IBeginConcatSampleWFS: IBeginWFService {
        #region Nav Annotations
        /// <NavInit>Init1</NavInit>
        #endregion
        IINIT_TASK Begin();
        #region Nav Annotations
        /// <NavInit>Init2</NavInit>
        #endregion
        IINIT_TASK Begin(string message);
    }
}