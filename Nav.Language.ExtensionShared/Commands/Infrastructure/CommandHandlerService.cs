#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Commanding;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Verteilt ein Editor-Kommando auf die für seinen <see cref="CommandArgs"/>-Typ registrierten
/// <see cref="INavCommandHandler{T}"/> und bündelt deren Ergebnis. <c>lastHandler</c> ist dabei die
/// Rückfallaktion (in der Regel das nächste <c>IOleCommandTarget</c> von Visual Studio), die greift,
/// wenn kein Handler das Kommando behandelt.
/// </summary>
interface ICommandHandlerService {
    /// <summary>
    /// Ermittelt den <see cref="CommandState"/> des Kommandos, indem die zuständigen Handler als Kette
    /// befragt werden; behandelt keiner das Kommando, wird <paramref name="lastHandler"/> ausgewertet.
    /// </summary>
    /// <typeparam name="T">Der Argument-Typ des Kommandos.</typeparam>
    /// <param name="args">Die Kommando-Argumente.</param>
    /// <param name="lastHandler">Rückfall-Zustandsermittlung, wenn kein Handler greift.</param>
    /// <returns>Der resultierende <see cref="CommandState"/>.</returns>
    CommandState GetCommandState<T>(T args, Func<CommandState> lastHandler) where T : CommandArgs;
    /// <summary>
    /// Führt das Kommando über die Kette der zuständigen Handler aus; behandelt keiner das Kommando,
    /// wird <paramref name="lastHandler"/> aufgerufen.
    /// </summary>
    /// <typeparam name="T">Der Argument-Typ des Kommandos.</typeparam>
    /// <param name="args">Die Kommando-Argumente.</param>
    /// <param name="lastHandler">Rückfall-Aktion, wenn kein Handler greift.</param>
    void Execute<T>(T args, Action lastHandler) where T : CommandArgs;
}

/// <summary>
/// Standard-Implementierung von <see cref="ICommandHandlerService"/>. Wird von
/// <see cref="CommandHandlerServiceProvider"/> mit der bereits nach Content-Type gefilterten und
/// gereihten Handler-Liste erzeugt, gruppiert die Handler bei Bedarf nach ihrem
/// <see cref="CommandArgs"/>-Typ (gecacht) und knüpft sie zu einer Weiterreich-Kette, deren
/// Endglied jeweils der übergebene <c>lastHandler</c> ist.
/// </summary>
class CommandHandlerService : ICommandHandlerService {

    readonly IList<INavCommandHandler> _commandHandlers;
    readonly Dictionary<Type, object>  _commandHandlersByType;

    /// <summary>
    /// Erzeugt den Dienst über die (bereits gefilterte und gereihte) Handler-Liste.
    /// </summary>
    /// <param name="commandHandlers">Die zuständigen Command-Handler in Reihenfolge.</param>
    public CommandHandlerService(IList<INavCommandHandler> commandHandlers) {
        _commandHandlers       = commandHandlers;
        _commandHandlersByType = new Dictionary<Type, object>();
    }

    /// <inheritdoc/>
    public CommandState GetCommandState<T>(T args, Func<CommandState> lastHandler) where T : CommandArgs {

        var handlers = GetCommandHandlers<T>();
        return GetCommandState(handlers, args, lastHandler);
    }

    /// <inheritdoc/>
    public void Execute<T>(T args, Action lastHandler) where T : CommandArgs {
        var handlers = GetCommandHandlers<T>();
        ExecuteHandlers(handlers, args, lastHandler);
    }

    /// <summary>
    /// Liefert — gecacht je Argument-Typ <typeparamref name="T"/> — die für diesen Kommando-Typ
    /// zuständigen, stark typisierten Handler.
    /// </summary>
    IList<INavCommandHandler<T>> GetCommandHandlers<T>() where T : CommandArgs {

        var key = typeof(T);
        if(!_commandHandlersByType.TryGetValue(key, out var commandHandlerList)) {
            var stronglyTypedHandlers = _commandHandlers.OfType<INavCommandHandler<T>>().ToList();
            commandHandlerList = stronglyTypedHandlers;
            _commandHandlersByType.Add(key, stronglyTypedHandlers);
        }

        return (IList<INavCommandHandler<T>>) commandHandlerList;
    }

    /// <summary>
    /// Baut aus den Handlern und <paramref name="lastHandler"/> eine verschachtelte Kette auf und
    /// stößt sie beim ersten Handler an. Ohne Handler wird <paramref name="lastHandler"/> direkt
    /// ausgewertet bzw. — fehlt auch dieser — <see cref="CommandState.Unavailable"/> geliefert.
    /// </summary>
    static CommandState GetCommandState<TArgs>(
        IList<INavCommandHandler<TArgs>> commandHandlers,
        TArgs args,
        Func<CommandState> lastHandler) where TArgs : CommandArgs {

        if(commandHandlers.Count > 0) {
            // Build up chain of handlers.
            var handlerChain = lastHandler ?? (() => default);
            for(int i = commandHandlers.Count - 1; i >= 1; i--) {
                // Declare locals to ensure that we don't end up capturing the wrong thing
                var nextHandler = handlerChain;
                int j           = i;
                handlerChain = () => commandHandlers[j].GetCommandState(args, nextHandler);
            }

            // Kick off the first command handler.
            return commandHandlers[0].GetCommandState(args, handlerChain);
        }

        if (lastHandler != null) {
            // If there aren't any command handlers, just invoke the last handler (if there is one).
            return lastHandler();
        }

        return CommandState.Unavailable;
    }

    /// <summary>
    /// Baut aus den Handlern und <paramref name="lastHandler"/> eine verschachtelte Kette auf und
    /// führt sie beim ersten Handler aus. Ohne Handler wird <paramref name="lastHandler"/> — sofern
    /// vorhanden — direkt aufgerufen.
    /// </summary>
    static void ExecuteHandlers<T>(IList<INavCommandHandler<T>> commandHandlers, T args, Action lastHandler) where T : CommandArgs {
        if(commandHandlers?.Count > 0) {
            // Build up chain of handlers.
            var handlerChain = lastHandler ?? delegate { };
            for(int i = commandHandlers.Count - 1; i >= 1; i--) {
                // Declare locals to ensure that we don't end up capturing the wrong thing
                var nextHandler = handlerChain;
                int j           = i;
                handlerChain = () => commandHandlers[j].ExecuteCommand(args, nextHandler);
            }

            // Kick off the first command handler.
            commandHandlers[0].ExecuteCommand(args, handlerChain);
        } else {
            // If there aren't any command handlers, just invoke the last handler (if there is one).
            lastHandler?.Invoke();
        }
    }
}