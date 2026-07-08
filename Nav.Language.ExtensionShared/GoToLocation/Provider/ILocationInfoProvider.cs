#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

public interface ILocationInfoProvider {

    [NotNull]
    Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = default);        
}