#region Using Directives

using System;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Utilities.Logging;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Puffer-gebundener Dienst, der aus dem <see cref="SyntaxTreeAndSnapshot"/> des <see cref="ParserService"/>
/// das Semantikmodell (<see cref="CodeGenerationUnitAndSnapshot"/>) berechnet und bei Änderungen im
/// Hintergrund neu aufbaut. Host-Adapter über dem Engine-Kern <see cref="CodeGenerationUnit"/>: Als
/// <see cref="ParserServiceDependent"/> reagiert er auf Parse-Ergebnisse; Konsumenten hängen sich über
/// <see cref="SemanticModelChanging"/>/<see cref="SemanticModelChanged"/> ein. Pro Puffer existiert genau
/// eine Instanz (siehe <see cref="GetOrCreateSingelton"/>).
/// </summary>
sealed class SemanticModelService: ParserServiceDependent {

    static readonly Logger Logger = Logger.Create<SemanticModelService>();

    readonly IDisposable          _observable;
    CodeGenerationUnitAndSnapshot _codeGenerationUnitAndSnapshot;
    bool                          _waitingForAnalysis;

    /// <summary>
    /// Baut die Reaktive-Extensions-Pipeline auf, die auf <see cref="RebuildTriggered"/> reagiert, im
    /// Hintergrund das Semantikmodell berechnet und das jüngste Ergebnis auf dem UI-Thread übernimmt. Nicht
    /// direkt aufrufen — den Puffer-Singleton über <see cref="GetOrCreateSingelton"/> beziehen.
    /// </summary>
    SemanticModelService(ITextBuffer textBuffer): base(textBuffer) {

        _observable = Observable.FromEventPattern<EventArgs>(
                                     handler => RebuildTriggered += handler,
                                     handler => RebuildTriggered -= handler)
                                 // .Throttle(ServiceProperties.SemanticModelServiceThrottleTime)
                                .Select(_ => Observable.DeferAsync(async token =>
                                 {
                                     var codeGenerationUnitAndSnapshot = await BuildAsync(ParserService.SyntaxTreeAndSnapshot, token).ConfigureAwait(false);

                                     return Observable.Return(codeGenerationUnitAndSnapshot);
                                 }))
                                .Switch()
                                .ObserveOn(SynchronizationContext.Current)
                                .Subscribe(TrySetResult);

        _waitingForAnalysis = true;
    }

    /// <summary>
    /// Liefert die eindeutige, an <paramref name="textBuffer"/> gebundene <see cref="SemanticModelService"/>-Instanz
    /// und legt sie beim ersten Aufruf an.
    /// </summary>
    public static SemanticModelService GetOrCreateSingelton(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(
            typeof(SemanticModelService),
            () => new SemanticModelService(textBuffer));
    }

    /// <summary>
    /// Meldet die <see cref="ParserService"/>-Events ab (Basis) und beendet die eigene Semantik-Pipeline.
    /// </summary>
    public override void Dispose() {
        base.Dispose();
        _observable?.Dispose();
    }

    /// <summary>
    /// Wird ausgelöst, sobald das vorhandene <see cref="CodeGenerationUnitAndSnapshot"/> veraltet und ein
    /// Neuaufbau ansteht.
    /// </summary>
    public event EventHandler<EventArgs>             SemanticModelChanging;
    /// <summary>
    /// Wird ausgelöst, sobald ein neues, zum aktuellen Puffer passendes <see cref="CodeGenerationUnitAndSnapshot"/>
    /// bereitsteht.
    /// </summary>
    public event EventHandler<SnapshotSpanEventArgs> SemanticModelChanged;
    // Dieses Event feuern wir u.a. um den Observer zu "füttern".
    /// <summary>
    /// Internes Signal, das die Reaktive-Extensions-Pipeline zum Neuaufbau des Semantikmodells anstößt.
    /// </summary>
    event EventHandler<EventArgs> RebuildTriggered;

    /// <summary>
    /// <see langword="true"/>, solange ein angestoßener Neuaufbau des Semantikmodells noch aussteht.
    /// </summary>
    public bool WaitingForAnalysis {
        get { return _waitingForAnalysis; }
    }

    /// <summary>
    /// Das zuletzt berechnete <see cref="CodeGenerationUnitAndSnapshot"/> oder <see langword="null"/>, solange
    /// noch keines vorliegt.
    /// </summary>
    [CanBeNull]
    public CodeGenerationUnitAndSnapshot CodeGenerationUnitAndSnapshot {
        get { return _codeGenerationUnitAndSnapshot; }
    }
        
    
        
    /// <summary>
    /// Liefert die an <paramref name="textBuffer"/> gebundene <see cref="SemanticModelService"/>-Instanz oder
    /// <see langword="null"/>, falls für diesen Puffer noch keine angelegt wurde.
    /// </summary>
    [CanBeNull]
    public static SemanticModelService TryGet(ITextBuffer textBuffer) {
        textBuffer.Properties.TryGetProperty<SemanticModelService>(typeof(SemanticModelService), out var semanticModelService);
        return semanticModelService;
    }

    /// <summary>
    /// Verwirft das vorhandene Semantikmodell (<see cref="SemanticModelChanging"/>) und stößt einen erneuten
    /// Aufbau an.
    /// </summary>
    public void Invalidate() {
        OnSemanticModelChanging(EventArgs.Empty);
        OnRebuildTriggered(EventArgs.Empty);
    }

