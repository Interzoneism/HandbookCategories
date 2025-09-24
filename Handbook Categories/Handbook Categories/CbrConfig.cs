using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Handbook_Categories;

public class CbrConfig
{
    public const string ConfigFileName = "ConnectedBlockRange.json";

    public bool CbrEnabled { get; set; } = true;
    public int MaxRangeCap { get; set; } = 5;
    public int RayHitsRequired { get; set; } = 3;
    public int RayRange { get; set; } = 6;
    public int RayHeightMin { get; set; } = 1;
    public int RayHeightMax { get; set; } = 2;
    public bool OverlayEnabled { get; set; } = true;
    public int OverlayRadius { get; set; } = 14;
    public int OverlayCellsPerTick { get; set; } = 180;
    public string OverlayColor { get; set; } = "40C46080";
    public int VerticalSearchUp { get; set; } = 64;
    public int VerticalSearchDown { get; set; } = 48;
    public List<BlockRule> BlockRules { get; set; } = new();

    public void EnsureValidity()
    {
        MaxRangeCap = GameMath.Clamp(MaxRangeCap, 0, 64);
        RayRange = GameMath.Clamp(RayRange, 0, 64);
        RayHitsRequired = GameMath.Clamp(RayHitsRequired, 0, 4);
        if (RayHeightMin < 0) RayHeightMin = 0;
        if (RayHeightMax < 0) RayHeightMax = 0;
        if (RayHeightMin > RayHeightMax)
        {
            (RayHeightMin, RayHeightMax) = (RayHeightMax, RayHeightMin);
        }
        OverlayRadius = GameMath.Clamp(OverlayRadius, 0, 64);
        OverlayCellsPerTick = GameMath.Clamp(OverlayCellsPerTick, 1, 4096);
        VerticalSearchUp = GameMath.Clamp(VerticalSearchUp, 0, 256);
        VerticalSearchDown = GameMath.Clamp(VerticalSearchDown, 0, 256);
        BlockRules ??= new List<BlockRule>();
    }

    public class BlockRule
    {
        public string Wildcard { get; set; }
        public bool? CbrEligible { get; set; }
        public int? Tier { get; set; }
        public string Underfoot { get; set; }
        public bool? CountsAsWall { get; set; }
    }
}
