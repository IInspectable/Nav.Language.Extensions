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

[Serializable]
[DebuggerDisplay("{" + nameof(ToDebuggerDisplayString) + "(), nq}")]
public abstract partial class SyntaxNode: IExtent {

    List<SyntaxNode> _childNodes;
    SyntaxTree       _syntaxTree;
    SyntaxNode       _parent;

    internal SyntaxNode(TextExtent extent) {
        Extent = extent;
    }

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

    public int Start  => Extent.Start;
    public int End    => Extent.End;
    public int Length => Extent.Length;

    public TextExtent Extent { get; }

    [CanBeNull]
    public SyntaxNode Parent {
        get {
            EnsureConstructed();
            return _parent;
        }
    }

    public Location GetLocation() {
        EnsureConstructed();
        return SyntaxTree.SourceText.GetLocation(Extent);
    }

    [NotNull]
    public IEnumerable<SyntaxToken> ChildTokens() {
        return SyntaxTree.Tokens[Extent].Where(token => token.Parent == this);
    }

    static readonly IReadOnlyList<SyntaxNode> EmptyNodeList = new List<SyntaxNode>();

    [NotNull]
    public IReadOnlyList<SyntaxNode> ChildNodes() {
        EnsureConstructed();
        return _childNodes ?? EmptyNodeList;
    }

    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodes() {
        return DescendantNodes<SyntaxNode>();
    }

    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf() {
        return DescendantNodesAndSelf<SyntaxNode>();
    }

    [NotNull]
    public IEnumerable<T> DescendantNodes<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: false);
    }

    [NotNull]
    public IEnumerable<T> DescendantNodesAndSelf<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: true);
    }

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
    /// F�r Knoten, die sehr weit "oben" liegen, kann die Implementierung von DescendantNodes&lt;T&gt;
    /// massiv beschleunigt werden, wenn sichergestellt werden kann, dass ein Knoten keine untergeordnenten 
    /// Knoten vom selben Typ haben kann, und deshalb die Suche in den Kindknoten vorzeitig abgebrochen
    /// werden kann.
    /// Eigentlich m�sste diese Eigenschaft systematisch �berschrieben werden. Bisweilen werden hiermit nur
    /// die Hotspots optimiert.
    /// </summary>
    private protected virtual bool PromiseNoDescendantNodeOfSameType => false;

    /// <summary>
    /// Gets a list of ancestor nodes
    /// </summary>
    public IEnumerable<SyntaxNode> Ancestors() {
        return Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>();
    }

    /// <summary>
    /// Gets a list of ancestor nodes (including this node) 
    /// </summary>
    public IEnumerable<SyntaxNode> AncestorsAndSelf() {
        for (var node = this; node != null; node = node.Parent) {
            yield return node;
        }
    }

    public SyntaxToken FindToken(int position) {
        return SyntaxTree.Tokens.FindAtPosition(position);          
    }

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

    // Die Trailing Trivias gehen bis zum n�chsten Token, respektive zum Ende der Zeile
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

    static Func<TextClassification, bool> GetIsTriviaFunc(bool onlyWhiteSpace = false) {
        return onlyWhiteSpace ? (c => c == TextClassification.Whitespace) : new Func<TextClassification, bool>(SyntaxFacts.IsTrivia);
    }

    [NotNull]
    public SyntaxTree SyntaxTree {
        get {
            EnsureConstructed();
            return _syntaxTree;
        }
    }

    protected void AddChildNode(SyntaxNode syntaxNode) {
        EnsureConstructionMode();
        EnsureChildNodes();
        if (syntaxNode != null) {
            _childNodes.Add(syntaxNode);
        }
    }

    protected void AddChildNodes(IEnumerable<SyntaxNode> syntaxNodes) {
        EnsureConstructionMode();
        EnsureChildNodes();
        foreach (var node in syntaxNodes) {
            AddChildNode(node);
        }
    }

    void EnsureConstructed() {
        if (_syntaxTree == null) {
            throw new InvalidOperationException();
        }
    }

    void EnsureChildNodes() {
        if (_childNodes == null) {
            EnsureConstructionMode();
            _childNodes = new List<SyntaxNode>();
        }
    }

    void EnsureConstructionMode() {
        if (_syntaxTree != null) {
            throw new InvalidOperationException();
        }
    }

    public override string ToString() {
        return SyntaxTree.SourceText.Substring(Start, Length);
    }

    public string ToDebuggerDisplayString() {
        return $"{Extent} {GetType().Name}";
    }

}