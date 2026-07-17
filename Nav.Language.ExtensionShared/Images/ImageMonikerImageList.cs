#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Images; 

/// <summary>
/// Stellt eine feste Folge von <see cref="ImageMoniker"/>s als <see cref="IVsImageMonikerImageList"/>
/// bereit — die von Visual Studio erwartete Icon-Liste (etwa für Baum-/Listenansichten), die Icons
/// über einen fortlaufenden Index adressiert.
/// </summary>
sealed class ImageMonikerImageList: IVsImageMonikerImageList {

    readonly ImmutableList<ImageMoniker> _imageMonikers;

    /// <summary>
    /// Erzeugt die Icon-Liste aus einer <see cref="IEnumerable{T}"/> von <see cref="ImageMoniker"/>s;
    /// deren Reihenfolge bestimmt die Indizierung.
    /// </summary>
    public ImageMonikerImageList(IEnumerable<ImageMoniker> imageMonikers) {
        _imageMonikers = imageMonikers.ToImmutableList();
    }

    /// <summary>
    /// Erzeugt die Icon-Liste aus den übergebenen <see cref="ImageMoniker"/>s; deren Reihenfolge
    /// bestimmt die Indizierung.
    /// </summary>
    public ImageMonikerImageList(params ImageMoniker[] imageMonikers) {
        _imageMonikers = imageMonikers.ToImmutableList();
    }

    /// <summary>
    /// Kopiert <paramref name="imageMonikerCount"/> Icons ab <paramref name="firstImageIndex"/> in das
    /// Zielarray <paramref name="imageMonikers"/> (VS-SDK-Vertrag).
    /// </summary>
    /// <param name="firstImageIndex">Der Index des ersten zu liefernden Icons.</param>
    /// <param name="imageMonikerCount">Die Anzahl zu liefernder Icons.</param>
    /// <param name="imageMonikers">Das aufzufüllende Zielarray.</param>
    public void GetImageMonikers(int firstImageIndex, int imageMonikerCount, ImageMoniker[] imageMonikers) {
        for (int index = 0; index < imageMonikerCount; index++) {
            imageMonikers[index] = _imageMonikers[index + firstImageIndex];
        }
    }

    /// <summary>Die Anzahl der Icons in dieser Liste.</summary>
    public int ImageCount {
        get { return _imageMonikers.Count; }
    }
}