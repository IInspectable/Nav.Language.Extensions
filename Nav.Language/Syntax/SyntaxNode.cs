#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Knoten des Nav-Syntaxbaums. Jeder Knoten deckt einen zusammenhängenden
/// <see cref="Extent"/> im Quelltext ab und ist Teil eines unveränderlichen Baums: Er kennt seinen
/// <see cref="Parent"/>, seine <see cref="ChildNodes"/> und die ihm direkt zugeordneten
/// <see cref="ChildTokens"/>. Der Baum wird in zwei Phasen aufgebaut — erst die Knoten samt Kindern, dann
/// das einmalige <see cref="FinalConstruct"/>, das <see cref="SyntaxTree"/> und <see cref="Parent"/>
/// nachträgt und den Knoten einfriert. Danach sind alle Zugriffe lesend.
/// </summary>
[Serializable]
[DebuggerDisplay("{" + nameof(ToDebuggerDisplayString) + "(), nq}")]
public abstract partial class SyntaxNode: IExtent {

    List<SyntaxNode> _childNodes;
    SyntaxTree       _syntaxTree;
    SyntaxNode       _parent;

    /// <summary>
    /// Erzeugt einen Knoten mit seinem Quelltext-<paramref name="extent"/>. Der Knoten ist danach noch im
    /// Aufbau-Modus (kein <see cref="SyntaxTree"/>, kein <see cref="Parent"/>) — beides setzt erst
    /// <see cref="FinalConstruct"/>.
    /// </summary>
    internal SyntaxNode(TextExtent extent) {
        Extent = extent;
    }

    /// <summary>
    /// Schließt den Aufbau des Knotens ab: trägt <paramref name="syntaxTree"/> und <paramref name="parent"/>
    /// nach und ruft sich rekursiv für alle Kindknoten auf. Danach ist der Knoten eingefroren und nur noch
    /// lesend benutzbar. Darf je Knoten genau einmal aufgerufen werden.
    /// </summary>
    internal void FinalConstruct(SyntaxTree syntaxTree, SyntaxNode parent) {

        EnsureConstructionMode();

        _syntaxTree = syntaxTree;
        _parent     = parent;

        if (_childNodes == null) {
            return;
        }

        foreach (var child in _childNodes) {
            child.FinalConstruct(syntaxTree, this);
        }
    }

    /// <summary>Die Startposition dieses Knotens im Quelltext (inklusiv).</summary>
    public int Start  => Extent.Start;

    /// <summary>Die Endposition dieses Knotens im Quelltext (exklusiv).</summary>
    public int End    => Extent.End;

    /// <summary>Die Länge dieses Knotens in Zeichen.</summary>
    public int Length => Extent.Length;

    /// <summary>Der Quelltext-Ausschnitt, den dieser Knoten abdeckt (ohne umgebende Trivia).</summary>
    public TextExtent Extent { get; }

    /// <summary>
    /// Der übergeordnete Knoten, oder <c>null</c> für die Wurzel des Baums. Erst nach
    /// <see cref="FinalConstruct"/> verfügbar.
    /// </summary>
    [CanBeNull]
    public SyntaxNode Parent {
        get {
            EnsureConstructed();
            return _parent;
        }
    }

    /// <summary>Die <see cref="Location"/> (Datei + Zeilen-/Spaltenbereich) dieses Knotens.</summary>
    public Location GetLocation() {
        EnsureConstructed();
        return SyntaxTree.SourceText.GetLocation(Extent);
    }

