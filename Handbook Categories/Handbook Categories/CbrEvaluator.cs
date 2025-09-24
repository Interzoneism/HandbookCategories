using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Handbook_Categories;

public class CbrEvaluator
{
    private readonly CbrConfig _config;
    private readonly CbrBlockMetadataProvider _metadataProvider;
    private readonly ILogger _logger;

    public CbrEvaluator(CbrConfig config, CbrBlockMetadataProvider metadataProvider, ILogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _logger = logger;
    }

    public bool ShouldDenySpawn(IBlockAccessor accessor, Vec3d spawnPosition, EntityProperties properties, RuntimeSpawnConditions runtime)
    {
        if (accessor == null)
        {
            return false;
        }

        BlockPos spawnPos = ToBlockPos(spawnPosition);
        BlockPos groundPos = spawnPos.DownCopy();

        if (!accessor.IsValidPos(groundPos))
        {
            return false;
        }

        Block groundBlock = accessor.GetBlock(groundPos);
        CbrBlockMetadata groundMeta = _metadataProvider.Get(groundBlock);
        bool hasValidGround = DetermineValidGround(accessor, groundPos, groundBlock, properties, runtime);

        if (!hasValidGround)
        {
            return false;
        }

        if (EvaluateUnderfoot(accessor, groundPos, groundBlock, groundMeta))
        {
            return true;
        }

        if (!_config.CbrEnabled || _config.MaxRangeCap <= 0)
        {
            return false;
        }

        return EvaluateCbr(accessor, groundPos);
    }

    public bool IsGroundProtectedByCbr(IBlockAccessor accessor, BlockPos groundPos)
    {
        if (!_config.CbrEnabled || _config.MaxRangeCap <= 0)
        {
            return false;
        }

        if (accessor == null || groundPos == null || !accessor.IsValidPos(groundPos))
        {
            return false;
        }

        Block groundBlock = accessor.GetBlock(groundPos);
        if (!DetermineValidGround(accessor, groundPos, groundBlock, null, null))
        {
            return false;
        }

        return EvaluateCbr(accessor, groundPos);
    }

    private bool EvaluateUnderfoot(IBlockAccessor accessor, BlockPos groundPos, Block groundBlock, CbrBlockMetadata groundMeta)
    {
        switch (groundMeta.Underfoot)
        {
            case UnderfootType.Premium:
                return true;
            case UnderfootType.Cheap:
                return PerformFourRay(accessor, groundPos);
            default:
                return false;
        }
    }

