#region Using Directives 

using System;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Delegat für die Parse-Funktion, die aus dem Puffertext einen <see cref="SyntaxNode"/> erzeugt.
/// Voreinstellung ist der Nav-Parser <see cref="SyntaxNode"/>-erzeugend über die Engine; pro
/// <see cref="ITextBuffer"/> lässt sich die Methode via <see cref="ParserService.SetParseMethod"/>
/// überschreiben (etwa um nur ein Teilkonstrukt statt der ganzen <c>CodeGenerationUnit</c> zu parsen).
/// </summary>
/// <param name="text">Der zu parsende Quelltext.</param>
/// <param name="filePath">Der Dateipfad des Puffers; darf <see langword="null"/> sein (unbenannter Puffer).</param>
/// <param name="cancellationToken">Token zum Abbruch des Parsevorgangs.</param>
public delegate SyntaxNode ParseMethod(string text, string filePath = null, CancellationToken cancellationToken = default);

/// <summary>
/// Puffer-gebundener Dienst, der den <see cref="SyntaxTree"/> zum aktuellen <see cref="ITextBuffer"/>
/// vorhält und bei Textänderungen — throttled und im Hintergrund — neu parst. Host-Adapter über dem
/// Engine-Kern <see cref="Syntax"/>: Pro Puffer existiert genau eine Instanz (siehe
/// <see cref="GetOrCreateSingelton"/>); Konsumenten hängen sich über <see cref="ParseResultChanging"/>/
/// <see cref="ParseResultChanged"/> ein. Aufsetzend darauf berechnet <see cref="SemanticModelService"/>
/// das Semantikmodell.
/// </summary>
sealed class ParserService: IDisposable {

    static readonly Logger Logger = Logger.Create<ParserService>();

    static readonly object      ParseMethodKey = new();
    readonly        IDisposable _parserObs;
    SyntaxTreeAndSnapshot       _syntaxTreeAndSnapshot;
    bool                        _waitingForAnalysis;

    /// <summary>
    /// Baut die Reaktive-Extensions-Pipeline auf: gedrosselt (<see cref="ServiceProperties.ParserServiceThrottleTime"/>)
    /// auf <see cref="RebuildTriggered"/> reagieren, im Hintergrund parsen, das jüngste Ergebnis auf dem
    /// UI-Thread über <see cref="TrySetResult(SyntaxTreeAndSnapshot)"/> übernehmen, und stößt ein initiales
    /// Parsen an. Nicht direkt aufrufen — den Puffer-Singleton über <see cref="GetOrCreateSingelton"/> beziehen.
    /// </summary>
    ParserService(ITextBuffer textBuffer) {
        TextBuffer = textBuffer;

        WeakEventDispatcher.Connect(this, textBuffer);

        _parserObs = Observable.FromEventPattern<EventArgs>(
                                    handler => RebuildTriggered += handler,
                                    handler => RebuildTriggered -= handler)
                               .Select( _ => CreateBuildResultArgs())
                               .Throttle(ServiceProperties.ParserServiceThrottleTime)
                               .Select( args => Observable.DeferAsync(async token => 
                                {
                                    var parseResult = await BuildAsync(args, token).ConfigureAwait(false);

                                    return Observable.Return(parseResult);
                                }))
                               .Switch()                                 
                               .ObserveOn(SynchronizationContext.Current)
                               .Subscribe(TrySetResult);

        _waitingForAnalysis = true;
        // Initiales Parsen antriggern
        Invalidate();
    }

    /// <summary>
    /// Liefert die eindeutige, an <paramref name="textBuffer"/> gebundene <see cref="ParserService"/>-Instanz
    /// und legt sie beim ersten Aufruf an. Der Dienst bleibt für die Lebensdauer des Puffers erhalten.
    /// </summary>
    public static ParserService GetOrCreateSingelton(ITextBuffer textBuffer)
    {
        return TextBufferScopedValue<ParserService>.GetOrCreate(
            textBuffer,
            typeof(ParserService),
            () => new ParserService(textBuffer)).Value;
    }

    /// <summary>
    /// Beendet die Parse-Pipeline (Abmelden des Reaktive-Extensions-Abonnements).
    /// </summary>
    public void Dispose() {
        _parserObs.Dispose();
    }

    /// <summary>
    /// Wird ausgelöst, sobald der vorhandene <see cref="SyntaxTreeAndSnapshot"/> durch eine Textänderung
    /// ungültig wird und ein Neuaufbau ansteht — das gecachte Ergebnis sollte ab jetzt als veraltet gelten.
    /// </summary>
    public event EventHandler<EventArgs>             ParseResultChanging;
    /// <summary>
    /// Wird ausgelöst, sobald ein neuer, zum aktuellen Puffer passender <see cref="SyntaxTreeAndSnapshot"/>
    /// bereitsteht. Die <see cref="SnapshotSpanEventArgs"/> umspannen den betroffenen Bereich.
    /// </summary>
    public event EventHandler<SnapshotSpanEventArgs> ParseResultChanged;
    // Dieses Event feuern wir um den Observer zu "füttern".
    /// <summary>
    /// Internes Signal, das die Reaktive-Extensions-Pipeline zum (gedrosselten) Neu-Parsen anstößt.
    /// </summary>
    event EventHandler<EventArgs> RebuildTriggered;

