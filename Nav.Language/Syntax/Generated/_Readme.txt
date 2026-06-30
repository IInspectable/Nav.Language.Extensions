===============================

Der Besucher- und Walker-Code für den Syntaxbaum (ISyntaxNodeVisitor / SyntaxNodeVisitor /
SyntaxNodeWalker) wird beim Kompilieren automatisch erzeugt — durch den Roslyn-Quellgenerator
in _build\SourceGenerators\Nav.Visitor.SourceGenerator (SyntaxVisitorWalkerGenerator). Er löst die
früheren, nur in Visual Studio lauffähigen T4-Templates (.tt) ab und läuft unter dotnet build wie
unter MSBuild.exe.

Quelle der Wahrheit sind die SyntaxNode-Ableitungen selbst: für jede konkrete *Syntax-Klasse entsteht
die passende Accept-/Walk-Überschreibung sowie der zugehörige Besucher-/Walker-Eintrag. Es ist nichts
von Hand zu generieren; ein normaler Build genügt.
