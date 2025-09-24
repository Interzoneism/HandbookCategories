using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Handbook_Categories;

public class CbrBlockMetadataProvider
{
    private readonly CbrConfig _config;
    private readonly Dictionary<int, CbrBlockMetadata> _cache = new();
    public CbrBlockMetadataProvider(ICoreAPI api, CbrConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void UpdateApi(ICoreAPI api)
    {
        _ = api;
        ClearCache();
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public CbrBlockMetadata Get(Block block)
    {
        if (block == null)
        {
            return CbrBlockMetadata.Empty;
        }

        if (_cache.TryGetValue(block.Id, out var cached))
        {
            return cached;
        }

        CbrBlockMetadata metadata = Build(block);
        _cache[block.Id] = metadata;
        return metadata;
    }

    private CbrBlockMetadata Build(Block block)
    {
        bool eligible = false;
        int tier = 0;
        UnderfootType underfoot = UnderfootType.None;
        bool hasWallData = false;
        byte wallMask = 0;

        JsonObject attributes = block.Attributes;

        if (attributes?.KeyExists("cbrEligible") == true)
        {
            eligible = attributes["cbrEligible"].AsBool(false);
        }

        if (attributes?.KeyExists("cbrTier") == true)
        {
            tier = Math.Max(0, attributes["cbrTier"].AsInt(0));
        }

        if (attributes?.KeyExists("noSpawnOnTop") == true)
        {
            underfoot = ParseUnderfoot(attributes["noSpawnOnTop"].AsString());
        }

        if (attributes?.KeyExists("cbrCountsAsWall") == true)
        {
            wallMask = ParseWallAttribute(attributes["cbrCountsAsWall"], ref hasWallData);
        }

        ApplyConfigRules(block, ref eligible, ref tier, ref underfoot, ref wallMask, ref hasWallData);

        if (!hasWallData)
        {
            wallMask = BuildDefaultWallMask(block);
        }

        return new CbrBlockMetadata(eligible, tier, underfoot, wallMask, hasWallData);
    }

    private void ApplyConfigRules(Block block, ref bool eligible, ref int tier, ref UnderfootType underfoot, ref byte wallMask, ref bool hasWallData)
    {
        if (_config.BlockRules == null || _config.BlockRules.Count == 0)
        {
            return;
        }

        string code = block.Code?.ToShortString();
        if (code == null)
        {
            return;
        }

        foreach (CbrConfig.BlockRule rule in _config.BlockRules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Wildcard))
            {
                continue;
            }

            if (!WildcardUtil.Match(rule.Wildcard, code))
            {
                continue;
            }

            if (rule.CbrEligible.HasValue)
            {
                eligible = rule.CbrEligible.Value;
            }

            if (rule.Tier.HasValue)
            {
                tier = Math.Max(0, rule.Tier.Value);
            }

            if (!string.IsNullOrEmpty(rule.Underfoot))
            {
                underfoot = ParseUnderfoot(rule.Underfoot);
            }

            if (rule.CountsAsWall.HasValue)
            {
                hasWallData = true;
                wallMask = rule.CountsAsWall.Value ? FullMask() : (byte)0;
            }
        }
    }

    private static byte BuildDefaultWallMask(Block block)
    {
        byte mask = 0;
        foreach (BlockFacing facing in BlockFacing.ALLFACES)
        {
            if (block.SideSolid[facing.Index])
            {
                mask |= (byte)(1 << facing.Index);
            }
        }

        return mask;
    }

    private static byte ParseWallAttribute(JsonObject attribute, ref bool hasWallData)
    {
        if (attribute == null || !attribute.Exists)
        {
            return 0;
        }

        if (attribute.Token is JObject)
        {
            byte mask = 0;
            bool any = false;

            JsonObject allToken = attribute["all"];
            if (allToken.Exists)
            {
                any = true;
                bool value = allToken.AsBool(false);
                hasWallData = true;
                return value ? FullMask() : (byte)0;
            }

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                JsonObject facingToken = attribute[facing.Code];
                if (!facingToken.Exists)
                {
                    continue;
                }

                any = true;
                if (facingToken.AsBool(false))
                {
                    mask |= (byte)(1 << facing.Index);
                }
            }

            if (any)
            {
                hasWallData = true;
            }

            return mask;
        }

        hasWallData = true;
        return attribute.AsBool(false) ? FullMask() : (byte)0;
    }

    private static byte FullMask()
    {
        byte mask = 0;
        foreach (BlockFacing facing in BlockFacing.ALLFACES)
        {
            mask |= (byte)(1 << facing.Index);
        }

        return mask;
    }

    internal static UnderfootType ParseUnderfoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UnderfootType.None;
        }

        string lowered = value.Trim().ToLowerInvariant();

        if (lowered.Contains("premium"))
        {
            return UnderfootType.Premium;
        }

        if (lowered.Contains("cheap"))
        {
            return UnderfootType.Cheap;
        }

        if (lowered.Contains("none"))
        {
            return UnderfootType.None;
        }

        return lowered switch
        {
            "premium" => UnderfootType.Premium,
            "premium-underfoot" => UnderfootType.Premium,
            "cheap" => UnderfootType.Cheap,
            "cheap-underfoot" => UnderfootType.Cheap,
            _ => UnderfootType.None
        };
    }
}