    /// <summary>
    /// Der Puffer, dessen Text dieser Dienst parst.
    /// </summary>
    [NotNull]
    public ITextBuffer TextBuffer { get; }

    /// <summary>
    /// <see langword="true"/>, solange ein angestoßenes Parsen noch aussteht — also zwischen
    /// <see cref="ParseResultChanging"/> und dem darauf folgenden <see cref="ParseResultChanged"/>.
    /// </summary>
    public bool WaitingForAnalysis {
        get { return _waitingForAnalysis; }
    }

    /// <summary>
    /// Der zuletzt berechnete <see cref="SyntaxTreeAndSnapshot"/> oder <see langword="null"/>, solange
    /// noch kein Ergebnis vorliegt.
    /// </summary>
    [CanBeNull]
    public SyntaxTreeAndSnapshot SyntaxTreeAndSnapshot {
        get { return _syntaxTreeAndSnapshot; }           
    }
        
    /// <summary>
    /// Liefert die für <paramref name="textBuffer"/> hinterlegte <see cref="ParseMethod"/> oder — falls keine
    /// gesetzt wurde — den Standard-Parser für die gesamte <c>CodeGenerationUnit</c>.
    /// </summary>
    public static ParseMethod GetParseMethod(ITextBuffer textBuffer) {
        textBuffer.Properties.TryGetProperty(ParseMethodKey, out ParseMethod parseMethod);
        return parseMethod ?? Syntax.ParseCodeGenerationUnit;
    }

    /// <summary>
    /// Liefert die an <paramref name="textBuffer"/> gebundene <see cref="ParserService"/>-Instanz oder
    /// <see langword="null"/>, falls für diesen Puffer noch keine angelegt wurde.
    /// </summary>
    public static ParserService TryGet(ITextBuffer textBuffer) {
        return TextBufferScopedValue<ParserService>.TryGet(textBuffer, typeof(ParserService));
    }
        
    /// <summary>
    /// Hinterlegt eine abweichende <see cref="ParseMethod"/> für <paramref name="textBuffer"/>, die
    /// <see cref="GetParseMethod"/> künftig anstelle des Standard-Parsers liefert.
    /// </summary>
    public static void SetParseMethod(ITextBuffer textBuffer, ParseMethod parseMethod) {
        textBuffer.Properties.AddProperty(ParseMethodKey, parseMethod);
    }

    /// <summary>
    /// Verwirft das vorhandene Ergebnis (<see cref="ParseResultChanging"/>) und stößt ein erneutes Parsen an.
    /// </summary>
    public void Invalidate() {
        OnParseResultChanging(EventArgs.Empty);
        OnRebuildTriggered(EventArgs.Empty);
    }

    /// <summary>
    /// Erzwingt — unter Umgehung der asynchronen, gedrosselten Pipeline — sofort einen aktuellen
    /// <see cref="SyntaxTreeAndSnapshot"/> und liefert ihn zurück. Ist das gecachte Ergebnis bereits aktuell,
    /// wird es unverändert zurückgegeben; andernfalls wird synchron neu geparst (ohne Events auszulösen).
    /// </summary>
    public SyntaxTreeAndSnapshot UpdateSynchronously(CancellationToken cancellationToken = default) {
        var syntaxTreeAndSnapshot = SyntaxTreeAndSnapshot;
        if (syntaxTreeAndSnapshot !=null && syntaxTreeAndSnapshot.IsCurrent(TextBuffer)) {
            return syntaxTreeAndSnapshot;
        }

        syntaxTreeAndSnapshot = BuildSynchronously(CreateBuildResultArgs(), cancellationToken);
        TrySetResult(syntaxTreeAndSnapshot, raiseEvents: false);

        return syntaxTreeAndSnapshot;
    }

    void OnRebuildTriggered(EventArgs e) {
        RebuildTriggered?.Invoke(this, e);
    }

    void OnParseResultChanging(EventArgs e) {
        _waitingForAnalysis = true;
        ParseResultChanging?.Invoke(this, e);
    }

