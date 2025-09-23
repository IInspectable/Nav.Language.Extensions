#region Using Directives
using System;
using Nav.Language.Tests.Regression.Test1.IWFL;
using Pharmatechnik.Apotheke.XTplus.Common.WFL;
using Pharmatechnik.Apotheke.XTplus.Common.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.Test1.WFL {	
    public partial class TestWFS {
        protected override INavCommandBody BeginLogic(TestInitParams p1,
                                                      int? nullableParam,
                                                      NS.2.WFL.IBeginMessageboxOkWFS messageboxOk) {
            throw new NotImplementedException();
        }

        public override IINIT_TASK Begin() {
            throw new NotImplementedException();
        }

        protected override INavCommandBody AfterMsgExitLogic(MessageboxOkResult result) {
             throw new NotImplementedException();
        }

        protected override INavCommandBody AfterNoResultsLogic(MessageboxOkResult result) {
             throw new NotImplementedException();
        }

        protected override INavCommandBody AfterMsgNonModalLogic(MessageboxOkResult result) {
             throw new NotImplementedException();
        }

        protected override INavCommandBody AfterMsgContinueLogic(MessageboxOkResult result) {
             throw new NotImplementedException();
        }

        protected override INavCommand AfterMsgAbstract(MessageboxOkResult result) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody AfterDoSomethingLogic(bool result) {
             throw new NotImplementedException();
        }

        protected override INavCommandBody OnEndLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnContinueLogic(ViewTO to,
                                                           NS.2.WFL.IBeginMessageboxConinueWFS messageboxConinue) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnExitClickLogic(ViewTO to,
                                                            NS.2.WFL.IBeginMessageboxOkWFS messageboxOk) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnDoSomethingLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnMsgAbstractLogic(ViewTO to,
                                                              NS.2.WFL.IBeginMessageboxConinueWFS messageboxConinue) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnMsgNonModalLogic(ViewTO to,
                                                              NS.2.WFL.IBeginMessageboxConinueWFS messageboxConinue) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnReloadClickLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnShowMeModalLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnShowMeNonModalLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnNonNotImplementedLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnGoToNotImplementedLogic(ViewTO to) {
            throw new NotImplementedException();
        }

        protected override INavCommandBody OnModalNotImplementedLogic(ViewTO to) {
            throw new NotImplementedException();
        }
    }
}