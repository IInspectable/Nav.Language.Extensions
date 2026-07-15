#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Roslyn-seitige Annotation eines generierten Task-Typs — der Rückverweis aus dem generierten C#-Code
/// auf seine Nav-Herkunft. Aus den XML-Doku-Tags <c>&lt;NavFile&gt;</c> und <c>&lt;NavTask&gt;</c>
/// (siehe <see cref="Pharmatechnik.Nav.Language.CodeGen.CodeGenInvariants"/>) am generierten
/// <c>{Task}WFS</c>-Typ gelesen und von <see cref="AnnotationReader"/> erzeugt. Bildet die Wurzel der
/// Annotation-Hierarchie: Methoden- (<see cref="NavMethodAnnotation"/>) und Aufruf-Annotationen
/// (<see cref="NavInvocationAnnotation"/>) leiten davon ab und tragen dieselbe Task-Herkunft mit.
/// Trägt die Brücke zurück in die Nav-Welt (Auflösung der <see cref="Location"/> über
/// <c>LocationFinder</c>).
/// </summary>
public partial class NavTaskAnnotation {

    /// <summary>
    /// Erzeugt die Annotation aus den am generierten Task-Typ gefundenen Werten.
    /// </summary>
    /// <param name="classDeclarationSyntax">Die generierte Task-Klasse, an der die Annotation hängt.</param>
    /// <param name="declaringClassDeclarationSyntax">Die Klasse, in der die Tags tatsächlich stehen — häufig die
    /// Basisklasse <c>{Task}WFSBase</c> (siehe <see cref="DeclaringClassDeclarationSyntax"/>).</param>
    /// <param name="taskName">Der Name der Nav-Task (aus dem <c>&lt;NavTask&gt;</c>-Tag).</param>
    /// <param name="navFileName">Der — in einen absoluten Pfad aufgelöste — Pfad der <c>.nav</c>-Quelldatei
    /// (aus dem <c>&lt;NavFile&gt;</c>-Tag).</param>
    /// <exception cref="ArgumentNullException"><paramref name="classDeclarationSyntax"/> oder
    /// <paramref name="declaringClassDeclarationSyntax"/> ist <see langword="null"/>.</exception>
    public NavTaskAnnotation(ClassDeclarationSyntax classDeclarationSyntax, ClassDeclarationSyntax declaringClassDeclarationSyntax, string taskName, string navFileName) {

        ClassDeclarationSyntax          = classDeclarationSyntax          ?? throw new ArgumentNullException(nameof(classDeclarationSyntax));
        DeclaringClassDeclarationSyntax = declaringClassDeclarationSyntax ?? throw new ArgumentNullException(nameof(declaringClassDeclarationSyntax));
        TaskName                        = taskName                        ?? String.Empty;
        NavFileName                     = navFileName                     ?? String.Empty;
    }

    /// <summary>
    /// Kopierkonstruktor — übernimmt die Task-Herkunft (<see cref="ClassDeclarationSyntax"/>,
    /// <see cref="DeclaringClassDeclarationSyntax"/>, <see cref="TaskName"/>, <see cref="NavFileName"/>)
    /// einer bereits gelesenen Annotation. Genutzt von den abgeleiteten Methoden- und
    /// Aufruf-Annotationen, um dieselbe Task auf einen konkreten Member bzw. eine Aufrufstelle zu spezialisieren.
    /// </summary>
    /// <param name="other">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> ist <see langword="null"/>.</exception>
    protected NavTaskAnnotation(NavTaskAnnotation other) {
        ClassDeclarationSyntax          = other?.ClassDeclarationSyntax ?? throw new ArgumentNullException(nameof(other));
        DeclaringClassDeclarationSyntax = other.DeclaringClassDeclarationSyntax;
        TaskName                        = other.TaskName;
        NavFileName                     = other.NavFileName;
    }

    /// <summary>
    /// Die generierte Task-Klasse, an der die Annotation gefunden wurde. Anker für die Navigation in
    /// den generierten C#-Code.
    /// </summary>
    [NotNull]
    public ClassDeclarationSyntax ClassDeclarationSyntax { get; }

    /// <summary>
    /// Liefert die Klasse, in der das Tag definiert wurde. Das kann und wird
    /// in vielen Fällen die Basisklasse "WFSBase" sein.
    /// </summary>
    [NotNull]
    public ClassDeclarationSyntax DeclaringClassDeclarationSyntax { get; }

    /// <summary>
    /// Der Name der Nav-Task, auf die diese Annotation zurückverweist (aus dem <c>&lt;NavTask&gt;</c>-Tag).
    /// </summary>
    [NotNull]
    public string TaskName { get;}

    /// <summary>
    /// Der absolute Pfad der <c>.nav</c>-Quelldatei, aus der die Task generiert wurde (aus dem
    /// <c>&lt;NavFile&gt;</c>-Tag; relative Tag-Angaben werden beim Lesen gegen den Ort der
    /// deklarierenden Klasse aufgelöst).
    /// </summary>
    [NotNull]
    public string NavFileName { get; }
}