    /// <summary>
    /// Erzwingt — unter Umgehung der asynchronen Pipeline — sofort ein aktuelles
    /// <see cref="CodeGenerationUnitAndSnapshot"/> und liefert es zurück. Muss auf dem UI-Thread laufen (die
    /// Ergebnis-Events werden dort gefeuert). Ist das gecachte Modell bereits aktuell, wird es unverändert
    /// zurückgegeben; andernfalls wird — nach einem synchronen Parse-Update — neu berechnet.
    /// </summary>
    public CodeGenerationUnitAndSnapshot UpdateSynchronously(CancellationToken cancellationToken = default) {

        // Muss im UI Thread sein, da sonst die Events nicht ind er UI gefeuert werden, und TrySetResult muss um UI Thrtead ausgeführt werden.
        ThreadHelper.ThrowIfNotOnUIThread();

        var codeGenerationUnitAndSnapshot = CodeGenerationUnitAndSnapshot;
        if(codeGenerationUnitAndSnapshot != null && codeGenerationUnitAndSnapshot.IsCurrent(TextBuffer)) {
            return codeGenerationUnitAndSnapshot;
        }

        var syntaxTreeAndSnapshot =ParserService.UpdateSynchronously(cancellationToken);

        codeGenerationUnitAndSnapshot = BuildSynchronously(syntaxTreeAndSnapshot, cancellationToken);
        TrySetResult(codeGenerationUnitAndSnapshot);

        return codeGenerationUnitAndSnapshot;
    }

    /// <summary>
    /// Reicht ein anstehendes Parse-Update als <see cref="SemanticModelChanging"/> weiter — das bisherige
    /// Semantikmodell gilt damit als veraltet.
    /// </summary>
    protected override void OnParseResultChanging(object sender, EventArgs e) {
        OnSemanticModelChanging(EventArgs.Empty);
    }

    /// <summary>
    /// Stößt nach einem neuen Parse-Ergebnis den Neuaufbau des Semantikmodells an.
    /// </summary>
    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        Invalidate();
    }
        
    void OnRebuildTriggered(EventArgs e) {
        RebuildTriggered?.Invoke(this, e);
    }

    void OnSemanticModelChanging(EventArgs e) {
        _waitingForAnalysis = true;
        SemanticModelChanging?.Invoke(this, e);
    }

    void OnSemanticModelChanged(SnapshotSpanEventArgs e) {
        _waitingForAnalysis = false;
        SemanticModelChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Berechnet das Semantikmodell im Hintergrund-Thread (siehe <see cref="BuildSynchronously"/>).
    /// </summary>
    static async Task<CodeGenerationUnitAndSnapshot> BuildAsync(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, CancellationToken cancellationToken) {
        return await Task.Run(() => {
            using(Logger.LogBlock(nameof(BuildAsync))) {
                return BuildSynchronously(syntaxTreeAndSnapshot, cancellationToken);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Baut aus dem <paramref name="syntaxTreeAndSnapshot"/> die <see cref="CodeGenerationUnit"/> auf und
    /// bündelt sie mit dem Snapshot. Liefert <see langword="null"/>, wenn kein Parse-Ergebnis vorliegt oder
    /// die Wurzel keine <see cref="CodeGenerationUnitSyntax"/> ist.
    /// </summary>
    static CodeGenerationUnitAndSnapshot BuildSynchronously(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, CancellationToken cancellationToken) {

        if(syntaxTreeAndSnapshot == null) {
            Logger.Debug("Es gibt kein ParesResult. Der Vorgang wird abgebrochen.");
            return null;
        }

        var syntaxTree = syntaxTreeAndSnapshot.SyntaxTree;
        var snapshot   = syntaxTreeAndSnapshot.Snapshot;

        if(!(syntaxTree.Root is CodeGenerationUnitSyntax codeGenerationUnitSyntax)) {
            Logger.Debug($"Der SyntaxRoot ist nicht vom Typ {typeof(CodeGenerationUnitSyntax)}. Der Vorgang wird abgebrochen.");
            return null;
        }

        var codeGenerationUnit = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax, cancellationToken);

        return new CodeGenerationUnitAndSnapshot(codeGenerationUnit, snapshot);
    }

    /// <summary>
    /// Übernimmt ein frisch berechnetes <paramref name="codeGenerationUnitAndSnapshot"/> als aktuelles Modell
    /// und löst <see cref="SemanticModelChanged"/> aus — sofern es nicht <see langword="null"/> ist und noch
    /// zum Puffer passt (andernfalls verworfen). Muss auf dem UI-Thread laufen.
    /// </summary>
    void TrySetResult(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        ThreadHelper.ThrowIfNotOnUIThread();

        // Dieser Fall kann eintreten, da wir im Ctor "blind" ein Invalidate aufrufen. Möglicherweise gibt es aber noch kein SyntaxTreeAndSnapshot,
        // welches aber noch folgen wird und im Zuge eines OnParseResultChanging abgerbeitet wird.
        if(codeGenerationUnitAndSnapshot == null) {
            return;
        }
        // Der Puffer wurde zwischenzeitlich schon wieder geändert. Dieses Ergebnis brauchen wir nicht,
        // da bereits ein neues berechnet wird.
        if (!codeGenerationUnitAndSnapshot.IsCurrent(TextBuffer)) {
            return;
        }

        _codeGenerationUnitAndSnapshot = codeGenerationUnitAndSnapshot;

        OnSemanticModelChanged(new SnapshotSpanEventArgs(codeGenerationUnitAndSnapshot.Snapshot.GetFullSpan()));
    }        
}