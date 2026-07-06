namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die <b>versionierbaren</b> Codegen-Fakten der geteilten Namensalgebra — Gegenstück zu den
/// generationsübergreifend fixen <see cref="CodeGenInvariants"/>. Anders als jene dürfen diese Werte
/// (Implementierungs-Klassen-/Namespace-Suffixe, Methoden-Namensbausteine) von Nav-Sprachversion zu
/// Nav-Sprachversion abweichen; deshalb sind sie <i>keine</i> Konstanten, sondern eine Instanz pro
/// <see cref="NavLanguageVersion"/>, die <see cref="NavCodeGenFacts.For(NavLanguageVersion)"/> liefert.
/// <para>
/// Bewusst <b>minimal</b>: hier stehen nur die Facts, welche die generationsübergreifend geteilte
/// <c>*CodeInfo</c>-/<c>PathProvider</c>-Namensfläche speisen (Nav→C#-Navigation, QuickInfo, Dateipfade).
/// Rein emitter-interne Werte — Default-Basistypen, Feld-/Parameternamen, die
/// <c>NavigationEngine</c>-Namespaces usw. — gehören <i>nicht</i> hierher: sie liest allein der
/// jeweilige V1-Emitter (<c>CodeModel</c>s/Templates), und eine andere Generation bringt dafür ihren
/// eigenen Emitter mit eigenem Vokabular mit. Ein geteiltes Interface würde jene Generation sonst in
/// das Vokabular von V1 zwingen.
/// </para>
/// </summary>
public interface ICodeGenFacts {

    // -- Methoden-Namensbausteine ------------------------------------------------------------------

    /// <summary>Präfix der generierten Begin-Methoden (<c>Begin</c> in <c>Begin{Node}</c>).</summary>
    string BeginMethodPrefix { get; }

    /// <summary>Präfix der generierten Exit-Methoden (<c>After</c> in <c>After{Node}</c>).</summary>
    string ExitMethodPrefix { get; }

    /// <summary>Suffix der abstrakten Logic-Methoden (<c>Logic</c> in <c>{…}Logic</c>).</summary>
    string LogicMethodSuffix { get; }

    // -- Klassen-/Namespace-Suffixe der Implementierung --------------------------------------------

    /// <summary>Suffix des Implementierungs-Klassennamens (<c>WFS</c> in <c>{Task}WFS</c>).</summary>
    string WfsClassSuffix { get; }

    /// <summary>Suffix der generierten Basisklasse (<c>WFSBase</c> in <c>{Task}WFSBase</c>).</summary>
    string WfsBaseClassSuffix { get; }

    /// <summary>Namespace-Suffix der Implementierungs-Ablage (<c>WFL</c> in <c>{ns}.WFL</c>).</summary>
    string WflNamespaceSuffix { get; }

}
