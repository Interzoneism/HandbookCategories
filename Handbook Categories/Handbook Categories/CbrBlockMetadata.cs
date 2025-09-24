using Vintagestory.API.MathTools;

namespace Handbook_Categories;

public readonly struct CbrBlockMetadata
{
    public static readonly CbrBlockMetadata Empty = new(false, 0, UnderfootType.None, 0, false);

    public bool CbrEligible { get; }
    public int Tier { get; }
    public UnderfootType Underfoot { get; }
    public bool HasWallData { get; }

    private readonly byte _wallMask;

    public CbrBlockMetadata(bool eligible, int tier, UnderfootType underfoot, byte wallMask, bool hasWallData)
    {
        CbrEligible = eligible;
        Tier = tier < 0 ? 0 : tier;
        Underfoot = underfoot;
        _wallMask = wallMask;
        HasWallData = hasWallData;
    }

    public bool HasAnyWall => _wallMask != 0;

    public bool CountsAsWall(BlockFacing facing)
    {
        if (_wallMask == 0)
        {
            return false;
        }

        return (_wallMask & (1 << facing.Index)) != 0;
    }
}