    /// <summary>
    /// Die diesem Knoten <b>direkt</b> zugeordneten Token (die der Parser hier konsumiert und angehängt hat) —
    /// in Quelltext-Reihenfolge. Token tiefer liegender Kindknoten gehören nicht dazu; Trivia hängt am
    /// Wurzelknoten und erscheint hier nur für die Wurzel.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxToken> ChildTokens() {
        return SyntaxTree.Tokens[Extent].Where(token => token.Parent == this);
    }

    static readonly IReadOnlyList<SyntaxNode> EmptyNodeList = new List<SyntaxNode>();

    /// <summary>Die unmittelbaren Kindknoten dieses Knotens (eine Ebene tief), in Quelltext-Reihenfolge.</summary>
    [NotNull]
    public IReadOnlyList<SyntaxNode> ChildNodes() {
        EnsureConstructed();
        return _childNodes ?? EmptyNodeList;
    }

    /// <summary>
    /// Alle Nachfahren dieses Knotens (rekursiv, ohne den Knoten selbst) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodes() {
        return DescendantNodes<SyntaxNode>();
    }

    /// <summary>
    /// Dieser Knoten und alle seine Nachfahren (rekursiv) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf() {
        return DescendantNodesAndSelf<SyntaxNode>();
    }

    /// <summary>
    /// Alle Nachfahren vom Typ <typeparamref name="T"/> (rekursiv, ohne den Knoten selbst) in
    /// Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<T> DescendantNodes<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: false);
    }

    /// <summary>
    /// Dieser Knoten (sofern vom Typ <typeparamref name="T"/>) und alle seine Nachfahren vom Typ
    /// <typeparamref name="T"/> (rekursiv) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<T> DescendantNodesAndSelf<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: true);
    }

    /// <summary>
    /// Gemeinsame Implementierung der Nachfahren-Durchläufe: liefert optional zuerst den Knoten selbst
    /// (sofern vom Typ <typeparamref name="T"/>) und steigt dann in die Kindknoten ab. Kann über
    /// <see cref="PromiseNoDescendantNodeOfSameType"/> den Abstieg vorzeitig abbrechen.
    /// </summary>
    [NotNull]
    IEnumerable<T> DescendantNodesAndSelfImpl<T>(bool includeSelf) where T : SyntaxNode {
        EnsureConstructed();
        if (includeSelf && this is T) {
            yield return (T) this;

            if (typeof(T) == GetType() && PromiseNoDescendantNodeOfSameType) {
                yield break;
            }
        }

        foreach (var node in ChildNodes().SelectMany(child => child.DescendantNodesAndSelf<T>())) {
            yield return node;
        }
    }

    /// <summary>
    /// Für Knoten, die sehr weit "oben" liegen, kann die Implementierung von DescendantNodes&lt;T&gt;
    /// massiv beschleunigt werden, wenn sichergestellt werden kann, dass ein Knoten keine untergeordneten
    /// Knoten vom selben Typ haben kann, und deshalb die Suche in den Kindknoten vorzeitig abgebrochen
    /// werden kann.
    /// Eigentlich müsste diese Eigenschaft systematisch überschrieben werden. Bisweilen werden hiermit nur
    /// die Hotspots optimiert.
    /// </summary>
    private protected virtual bool PromiseNoDescendantNodeOfSameType => false;

    /// <summary>
    /// Die Vorfahren dieses Knotens — vom <see cref="Parent"/> aufwärts bis zur Wurzel.
    /// </summary>
    public IEnumerable<SyntaxNode> Ancestors() {
        return Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>();
    }

    /// <summary>
    /// Dieser Knoten und seine Vorfahren — von ihm selbst aufwärts bis zur Wurzel.
    /// </summary>
    public IEnumerable<SyntaxNode> AncestorsAndSelf() {
        for (var node = this; node != null; node = node.Parent) {
            yield return node;
        }
    }

    /// <summary>
    /// Das Token an der angegebenen <paramref name="position"/> im gesamten Baum, oder
    /// <see cref="SyntaxToken.Missing"/>, wenn dort keines liegt.
    /// </summary>
    public SyntaxToken FindToken(int position) {
        return SyntaxTree.Tokens.FindAtPosition(position);
    }

    /// <summary>
    /// Der Knoten, dem das Token an der angegebenen <paramref name="position"/> zugeordnet ist — sofern
    /// dieses Token innerhalb des <see cref="Extent"/> dieses Knotens liegt; sonst <c>null</c>.
    /// </summary>
    public SyntaxNode FindNode(int position) {
        var token = SyntaxTree.Tokens.FindAtPosition(position);
        if (token.IsMissing) {
            return null;
        }

        if (Extent.Contains(token.Extent)) {
            return token.Parent;
        }

        return null;
    }

    /// <summary>
    /// Der <see cref="Extent"/> dieses Knotens samt umgebender Trivia — von der Leading- bis zur
    /// Trailing-Trivia (siehe <see cref="GetLeadingTriviaExtent"/> / <see cref="GetTrailingTriviaExtent"/>).
    /// </summary>
    /// <param name="onlyWhiteSpace">Wenn <c>true</c>, zählt nur Whitespace als Trivia (Kommentare begrenzen
    /// den Ausschnitt); sonst zählen auch Kommentare als Trivia.</param>
    public TextExtent GetFullExtent(bool onlyWhiteSpace = false) {
        return TextExtent.FromBounds(GetLeadingTriviaExtent(onlyWhiteSpace).Start, GetTrailingTriviaExtent(onlyWhiteSpace).End);
    }

    /// <summary>
    /// Die Leading-Trivia dieses Knotens — abgeleitet aus dem <b>ersten</b> signifikanten Token (echtes
    /// Roslyn-Modell). Hat der Knoten keine eigenen Token, ist sie leer.
    /// </summary>
    public ImmutableArray<SyntaxTrivia> GetLeadingTrivia() {
        var first = ChildTokens().FirstOrDefault();
        return first.IsMissing ? ImmutableArray<SyntaxTrivia>.Empty : first.LeadingTrivia;
    }

    /// <summary>
    /// Die Trailing-Trivia dieses Knotens — abgeleitet aus dem <b>letzten</b> signifikanten Token (echtes
    /// Roslyn-Modell). Hat der Knoten keine eigenen Token, ist sie leer.
    /// </summary>
    public ImmutableArray<SyntaxTrivia> GetTrailingTrivia() {
        var last = ChildTokens().LastOrDefault();
        return last.IsMissing ? ImmutableArray<SyntaxTrivia>.Empty : last.TrailingTrivia;
    }

    /// <summary>
    /// Der Ausschnitt der Leading-Trivia vor diesem Knoten: die Einrückung seiner eigenen Zeile sowie alle
    /// unmittelbar darüber liegenden Zeilen, die ausschließlich aus Trivia bestehen. Steht vor dem Knoten in
    /// seiner Zeile bereits etwas Nicht-Trivia, ist der Ausschnitt leer (nullbreit am Knotenanfang).
    /// </summary>
    /// <param name="onlyWhiteSpace">Wenn <c>true</c>, zählt nur Whitespace als Trivia; Kommentare begrenzen
    /// den Ausschnitt dann.</param>
    public TextExtent GetLeadingTriviaExtent(bool onlyWhiteSpace = false) {
        var isTrivia      = GetIsTriviaFunc(onlyWhiteSpace);
        var nodeStartLine = SyntaxTree.SourceText.GetTextLineAtPosition(Start);
        var leadingExtent = TextExtent.FromBounds(nodeStartLine.Extent.Start, Start);

        var start = Start;
        // Wenn  bis zum Zeilenanfang nur Trivia Tokens, werden alle vorigen Zeilen, die nur aus Trivias bestehen, auch mit dazu genommen
        if (SyntaxTree.Tokens[leadingExtent].All(token => isTrivia(token.Classification))) {
            // Trivia geht mindestens zum Zeilenanfang der Node
            start = leadingExtent.Start;
            // Jetzt alle vorigen Zeilen durchlaufen
            var line = nodeStartLine.Line - 1;
            while (line >= 0) {

                var lineExtent = SyntaxTree.SourceText.TextLines[line].Extent;
                if (!SyntaxTree.Tokens[lineExtent].All(token => isTrivia(token.Classification))) {
                    // Zeile besteht nicht nur aus Trivias
                    break;
                }

                start =  lineExtent.Start;
                line  -= 1;
            }
        }

        return TextExtent.FromBounds(start, Start);
    }

    /// <summary>
    /// Der Ausschnitt der Trailing-Trivia nach diesem Knoten: vom Knotenende bis zum nächsten Token,
    /// respektive bis zum Ende der Zeile (das Zeilenende eingeschlossen), falls in dieser Zeile kein
    /// weiteres Token mehr folgt.
    /// </summary>
    /// <param name="onlyWhiteSpace">Wenn <c>true</c>, zählt nur Whitespace als Trivia; ein Kommentar in der
    /// Zeile begrenzt den Ausschnitt dann.</param>
    public TextExtent GetTrailingTriviaExtent(bool onlyWhiteSpace = false) {

        var isTrivia       = GetIsTriviaFunc(onlyWhiteSpace);
        var nodeEndLine    = SyntaxTree.SourceText.GetTextLineAtPosition(End);
        var trailingExtent = TextExtent.FromBounds(End, nodeEndLine.Extent.End);

        var endToken = SyntaxTree.Tokens[trailingExtent]
                                 .SkipWhile(token => isTrivia(token.Classification))
                                 .FirstOrDefault();

        var end = endToken.IsMissing ? nodeEndLine.Extent.End : endToken.Start;

        return TextExtent.FromBounds(End, end);
    }

    /// <summary>
    /// Liefert das Trivia-Prädikat für die <c>Get*TriviaExtent</c>-Methoden: bei
    /// <paramref name="onlyWhiteSpace"/> nur Whitespace, sonst Whitespace und Kommentare
    /// (siehe <see cref="SyntaxFacts.IsTrivia"/>).
    /// </summary>
    static Func<TextClassification, bool> GetIsTriviaFunc(bool onlyWhiteSpace = false) {
        return onlyWhiteSpace ? (c => c == TextClassification.Whitespace) : new Func<TextClassification, bool>(SyntaxFacts.IsTrivia);
    }

    /// <summary>Der <see cref="SyntaxTree"/>, zu dem dieser Knoten gehört. Erst nach <see cref="FinalConstruct"/> verfügbar.</summary>
    [NotNull]
    public SyntaxTree SyntaxTree {
        get {
            EnsureConstructed();
            return _syntaxTree;
        }
    }

    /// <summary>
    /// Fügt — nur während des Aufbaus — einen Kindknoten an. <c>null</c> wird ignoriert. Nach
    /// <see cref="FinalConstruct"/> nicht mehr erlaubt.
    /// </summary>
    protected void AddChildNode(SyntaxNode syntaxNode) {
        EnsureConstructionMode();
        EnsureChildNodes();
        if (syntaxNode != null) {
            _childNodes.Add(syntaxNode);
        }
    }

    /// <summary>
    /// Fügt — nur während des Aufbaus — mehrere Kindknoten an (siehe <see cref="AddChildNode"/>).
    /// </summary>
    protected void AddChildNodes(IEnumerable<SyntaxNode> syntaxNodes) {
        EnsureConstructionMode();
        EnsureChildNodes();
        foreach (var node in syntaxNodes) {
            AddChildNode(node);
        }
    }

    /// <summary>Wirft, wenn der Knoten noch im Aufbau ist (kein <see cref="SyntaxTree"/> gesetzt).</summary>
    void EnsureConstructed() {
        if (_syntaxTree == null) {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Legt die Kindknoten-Liste bei Bedarf an (nur im Aufbau-Modus).</summary>
    void EnsureChildNodes() {
        if (_childNodes == null) {
            EnsureConstructionMode();
            _childNodes = new List<SyntaxNode>();
        }
    }

    /// <summary>Wirft, wenn der Knoten bereits eingefroren ist (<see cref="FinalConstruct"/> gelaufen).</summary>
    void EnsureConstructionMode() {
        if (_syntaxTree != null) {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Der Quelltext dieses Knotens (sein <see cref="Extent"/>, ohne umgebende Trivia).</summary>
    public override string ToString() {
        return SyntaxTree.SourceText.Substring(Start, Length);
    }

    /// <summary>Kompakte Debug-Darstellung: Ausschnitt und konkreter Knotentyp.</summary>
    public string ToDebuggerDisplayString() {
        return $"{Extent} {GetType().Name}";
    }

}
