namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Seite einer Kante, auf der eine Knoten-Referenz (<see cref="INodeReferenceSymbol"/>)
/// steht: als Quelle oder als Ziel der Transition.
/// </summary>
public enum NodeReferenceType {

    /// <summary>Die Referenz steht auf der Quellseite der Kante.</summary>
    Source,
    /// <summary>Die Referenz steht auf der Zielseite der Kante.</summary>
    Target

}

/// <summary>
/// Symbol einer Verwendung eines Knotens im Transitionsblock — z.B. stehen in
/// <c>Start --&gt; Auswahl;</c> zwei Knoten-Referenzen, links als Quelle, rechts als Ziel. Anders
/// als die Deklaration (<see cref="INodeSymbol"/>) steht eine Referenz für genau eine Fundstelle
/// an genau einer Kante (<see cref="Edge"/>); alle Referenzen eines Knotens sammelt
/// <see cref="INodeSymbol.References"/>.
/// </summary>
public interface INodeReferenceSymbol: ISymbol {

    /// <summary>
    /// Der deklarierte Knoten, auf den sich diese Referenz auflöst — <c>null</c>, wenn im
    /// umgebenden Task kein Knoten dieses Namens deklariert ist (Diagnose Nav0011).
    /// </summary>
    INodeSymbol? Declaration { get; }

    /// <summary>Ob diese Referenz auf der Quell- oder der Zielseite ihrer Kante steht.</summary>
    NodeReferenceType NodeReferenceType { get; }

    /// <summary>Die Kante (Transition), zu der diese Referenz gehört.</summary>
    IEdge Edge { get; }

}

/// <summary>
/// Die Zielreferenz eines <c>cancel</c>-Kantenziels (<c>… --&gt; cancel …</c>, ab Sprachversion 2).
/// Anders als alle anderen Knoten-Referenzen (siehe <see cref="INodeReferenceSymbol{T}"/>) trägt sie
/// <b>keinen</b> Typ-Parameter und löst sich <b>nie</b> auf: <c>cancel</c> hat per Design keine
/// Deklaration (es hat keinen Namen, keine Parameter, keine Identität — es ist nur ein Kantenziel), also
/// ist <see cref="INodeReferenceSymbol.Declaration"/> hier stets <c>null</c>. Weil es keinen Knoten gibt,
/// erscheint <c>cancel</c> nie als <see cref="Call"/>; der Cancel-Ausgang einer Quelle wird stattdessen
/// über diese Referenz erkannt (<see cref="EdgeExtensions.TargetsCancel"/>) — z.B. für das V2-Gating der
/// generierten <c>Cancel()</c>-Aufruffläche. Ein <c>cancel</c>-Ziel ist deshalb auch von
/// <see cref="SemanticAnalyzer.Nav0011CannotResolveNode0"/> ausgenommen — die fehlende Deklaration ist
/// hier gewollt, kein unauflösbarer Name.
/// </summary>
public interface ICancelNodeReferenceSymbol: INodeReferenceSymbol {

}
