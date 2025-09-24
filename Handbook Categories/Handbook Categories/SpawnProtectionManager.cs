using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Handbook_Categories;

public class SpawnProtectionManager : IDisposable
{
    private readonly ICoreServerAPI _api;
    private readonly CbrEvaluator _evaluator;
    private bool _disposed;

    public SpawnProtectionManager(ICoreServerAPI api, CbrConfig config, CbrBlockMetadataProvider metadataProvider)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _evaluator = new CbrEvaluator(config ?? throw new ArgumentNullException(nameof(config)), metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider)), api.Logger);
        _api.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
    }

    private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
    {
        RuntimeSpawnConditions runtime = properties?.Server?.SpawnConditions?.Runtime;
        bool deny = _evaluator.ShouldDenySpawn(blockAccessor, spawnPosition, properties, runtime);
        return !deny;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _api.Event.OnTrySpawnEntity -= Event_OnTrySpawnEntity;
        _disposed = true;
    }
}
