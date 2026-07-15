#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Notification; 

/// <summary>
/// Verwaltet eine Liste schwach referenzierter Empfänger (<typeparamref name="T"/>) und ruft eine Aktion auf
/// allen noch lebenden auf. Eingesammelte Empfänger werden beim nächsten <see cref="InvokeListener"/>
/// entfernt. Damit hält die Registrierung die Empfänger nicht künstlich am Leben; alle Zugriffe sind
/// über ein Sperrobjekt threadsicher.
/// </summary>
/// <typeparam name="T">Der Typ der registrierten Empfänger (Referenztyp).</typeparam>
class WeakListenerManager<T> where T :class {

    readonly List<WeakReference<T>> _listeners;

    /// <summary>Legt einen leeren Empfänger-Manager an.</summary>
    public WeakListenerManager() {
        _listeners = new List<WeakReference<T>>();
    }

    /// <summary>
    /// Führt <paramref name="action"/> auf jedem noch lebenden Empfänger aus und entfernt dabei bereits
    /// eingesammelte Einträge.
    /// </summary>
    public void InvokeListener(Action<T> action) {

        lock (_listeners) {

            var toRemove = new List<WeakReference<T>>();

            foreach (var entry in _listeners) {
                if (entry.TryGetTarget(out var listener)) {
                    action(listener);
                } else {
                    toRemove.Add(entry);
                }
            }

            foreach (var entry in toRemove) {
                _listeners.Remove(entry);
            }
        }
    }

    /// <summary>Nimmt <paramref name="listener"/> — schwach referenziert — in die Empfängerliste auf.</summary>
    public void AddListener(T listener) {
        lock (_listeners) {
            _listeners.Add(new WeakReference<T>(listener));
        }
    }

    /// <summary>Entfernt alle Einträge, die auf <paramref name="listener"/> verweisen.</summary>
    public void RemoveListener(T listener) {
        lock (_listeners) {

            _listeners.RemoveAll(entry => {
                if(entry.TryGetTarget(out var target)) {
                    return target == listener;
                }
                return false;
            });
        }
    }
}