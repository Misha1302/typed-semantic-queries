using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.TinyLicm;

public sealed class TinyLicmPass
{
    private static readonly CanHoistQuery Query = new();

    public LoadOperation Run(LoadOperation load, SemanticSession session)
    {
        var result = session.Query(Query, new CanHoistKey(load.Location, load.LoopPoint));
        return result.Status == QueryStatus.Known && result.Value!.Proven
            ? load with { Hoisted = true }
            : load;
    }
}
