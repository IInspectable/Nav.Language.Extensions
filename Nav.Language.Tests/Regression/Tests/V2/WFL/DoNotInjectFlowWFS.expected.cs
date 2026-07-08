#region Using Directives
using System;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Nav.Language.Tests.Regression.V2.DoNotInject.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.DoNotInject.WFL {
    public partial class DoNotInjectFlowWFS {
        protected override Init1CallContext.Result BeginLogic(Init1CallContext next) {
            throw new NotImplementedException();
        }

        protected override AfterEditCallContext.Result AfterEditLogic(EditorResult result,
                                                                      AfterEditCallContext next) {
            throw new NotImplementedException();
        }

        protected override OnEditCallContext.Result OnEditLogic(HomeTO to,
                                                                OnEditCallContext next) {
            throw new NotImplementedException();
        }

        protected override OnCloseCallContext.Result OnCloseLogic(HomeTO to,
                                                                  OnCloseCallContext next) {
            throw new NotImplementedException();
        }
    }
}