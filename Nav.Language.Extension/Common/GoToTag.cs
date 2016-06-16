#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.CodeAnalysis;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common {

    public abstract class GoToTag: ITag {

        [NotNull]
        public abstract Task<LocationResult> GetLocationAsync(CancellationToken cancellationToken=default(CancellationToken));
    }
}