using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Handbook_Categories;

public class CbrOverlayManager : IDisposable
{
    private readonly ICoreClientAPI _capi;
    private readonly CbrConfig _config;
    private readonly CbrEvaluator _evaluator;
    private readonly Dictionary<GroundCellKey, GroundCellState> _cells = new();
    private readonly Queue<GroundCellKey> _pending = new();
    private readonly HashSet<GroundCellKey> _queued = new();
    private readonly List<BlockPos> _highlightBlocks = new();
    private readonly List<int> _highlightColors = new();
    private readonly int _overlayColor;
    private readonly int _highlightSlotId;

    private bool _disposed;
    private bool _highlightDirty;
    private bool _highlightsActive;
    private long _tickListenerId;
    private int _lastCenterX = int.MinValue;
    private int _lastCenterZ = int.MinValue;

    public CbrOverlayManager(ICoreClientAPI capi, CbrConfig config, CbrBlockMetadataProvider metadataProvider)
    {
        _capi = capi ?? throw new ArgumentNullException(nameof(capi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        if (metadataProvider == null) throw new ArgumentNullException(nameof(metadataProvider));

        _overlayColor = ParseOverlayColor(config.OverlayColor, ColorUtil.ColorFromRgba(64, 196, 128, 96));
        _highlightSlotId = _capi.World.AllocateBlockHighlightSlot();
        _evaluator = new CbrEvaluator(config, metadataProvider, capi.Logger);

        _tickListenerId = _capi.Event.RegisterGameTickListener(OnClientTick, 100);
        _capi.Event.BlockChanged += OnBlockChanged;
    }

    private void OnClientTick(float dt)
    {
        if (_disposed)
        {
            return;
        }

        if (!_config.CbrEnabled)
        {
            ClearHighlights();
            return;
        }

        IClientPlayer player = _capi.World.Player;
        if (player?.Entity == null)
        {
            ClearHighlights();
            return;
        }

        int centerX = GameMath.Floor(player.Entity.Pos.X);
        int centerZ = GameMath.Floor(player.Entity.Pos.Z);

        if (centerX != _lastCenterX || centerZ != _lastCenterZ || _cells.Count == 0)
        {
            RebuildCells(centerX, centerZ);
        }

        bool overlayActive = _config.OverlayEnabled;

        if (overlayActive)
        {
            ProcessPendingCells(_config.OverlayCellsPerTick);
            UpdateHighlights();
        }
        else
        {
            ClearHighlights();
        }
    }

    private void RebuildCells(int centerX, int centerZ)
    {
        _lastCenterX = centerX;
        _lastCenterZ = centerZ;

        _cells.Clear();
        _pending.Clear();
        _queued.Clear();

        int radius = _config.OverlayRadius;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) > radius)
                {
                    continue;
                }

                var key = new GroundCellKey(centerX + dx, centerZ + dz);
                _cells[key] = new GroundCellState();
                EnqueueCell(key);
            }
        }

        _highlightDirty = true;
    }

    private void ProcessPendingCells(int maxCount)
    {
        int processed = 0;
        while (_pending.Count > 0 && processed < maxCount)
        {
            GroundCellKey key = _pending.Dequeue();
            _queued.Remove(key);
            processed++;

            GroundCellState state = EvaluateCell(key);
            _cells[key] = state;
            _highlightDirty = true;
        }
    }

    private GroundCellState EvaluateCell(GroundCellKey key)
    {
        BlockPos terrainPos = new(key.X, 0, key.Z);
        int terrainHeight = _capi.World.BlockAccessor.GetTerrainMapheightAt(terrainPos);
        terrainHeight = GameMath.Clamp(terrainHeight, 0, _capi.World.BlockAccessor.MapSizeY - 1);

        BlockPos groundPos = new(key.X, terrainHeight, key.Z);
        bool covered = _evaluator.IsGroundProtectedByCbr(_capi.World.BlockAccessor, groundPos);

        return new GroundCellState
        {
            GroundPos = groundPos,
            Covered = covered
        };
    }

    private void UpdateHighlights()
    {
        if (!_highlightDirty)
        {
            return;
        }

        _highlightDirty = false;
        _highlightBlocks.Clear();
        _highlightColors.Clear();

        foreach (KeyValuePair<GroundCellKey, GroundCellState> entry in _cells)
        {
            if (!entry.Value.Covered)
            {
                continue;
            }

            BlockPos groundPos = entry.Value.GroundPos;
            if (groundPos == null)
            {
                continue;
            }

            _highlightBlocks.Add(groundPos.Copy());
            _highlightColors.Add(_overlayColor);
        }

        IClientPlayer player = _capi.World.Player;
        if (player == null)
        {
            _highlightBlocks.Clear();
            _highlightColors.Clear();
            _highlightsActive = false;
            return;
        }

        _highlightsActive = _highlightBlocks.Count > 0;
        _capi.World.HighlightBlocks(player, _highlightSlotId, _highlightBlocks, _highlightColors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cubes);
    }

    private void ClearHighlights()
    {
        if (!_highlightsActive && !_highlightDirty)
        {
            return;
        }

        IClientPlayer player = _capi.World.Player;
        if (player != null)
        {
            _highlightBlocks.Clear();
            _highlightColors.Clear();
            _capi.World.HighlightBlocks(player, _highlightSlotId, _highlightBlocks, _highlightColors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cubes);
        }
        else
        {
            _highlightBlocks.Clear();
            _highlightColors.Clear();
        }
        _highlightsActive = false;
        _highlightDirty = false;
    }

    private void OnBlockChanged(BlockPos pos, Block oldBlock)
    {
        if (_disposed)
        {
            return;
        }

        int radius = Math.Max(1, _config.MaxRangeCap);
        MarkAreaDirty(pos, radius);
    }

    private void MarkAreaDirty(BlockPos center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                int x = center.X + dx;
                int z = center.Z + dz;

                if (Math.Max(Math.Abs(x - _lastCenterX), Math.Abs(z - _lastCenterZ)) > _config.OverlayRadius)
                {
                    continue;
                }

                var key = new GroundCellKey(x, z);
                if (!_cells.ContainsKey(key))
                {
                    continue;
                }

                EnqueueCell(key);
            }
        }
    }

    private void EnqueueCell(GroundCellKey key)
    {
        if (_queued.Add(key))
        {
            _pending.Enqueue(key);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_tickListenerId != 0)
        {
            _capi.Event.UnregisterGameTickListener(_tickListenerId);
            _tickListenerId = 0;
        }

        _capi.Event.BlockChanged -= OnBlockChanged;
        ClearHighlights();
    }

    private static int ParseOverlayColor(string value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string hex = value.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgba))
        {
            byte r = (byte)((rgba >> 24) & 0xFF);
            byte g = (byte)((rgba >> 16) & 0xFF);
            byte b = (byte)((rgba >> 8) & 0xFF);
            byte a = (byte)(rgba & 0xFF);
            return ColorUtil.ColorFromRgba(r, g, b, a);
        }

        if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return ColorUtil.ColorFromRgba(r, g, b, 96);
        }

        return fallback;
    }

    private readonly struct GroundCellKey : IEquatable<GroundCellKey>
    {
        public readonly int X;
        public readonly int Z;

        public GroundCellKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GroundCellKey other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GroundCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
    }

    private class GroundCellState
    {
        public BlockPos GroundPos;
        public bool Covered;
    }
}
