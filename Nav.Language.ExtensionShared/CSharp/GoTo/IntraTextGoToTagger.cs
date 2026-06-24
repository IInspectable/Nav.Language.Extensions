#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using JetBrains.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Utilities.Logging;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.Extension.Notification;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

class IntraTextGoToTagger: ITagger<IntraTextGoToTag>, IClassAnnotationChangeListener, IDisposable {

    static readonly Logger Logger = Logger.Create<IntraTextGoToTagger>();

    readonly ITextBuffer           _textBuffer;
    readonly IDisposable           _parserObs;
    readonly WorkspaceRegistration _workspaceRegistration;

    [CanBeNull]
    BuildTagsResult _result;  
    [CanBeNull]
    Workspace _workspace;

    public IntraTextGoToTagger(ITextBuffer textBuffer) {

        _textBuffer = textBuffer;

        // Direktes Abo auf Buffer-Änderungen: stellt sicher, dass nach einer Workspace-Re-Registration
        // (z.B. nach einem Build) beim nächsten Tippen neu getaggt wird, falls die Roslyn-Workspace-Events
        // ausbleiben (Self-Heal, analog zu Roslyns TaggerEventSources.OnTextChanged).
        _textBuffer.Changed += OnTextBufferChanged;

        Logger.Info($"{nameof(IntraTextGoToTagger)}.Ctor Begin: {_textBuffer.GetTextDocument()?.FilePath}");

        _workspaceRegistration                  =  Workspace.GetWorkspaceRegistration(textBuffer.AsTextContainer());
        _workspaceRegistration.WorkspaceChanged += OnWorkspaceRegistrationChanged;
            
        if (_workspaceRegistration.Workspace != null) {
            ConnectToWorkspace(_workspaceRegistration.Workspace);
        }

        _parserObs = Observable.FromEventPattern<EventArgs>(
                                    handler => RebuildTriggered += handler,
                                    handler => RebuildTriggered -= handler)
                               .Select(_ => new BuildTagsArgs {
                                    DocumentId = GetDocumentId(),
                                    Snapshot   = _textBuffer.CurrentSnapshot })
                               .Throttle(ServiceProperties.GoToNavTaggerThrottleTime)
                               .Select(args => Observable.DeferAsync(async token =>
                                {
                                    var parseResult = await BuildTagsAsync(args, token).ConfigureAwait(false);

                                    return Observable.Return(parseResult);
                                }))
                               .Switch()
                               .ObserveOn(SynchronizationContext.Current)
                               .Subscribe(TrySetResult);


        NotificationService.AddClassAnnotationChangeListener(this);
            
        Invalidate();

        Logger.Info($"{nameof(IntraTextGoToTagger)}.Ctor Ende: {_textBuffer.GetTextDocument()?.FilePath}");
    }

