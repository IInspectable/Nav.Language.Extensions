#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.Basic.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.Basic.WFL {
    public partial class BasicFlowWFS {
        protected override Init1CallContext.Result BeginLogic(string message,
                                                              Init1CallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterGotoSubCallContext.Result AfterGotoSubLogic(SubResult result,
                                                                            AfterGotoSubCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterModalSubCallContext.Result AfterModalSubLogic(SubResult result,
                                                                              AfterModalSubCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterNonModalSubCallContext.Result AfterNonModalSubLogic(SubResult result,
                                                                                    AfterNonModalSubCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnEndCallContext.Result OnEndLogic(HomeTO to,
                                                              OnEndCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnGotoCallContext.Result OnGotoLogic(HomeTO to,
                                                                OnGotoCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnCloseCallContext.Result OnCloseLogic(HomeTO to,
                                                                  OnCloseCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnModalCallContext.Result OnModalLogic(HomeTO to,
                                                                  OnModalCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnReloadCallContext.Result OnReloadLogic(HomeTO to,
                                                                    OnReloadCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnNonModalCallContext.Result OnNonModalLogic(HomeTO to,
                                                                        OnNonModalCallContext callContext) {
            throw new NotImplementedException();
        }
    }
}