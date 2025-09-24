using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Handbook_Categories;

public class Handbook_CategoriesModSystem : ModSystem
{
    private CbrConfig _config;
    private CbrBlockMetadataProvider _metadataProvider;
    private SpawnProtectionManager _serverManager;
    private CbrOverlayManager _overlayManager;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        _config = api.LoadModConfig<CbrConfig>(CbrConfig.ConfigFileName) ?? new CbrConfig();
        _config.EnsureValidity();
        api.StoreModConfig(_config, CbrConfig.ConfigFileName);

        _metadataProvider = new CbrBlockMetadataProvider(api, _config);
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        _metadataProvider?.ClearCache();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        EnsureMetadataProvider(api);
        _serverManager = new SpawnProtectionManager(api, _config, _metadataProvider);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        EnsureMetadataProvider(api);
        _overlayManager = new CbrOverlayManager(api, _config, _metadataProvider);
    }

    public override void Dispose()
    {
        base.Dispose();
        _overlayManager?.Dispose();
        _overlayManager = null;
        _serverManager?.Dispose();
        _serverManager = null;
        _metadataProvider = null;
    }

    private void EnsureMetadataProvider(ICoreAPI api)
    {
        if (_metadataProvider == null)
        {
            _metadataProvider = new CbrBlockMetadataProvider(api, _config);
        }
        else
        {
            _metadataProvider.UpdateApi(api);
        }
    }
}