    void IClassAnnotationChangeListener.OnClassAnnotationsChanged(object sender, ClassAnnotationChangedArgs e) {

        // Die Änderung kommt von uns selbst 
        if(e.DocumentId == GetDocumentId()) {
            return;
        }

        if(_result?.Annotations.Any(taskAnnotation =>
                                        // Tatsächlich ist der Taskname nicht eindeutig, was zur Folge haben kann, dass wir 
                                        // theoretisch das eine oder file zu viel aktualisieren. So what?
                                        taskAnnotation.TaskName == e.TaskAnnotation.TaskName) ==true) {

            Logger.Info($"Das Taggen für die Datei wird getriggert, weil sich die Annotations für den Task '{e.TaskAnnotation.TaskName}' geändert haben. Dieses Dokument:' {GetDocumentId()}', geändertes Dokument: '{e.DocumentId}': {_textBuffer.GetTextDocument()?.FilePath}");

            Invalidate();
        }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public IEnumerable<ITagSpan<IntraTextGoToTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        if (_result == null || spans.Count == 0) {
            yield break;
        }

        foreach (var span in spans) {
            foreach (var tag in _result.Tags) {

                var transSpan = tag.Span.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive);
                if (transSpan.IntersectsWith(span)) {
                    yield return new TagSpan<IntraTextGoToTag>(transSpan, tag.Tag);
                }
            }
        }
    }

    void OnWorkspaceRegistrationChanged(object sender, EventArgs e) {
        Logger.Trace($"{nameof(OnWorkspaceRegistrationChanged)}: {_textBuffer.GetTextDocument()?.FilePath}: {_textBuffer.GetTextDocument()?.FilePath}");
        DisconnectFromWorkspace();

        var newWorkspace = _workspaceRegistration.Workspace;

        ConnectToWorkspace(newWorkspace);
    }

    void ConnectToWorkspace([CanBeNull] Workspace workspace) {

        DisconnectFromWorkspace();
        Logger.Trace($"{nameof(ConnectToWorkspace)}: {_textBuffer.GetTextDocument()?.FilePath}: {_textBuffer.GetTextDocument()?.FilePath}");

        _workspace = workspace;

        if(_workspace != null) {
            // ReSharper disable PossibleNullReferenceException
            // TODO Fehlt uns irgendein Event? Es scheint manchmal vorzukommen, dass die Tags nach dem Starten von VS nicht verfügbar sind...
            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.DocumentOpened   += OnDocumentOpened;
            // ReSharper restore PossibleNullReferenceException
        }

        Invalidate();
    }

    void DisconnectFromWorkspace() {
            
        _result = null;

        if (_workspace != null) {
            Logger.Trace($"{nameof(DisconnectFromWorkspace)}: {_textBuffer.GetTextDocument()?.FilePath}");

            // ReSharper disable PossibleNullReferenceException
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.DocumentOpened   -= OnDocumentOpened;
            // ReSharper restore PossibleNullReferenceException
            _workspace = null;                
        }
    }

    void OnDocumentOpened(object sender, DocumentEventArgs args) {
        InvalidateIfThisDocument(args.Document.Id);
    }

    void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs args) {

        Logger.Debug($"{nameof(OnWorkspaceChanged)}@{GetDocumentId()}: {args.Kind}: {_textBuffer.GetTextDocument()?.FilePath}");

        // We're getting an event for a workspace we already disconnected from
        if (args.NewSolution.Workspace != _workspace) {
            // we are async so we are getting events from previous workspace we were associated with
            // just ignore them
            return;
        }

        if(args.Kind == WorkspaceChangeKind.DocumentChanged || args.Kind ==WorkspaceChangeKind.DocumentReloaded) {
            InvalidateIfThisDocument(args.DocumentId);
        }               
    }

    void InvalidateIfThisDocument(DocumentId documentId) {
        if (_workspace != null) {
            var openDocumentId = GetDocumentId();
            if (openDocumentId == documentId) {
                Invalidate();
            }
        }
    }

    [CanBeNull]
    DocumentId GetDocumentId() {
        var openDocumentId = _workspace?.GetDocumentIdInCurrentContext(_textBuffer.AsTextContainer());
        return openDocumentId;
    }

    /// <summary>
    /// TrySetResult wird im GUI-Context aufgerufen
    /// </summary>
    void TrySetResult(BuildTagsResult result) {

        // Der Puffer wurde zwischenzeitlich schon wieder geändert. Dieses Ergebnis brauchen wir nicht,
        // da bereits ein neues berechnet wird.
        if (_textBuffer.CurrentSnapshot != result.BuildArgs.Snapshot) {   
            return;
        }

        // Der Buffer ist momentan keinem Workspace/Roslyn-Dokument zugeordnet (z.B. während einer
        // Workspace-Re-Registration nach Build/Solution-Reload, oder durch eine Exception beim Bauen).
        // In diesem Übergangszustand behalten wir das letzte gute Ergebnis und feuern KEIN (leeres)
        // TagsChanged, damit die Adornments nicht fälschlich verschwinden.
        if (!result.DocumentResolved) {
            Logger.Debug($"{nameof(TrySetResult)}: Kein Roslyn-Dokument auflösbar, behalte vorheriges Ergebnis: {_textBuffer.GetTextDocument()?.FilePath}");
            return;
        }

        var prevResult = _result;

        // Das neue Ergebnis wird schon alleine wegen der aktuelleren SnapshotSpans gespeichert
        _result = result;

        if (ShouldRaiseTagsChanged(prevResult, _result)) {

            var snapshotSpan = result.BuildArgs.Snapshot.GetFullSpan();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(snapshotSpan));

            // In der "Praxis" haben wir nur eine einzige NavTaskAnnotation pro file
            // Da wir ausschließlich in der WFSBase die Annotations haben (=> DeclaringClassDeclarationSyntax),
            // müssen wir nur für die abgeleiteten Klassen explizit das Taggen antriggern,
            // da diese nicht automatisch ein DocumentChanged Ereignis erhalten, nur weil sich das 
            // File der Baisklasse geändert hat.
            foreach (var navTaskAnnotation in result.Annotations
                                                    .Where(a => a.GetType()              == typeof(NavTaskAnnotation))
                                                    .Where(a => a.ClassDeclarationSyntax ==a.DeclaringClassDeclarationSyntax)) {

                var args = new ClassAnnotationChangedArgs {
                    DocumentId     = result.BuildArgs.DocumentId,
                    TaskAnnotation = navTaskAnnotation
                };
                NotificationService.RaiseClassAnnotationChanged(this, args);
            }
        }
    }

    static bool ShouldRaiseTagsChanged(BuildTagsResult prevResult, BuildTagsResult newResult) {

        // Der triviale, aber Standardfall für alle "nicht-WFS" files.
        // prevResult kann null sein (erstes Ergebnis bzw. nach DisconnectFromWorkspace); dann gibt es
        // keine alten Tags, ein leeres newResult ändert also ebenfalls nichts.
        if ((prevResult?.Tags.Count ?? 0) == 0 && newResult.Tags.Count ==0) {

            Logger.Info($"{nameof(ShouldRaiseTagsChanged)} liefert false, da keine Annotation-Tags in der Datei gefunden wurde ('{newResult.BuildArgs.DocumentId}')");
            return false;
        }

        // Hier könnte man theoretisch noch weiter in die Tags schauen, ob sich diese de facto geändert haben,
        // um so unnötig UI-Updates zu vermeiden. Wie groß ist der Benefit, wie groß das "Risiko" echte Updates
        // zu verlieren?

        return true;
    }

    // Dieses Event feuern wir um den Observer zu "füttern".
    event EventHandler<EventArgs> RebuildTriggered;

    void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
        Invalidate();
    }

    public void Invalidate() {
        RebuildTriggered?.Invoke(this, EventArgs.Empty);
    }
        
    public void Dispose() {
        _textBuffer.Changed                     -= OnTextBufferChanged;
        _workspaceRegistration.WorkspaceChanged -= OnWorkspaceRegistrationChanged;
        DisconnectFromWorkspace();
        _parserObs.Dispose();

        NotificationService.RemoveClassAnnotationChangeListener(this);

        Logger.Info($"{nameof(IntraTextGoToTagger)}.{nameof(Dispose)}: {_textBuffer.GetTextDocument()?.FilePath}");
    }

        
    /// <summary>
    /// Achtung: Diese Methode wird bereits in einem Background Thread aufgerufen. Also vorsicht bzgl. thread safety!
    /// </summary>
    static Task<BuildTagsResult> BuildTagsAsync(BuildTagsArgs args, CancellationToken cancellationToken) {

        return Task.Run(() => {

            try {
                return BuildTags(args);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                // Eine Exception darf NICHT den äußeren Rx-Stream beenden (sonst ist der Tagger dauerhaft
                // tot und die Adornments kehren erst nach erneutem Öffnen zurück). Wir liefern ein "nicht
                // aufgelöstes" Ergebnis (DocumentResolved == false), das in TrySetResult das letzte gute
                // Ergebnis erhält.
                Logger.Error(ex, $"{nameof(BuildTags)} ist fehlgeschlagen: {args.DocumentId}");
                return new BuildTagsResult(args);
            }

        }, cancellationToken);
    }

    static BuildTagsResult BuildTags(BuildTagsArgs buildArgs) {

        using(Logger.LogBlock(nameof(BuildTags))) {

            var document = buildArgs.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if(document == null) {
                // Wahrscheinlich handelt es sich bei diesem Dokument um ein "externes"
                Logger.Warn($"{nameof(BuildTags)}: Es steht kein Roslyn Dokument für '{buildArgs.DocumentId}' zur Verfügung. Der Vorgang wurde abgebrochen.");
                return new BuildTagsResult(buildArgs);
            }
            
            var annotations = AnnotationReader.ReadNavTaskAnnotations(document)
                                              .ToList();
            var tagSpanBuilder = new IntraTextGoToTagSpanBuilder(annotations, buildArgs.Snapshot);

            var tags = annotations.Select(annotation => tagSpanBuilder.Visit(annotation))
                                  .Where(tagsSpan => tagsSpan != null)
                                  .ToList();

            Logger.Debug($"{tags.Count} Annotations in Dokument {buildArgs.DocumentId} gefunden.");

            return new BuildTagsResult(buildArgs, tags, annotations);
        }
    }

    struct BuildTagsArgs {

        public ITextSnapshot Snapshot   { get; init; }
        public DocumentId    DocumentId { get; init; }

    }

    sealed class BuildTagsResult {

        public BuildTagsResult(BuildTagsArgs buildArgs) {
            BuildArgs        = buildArgs;
            Annotations      = new List<NavTaskAnnotation>();
            Tags             = new List<ITagSpan<IntraTextGoToTag>>();
            DocumentResolved = false;
        }

        public BuildTagsResult(BuildTagsArgs buildArgs, IList<ITagSpan<IntraTextGoToTag>> tags, IList<NavTaskAnnotation> annotations) {
            BuildArgs        = buildArgs;
            Tags             = tags;
            Annotations      = annotations;
            DocumentResolved = true;
        }

        public BuildTagsArgs                     BuildArgs        { get; }
        public IList<ITagSpan<IntraTextGoToTag>> Tags             { get; }
        public IList<NavTaskAnnotation>          Annotations      { get; }

        // True, wenn für den Snapshot ein Roslyn-Dokument aufgelöst werden konnte. Bei false befindet
        // sich der Buffer in einem Übergangszustand (keinem Workspace zugeordnet); das Ergebnis ist dann
        // nicht aussagekräftig und darf das letzte gute Ergebnis nicht überschreiben.
        public bool                              DocumentResolved { get; }
    }
}