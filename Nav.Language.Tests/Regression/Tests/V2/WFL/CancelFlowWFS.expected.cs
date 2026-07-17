#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.Cancel.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.Cancel.WFL {
    public partial class CancelFlowWFS {
        protected override Init1CallContext.Result BeginLogic(Init1CallContext next) {
            throw new NotImplementedException();
        }

        protected override OnDecideCallContext.Result OnDecideLogic(HomeTO to,
                                                                    OnDecideCallContext next) {
            throw new NotImplementedException();
        }

        protected override OnEscapeCallContext.Result OnEscapeLogic(HomeTO to,
                                                                    OnEscapeCallContext next) {
            throw new NotImplementedException();
        }

        protected override Choice_ConfirmCallContext.Result Choice_ConfirmLogic(Choice_ConfirmCallContext next) {
            throw new NotImplementedException();
        }
    }
}