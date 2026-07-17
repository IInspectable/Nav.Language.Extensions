using System;

using Microsoft.VisualStudio.Commanding;

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Nicht-generischer Basis-Marker aller Nav-Command-Handler. Dient als gemeinsamer MEF-Vertragstyp,
/// über den <see cref="CommandHandlerServiceProvider"/> sämtliche via
/// <see cref="ExportCommandHandlerAttribute"/> exportierten Handler unabhängig von ihrem konkreten
/// <see cref="CommandArgs"/>-Typ importiert; die eigentliche Kommando-Logik liegt in der generischen
/// Variante <see cref="INavCommandHandler{T}"/>.
/// </summary>
interface INavCommandHandler {
}

/// <summary>
/// Ein Handler für ein konkretes Editor-Kommando, adressiert über seinen Argument-Typ
/// <typeparamref name="T"/>. Mehrere Handler zum selben Kommando bilden über
/// <see cref="CommandHandlerService"/> eine Kette; jeder Handler kann das Kommando behandeln oder
/// über den <c>nextHandler</c> an den nächsten Handler (letztlich das nächste
/// <c>IOleCommandTarget</c> von Visual Studio) weiterreichen.
/// </summary>
/// <typeparam name="T">Der Argument-Typ des Kommandos (abgeleitet von <see cref="CommandArgs"/>).</typeparam>
interface INavCommandHandler<in T> : INavCommandHandler where T : CommandArgs {
    /// <summary>
    /// Ermittelt den Zustand des Kommandos (verfügbar/aktiv, Anzeigetext). Behandelt dieser Handler
    /// das Kommando nicht, gibt er das über <paramref name="nextHandler"/> ermittelte Ergebnis zurück.
    /// </summary>
    /// <param name="args">Die Kommando-Argumente.</param>
    /// <param name="nextHandler">Ermittelt den Zustand über den nächsten Handler der Kette.</param>
    /// <returns>Der resultierende <see cref="CommandState"/>.</returns>
    CommandState GetCommandState(T args, Func<CommandState> nextHandler);
    /// <summary>
    /// Führt das Kommando aus. Behandelt dieser Handler es nicht, ruft er
    /// <paramref name="nextHandler"/>, um an den nächsten Handler der Kette weiterzureichen.
    /// </summary>
    /// <param name="args">Die Kommando-Argumente.</param>
    /// <param name="nextHandler">Führt das Kommando über den nächsten Handler der Kette aus.</param>
    void ExecuteCommand(T args, Action nextHandler);
}