    private bool PerformFourRay(IBlockAccessor accessor, BlockPos groundPos)
    {
        if (_config.RayHitsRequired <= 0)
        {
            return true;
        }

        if (_config.RayRange <= 0)
        {
            return false;
        }

        int hits = 0;
        BlockPos checkPos = new();

        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
        {
            bool rayHit = false;

            for (int step = 1; step <= _config.RayRange; step++)
            {
                int baseX = groundPos.X + facing.Normali.X * step;
                int baseZ = groundPos.Z + facing.Normali.Z * step;

                for (int offsetY = _config.RayHeightMin; offsetY <= _config.RayHeightMax; offsetY++)
                {
                    checkPos.Set(baseX, groundPos.Y + offsetY, baseZ);
                    if (!accessor.IsValidPos(checkPos))
                    {
                        continue;
                    }

                    Block block = accessor.GetBlock(checkPos);
                    if (BlockCountsAsWall(accessor, block, checkPos, facing.Opposite))
                    {
                        rayHit = true;
                        break;
                    }
                }

                if (rayHit)
                {
                    break;
                }
            }

            if (rayHit)
            {
                hits++;
                if (hits >= _config.RayHitsRequired)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool EvaluateCbr(IBlockAccessor accessor, BlockPos groundPos)
    {
        int radius = _config.MaxRangeCap;
        if (radius <= 0)
        {
            return false;
        }

        int yMin = Math.Max(0, groundPos.Y - _config.VerticalSearchDown);
        int yMax = Math.Min(accessor.MapSizeY - 1, groundPos.Y + _config.VerticalSearchUp);

        BlockPos emitterPos = new();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                int horizontal = Math.Max(Math.Abs(dx), Math.Abs(dz));
                if (horizontal > radius)
                {
                    continue;
                }

                emitterPos.Set(groundPos.X + dx, groundPos.Y, groundPos.Z + dz);

                for (int y = yMin; y <= yMax; y++)
                {
                    emitterPos.Y = y;
                    if (!accessor.IsValidPos(emitterPos))
                    {
                        continue;
                    }

                    Block block = accessor.GetBlock(emitterPos);
                    CbrBlockMetadata emitterMeta = _metadataProvider.Get(block);
                    if (!emitterMeta.CbrEligible)
                    {
                        continue;
                    }

                    int range = ComputeCbrRange(accessor, emitterPos, emitterMeta);
                    if (range <= 0)
                    {
                        continue;
                    }

                    if (horizontal <= range)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private int ComputeCbrRange(IBlockAccessor accessor, BlockPos emitterPos, CbrBlockMetadata emitterMeta)
    {
        if (!emitterMeta.CbrEligible || emitterMeta.Tier <= 0)
        {
            return 0;
        }

        bool connected = false;
        int maxTier = emitterMeta.Tier;
        BlockPos neighborPos = new();

        foreach (BlockFacing facing in BlockFacing.ALLFACES)
        {
            neighborPos.Set(emitterPos.X + facing.Normali.X, emitterPos.Y + facing.Normali.Y, emitterPos.Z + facing.Normali.Z);
            if (!accessor.IsValidPos(neighborPos))
            {
                continue;
            }

            Block neighborBlock = accessor.GetBlock(neighborPos);
            CbrBlockMetadata neighborMeta = _metadataProvider.Get(neighborBlock);
            if (!neighborMeta.CbrEligible)
            {
                continue;
            }

            connected = true;
            if (neighborMeta.Tier > maxTier)
            {
                maxTier = neighborMeta.Tier;
            }
        }

        if (!connected)
        {
            return 0;
        }

        int baseRange = emitterMeta.Tier == 1 ? 1 : emitterMeta.Tier;
        if (baseRange <= 0)
        {
            return 0;
        }

        int range = Math.Max(baseRange, maxTier);
        if (_config.MaxRangeCap > 0 && range > _config.MaxRangeCap)
        {
            range = _config.MaxRangeCap;
        }

        return range;
    }

    private bool DetermineValidGround(IBlockAccessor accessor, BlockPos groundPos, Block groundBlock, EntityProperties properties, RuntimeSpawnConditions runtime)
    {
        if (groundBlock == null || groundBlock.BlockId == 0)
        {
            return false;
        }

        if (groundBlock.Replaceable >= 6000 && (groundBlock.CollisionBoxes == null || groundBlock.CollisionBoxes.Length == 0))
        {
            return false;
        }

        try
        {
            if (properties != null || runtime != null)
            {
                if (!groundBlock.CanCreatureSpawnOn(accessor, groundPos, properties, runtime))
                {
                    return false;
                }
            }
            else if (!groundBlock.SideSolid[BlockFacing.UP.Index])
            {
                Cuboidf[] boxes = groundBlock.GetCollisionBoxes(accessor, groundPos);
                if (boxes == null || boxes.Length == 0)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning("CBR: Exception while evaluating ground for {0}: {1}", groundBlock?.Code, ex);
            return false;
        }

        return true;
    }

    private bool BlockCountsAsWall(IBlockAccessor accessor, Block block, BlockPos blockPos, BlockFacing facing)
    {
        if (block == null || block.BlockId == 0)
        {
            return false;
        }

        CbrBlockMetadata meta = _metadataProvider.Get(block);
        if (meta.HasAnyWall && meta.CountsAsWall(facing))
        {
            return true;
        }

        if (meta.HasWallData)
        {
            return false;
        }

        if (block.Replaceable >= 6000)
        {
            return false;
        }

        if (block.SideSolid[facing.Index])
        {
            return true;
        }

        if (block.SideSolid[facing.Opposite.Index])
        {
            return true;
        }

        try
        {
            Cuboidf[] boxes = block.GetCollisionBoxes(accessor, blockPos);
            return boxes != null && boxes.Length > 0;
        }
        catch (Exception ex)
        {
            _logger?.Warning("CBR: Exception while checking wall collision for {0}: {1}", block.Code, ex);
            return false;
        }
    }

    private static BlockPos ToBlockPos(Vec3d position)
    {
        return new BlockPos(GameMath.Floor(position.X), GameMath.Floor(position.Y), GameMath.Floor(position.Z));
    }
}
