// -----------------------------------------------------------------------------------------------
// Demo-Kontrakte, die im echten System NICHT von diesem Task erzeugt werden:
//   * die Transfer-Objekte / Result-DTOs (HomeTO, MsgResult, DetailResult) — kaemen vom GUI- bzw.
//     von den Sub-Task-Generatoren,
//   * die Begin-Einstiege der referenzierten Sub-Tasks (IBeginMsgWFS/IBeginDetailWFS) — kaemen aus
//     Msg.nav bzw. Detail.nav.
//
// Fuer dieses isolierte Navigations-Demo werden sie hier von Hand bereitgestellt, damit der
// generierte V2-Code kompiliert. (Auch dies ist reine Kompilier-Attrappe.)
// -----------------------------------------------------------------------------------------------

using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;

namespace NavV2Demo.IWFL {

    // Transfer-Objekt des View-Knotens Home. (Im echten System vom GUI-Generator erzeugt.)
    public class HomeTO { }

    // Result-DTOs der Sub-Tasks. (Im echten System aus Msg.nav / Detail.nav erzeugt.)
    public class MsgResult { }
    public class DetailResult { }
}

namespace NavV2Demo.Sub.WFL {

    // Begin-Einstiege der referenzierten Sub-Tasks (taskref Msg / Detail). Auf genau diese
    // IBegin…WFS-Namen zeigt die <NavInitCall>-Annotation im generierten CallContext.
    public interface IBeginMsgWFS {
        IINIT_TASK Begin(string text);
    }

    public interface IBeginDetailWFS {
        IINIT_TASK Begin(int id);
    }
}
