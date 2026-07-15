namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die gemeinsame (leere) Wurzel aller V1-CodeModels — der Zwischenrepräsentation zwischen
/// Semantikmodell und den V1-Emittern. Aus dem Semantikmodell (<see cref="CodeGenerationUnit"/> bzw.
/// den Task-Symbolen) baut der <see cref="CodeModelBuilder"/> die konkreten Ableitungen; die Emitter
/// unter <c>V1/Emitters/</c> rendern sie via <see cref="CodeBuilder"/> zu C#. Der Typ trägt selbst
/// keine Daten und dient nur als gemeinsame Basis (u.a. <see cref="FileGenerationCodeModel"/>,
/// <see cref="ParameterCodeModel"/>, <see cref="BeginWrapperCodeModel"/>,
/// <see cref="TaskBeginCodeModel"/>).
/// </summary>
public abstract class CodeModel {

}