// -----------------------------------------------------------------------------------------------
// Demo-Kontrakte, die im echten System NICHT von diesem Task erzeugt werden:
//   * das Transfer-Objekt des View-Knotens (DemoViewTO) — käme vom GUI-Generator,
//   * das Result-DTO des Sub-Tasks (ErrorBoxResult) — käme aus ErrorBox.nav,
//   * der Begin-Einstieg des referenzierten Sub-Tasks (IBeginErrorBoxWFS) — käme aus ErrorBox.nav.
//
// Für dieses isolierte Navigations-Demo werden sie hier von Hand bereitgestellt, damit der
// generierte V2-Code kompiliert. (Auch dies ist reine Kompilier-Attrappe.)
// -----------------------------------------------------------------------------------------------

using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;

namespace NavV2Demo.IWFL {

    // Transfer-Objekt des View-Knotens DemoView. (Im echten System vom GUI-Generator erzeugt.)
    public class DemoViewTO { }

    // Result-DTO des Sub-Tasks ErrorBox. (Im echten System aus ErrorBox.nav erzeugt.)
    public enum ErrorBoxResult {
        Ok,
        Abbrechen
    }
}

namespace NavV2Demo.Sub.WFL {

    // Begin-Einstieg des referenzierten Sub-Tasks (taskref ErrorBox). Auf genau diesen
    // IBeginErrorBoxWFS-Namen zeigt die <NavInitCall>-Annotation im generierten CallContext.
    public interface IBeginErrorBoxWFS {
        IINIT_TASK Begin(string message);
    }
}
