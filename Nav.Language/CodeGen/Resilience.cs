using System;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Stellt eine einfache Resilienz-Implementierung bereit.
/// </summary>
public static class Resilience {

    /// <summary>
    /// Führt die angegebene Funktion mit einer bestimmten Anzahl von Versuchen aus.
    /// </summary>
    /// <typeparam name="T">Der Rückgabewert der Funktion.</typeparam>
    /// <param name="func">Die auszuführende Funktion.</param>
    /// <param name="maxAttempts">Die maximale Anzahl der Versuche.</param>
    /// <param name="retryDelay">Die optionale Verzögerung zwischen den Versuchen.</param>
    /// <returns>Der Rückgabewert der Funktion.</returns>
    /// <exception cref="ArgumentNullException">Wird ausgelöst, wenn die Funktion null ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wird ausgelöst, wenn die maximale Anzahl der Versuche kleiner als 0 ist.</exception>
    public static T Execute<T>(Func<T> func, int maxAttempts, TimeSpan? retryDelay = null) {

        if (func == null) {
            throw new ArgumentNullException(nameof(func));
        }

        if (maxAttempts < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        var attempts = 0;

        while (true) {

            try {
                return func();

            } catch (Exception) {

                attempts++;

                if (attempts >= maxAttempts) {
                    throw;
                }

                if (retryDelay != null) {
                    System.Threading.Thread.Sleep(retryDelay.Value);

                }

            }
        }
    }

}