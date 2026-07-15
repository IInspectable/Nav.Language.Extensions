#region Using Directives

using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Basisklasse für ViewModels, die über <see cref="INotifyPropertyChanged"/> Änderungen an
/// ihren Eigenschaften an die WPF-Datenbindung melden.
/// </summary>
abstract class AbstractNotifyPropertyChanged : INotifyPropertyChanged {

    /// <summary>
    /// Wird ausgelöst, wenn sich der Wert einer gebundenen Eigenschaft geändert hat.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Meldet eine Eigenschaftsänderung: ruft <see cref="OnPropertyChanged"/> und löst
    /// anschließend das <see cref="PropertyChanged"/>-Ereignis aus.
    /// </summary>
    /// <param name="propertyName">
    /// Name der geänderten Eigenschaft; wird standardmäßig vom Aufrufer per
    /// <see cref="CallerMemberNameAttribute"/> übernommen.
    /// </param>
    protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
        OnPropertyChanged(propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Erweiterungspunkt für abgeleitete Klassen, der vor dem Auslösen von
    /// <see cref="PropertyChanged"/> für die geänderte Eigenschaft aufgerufen wird.
    /// </summary>
    /// <param name="propertyName">Name der geänderten Eigenschaft.</param>
    protected virtual void OnPropertyChanged(string propertyName) {
            
    }

    /// <summary>
    /// Setzt das Hintergrundfeld einer Eigenschaft nur dann, wenn sich der Wert tatsächlich
    /// ändert, und meldet in diesem Fall die Änderung über <see cref="NotifyPropertyChanged"/>.
    /// </summary>
    /// <param name="field">Referenz auf das Hintergrundfeld der Eigenschaft.</param>
    /// <param name="value">Der neu zu setzende Wert.</param>
    /// <param name="propertyName">
    /// Name der Eigenschaft; wird standardmäßig per <see cref="CallerMemberNameAttribute"/> übernommen.
    /// </param>
    /// <returns><c>true</c>, wenn der Wert geändert wurde; andernfalls <c>false</c>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "") {
        if (!EqualityComparer<T>.Default.Equals(field, value)) {
            field = value;
            // ReSharper disable once ExplicitCallerInfoArgument
            NotifyPropertyChanged(propertyName);
            return true;
        }

        return false;
    }
}