    void OnParseResultChanged(SnapshotSpanEventArgs e) {
        _waitingForAnalysis = false;
        ParseResultChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Momentaufnahme der zum Parsen nötigen Eingaben, die im GUI-Thread abgegriffen und dem
    /// Hintergrund-Parsen übergeben wird (siehe <see cref="CreateBuildResultArgs"/>).
    /// </summary>
    struct BuildResultArgs {
        public ITextSnapshot Snapshot    { get; init; }
        public string        Text        { get; init; }
        public string        FilePath    { get; init; }
        public ParseMethod   ParseMethod { get; init; }
    }

    /// <summary>
    /// Diese Methode muss im GUI Thread aufgerufen werden!
    /// </summary>
    BuildResultArgs CreateBuildResultArgs() {
        var args = new BuildResultArgs {
            Snapshot    = TextBuffer.CurrentSnapshot,
            Text        = TextBuffer.CurrentSnapshot.GetText(),
            FilePath    = TextBuffer.GetTextDocument()?.FilePath,
            ParseMethod = GetParseMethod(TextBuffer)
        };

        return args;
    }

    /// <summary>
    /// Achtung: Diese Methode wird bereits in einem Background Thread aufgerufen. Also vorischt bzgl. thread safety!
    /// Deshalb werden die BuildResultArgs bereits vorab im GUI Thread erstellt.
    /// </summary>
    static async Task<SyntaxTreeAndSnapshot> BuildAsync(BuildResultArgs args, CancellationToken cancellationToken) {
            
        return await Task.Run(() => {

            using(Logger.LogBlock(nameof(BuildAsync))) {
                return BuildSynchronously(args, cancellationToken);
            }

        }, cancellationToken).ConfigureAwait(false);            
    }

    /// <summary>
    /// Führt die eigentliche <see cref="ParseMethod"/> synchron aus und bündelt den entstandenen
    /// <see cref="SyntaxTree"/> mit dem zugehörigen <see cref="ITextSnapshot"/>.
    /// </summary>
    static SyntaxTreeAndSnapshot BuildSynchronously(BuildResultArgs args, CancellationToken cancellationToken) {

        var syntaxTree = args.ParseMethod(args.Text, args.FilePath, cancellationToken).SyntaxTree;

        return new SyntaxTreeAndSnapshot(syntaxTree, args.Snapshot);
    }

    /// <summary>
    /// Übernimmt ein frisch berechnetes Ergebnis und löst dabei die Änderungs-Events aus.
    /// </summary>
    void TrySetResult(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot) {
        TrySetResult(syntaxTreeAndSnapshot, raiseEvents: true);
    }

    /// <summary>
    /// Übernimmt <paramref name="syntaxTreeAndSnapshot"/> als aktuelles Ergebnis, sofern es noch zum Puffer
    /// passt (sonst verworfen, da bereits ein neueres berechnet wird). Bei <paramref name="raiseEvents"/>
    /// wird zusätzlich <see cref="ParseResultChanged"/> ausgelöst.
    /// </summary>
    void TrySetResult(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, bool raiseEvents) {

        // Der Puffer wurde zwischenzeitlich schon wieder geändert. Dieses Ergebnis brauchen wir nicht,
        // da bereits ein neues berechnet wird.
        if (!syntaxTreeAndSnapshot.IsCurrent(TextBuffer)) {
            return;
        }

        _syntaxTreeAndSnapshot = syntaxTreeAndSnapshot;

        var snapshotSpan = syntaxTreeAndSnapshot.Snapshot.GetFullSpan();
        if (raiseEvents) {
            OnParseResultChanged(new SnapshotSpanEventArgs(snapshotSpan));
        }
    }
        
    // Irgend jemand scheint den ITextBuffer länger als erhofft im Speicher zu halten
    // Damit der Parserservice nicht genauso lange im Speicher verbleibt, verknüpfen wir
    // hier die Events "weak".
    /// <summary>
    /// Koppelt das <see cref="ITextBuffer.Changed"/>-Event nur über eine <see cref="WeakReference"/> an den
    /// <see cref="ParserService"/>, damit ein langlebiger Puffer den Dienst nicht am Leben hält. Ist das Ziel
    /// eingesammelt, meldet sich der Dispatcher beim nächsten Event selbst ab.
    /// </summary>
    sealed class WeakEventDispatcher {
        readonly WeakReference _target;

        WeakEventDispatcher(ParserService service) {
            _target = new WeakReference(service);
        }

        /// <summary>
        /// Verbindet <paramref name="service"/> schwach mit den Änderungs-Events von <paramref name="textBuffer"/>.
        /// </summary>
        public static void Connect(ParserService service, ITextBuffer textBuffer) {
            var dispatcher =new WeakEventDispatcher(service);
            textBuffer.Changed += dispatcher.OnTextBufferChanged;
        }

        void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            var textBuffer = (ITextBuffer) sender;
            if (_target.Target is ParserService target) {
                target.Invalidate();
            } else {
                textBuffer.Changed -= OnTextBufferChanged;
            }
        }
    }
}