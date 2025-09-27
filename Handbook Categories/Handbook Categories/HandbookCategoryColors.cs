using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Handbook_Categories
{
    internal static class HandbookCategoryColors
    {
        internal const string DefaultColorName = "default";

        private static readonly Dictionary<string, string> NamedColorHexValues = new(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultColorName] = "#403529",
            ["red"] = "#B24041",
            ["orange"] = "#D97706",
            ["amber"] = "#F59E0B",
            ["yellow"] = "#E9C46A",
            ["lime"] = "#A7C957",
            ["green"] = "#2A9D8F",
            ["teal"] = "#14B8A6",
            ["cyan"] = "#0891B2",
            ["blue"] = "#1D4ED8",
            ["indigo"] = "#4338CA",
            ["purple"] = "#7C3AED",
            ["violet"] = "#8B5CF6",
            ["pink"] = "#DB2777",
            ["brown"] = "#8B5E3C",
            ["gray"] = "#6B7280",
            ["black"] = "#111827",
            ["white"] = "#F9FAFB"
        };

        private static double DefaultAlpha => GuiStyle.DialogDefaultBgColor.Length > 3 ? GuiStyle.DialogDefaultBgColor[3] : 1.0;

        internal static double[] GetDefaultBackgroundColor()
        {
            return (double[])GuiStyle.DialogDefaultBgColor.Clone();
        }

        internal static double[] ResolveBackgroundColor(string value, out bool usedFallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                usedFallback = true;
                return GetDefaultBackgroundColor();
            }

            string trimmed = value.Trim();

            if (NamedColorHexValues.TryGetValue(trimmed, out string hex))
            {
                usedFallback = false;
                return ColorUtil.Hex2Doubles(hex, DefaultAlpha);
            }

            if (TryParseHex(trimmed, out double[] fromHex))
            {
                usedFallback = false;
                return fromHex;
            }

            if (TryParseRgb(trimmed, out double[] fromRgb))
            {
                usedFallback = false;
                return fromRgb;
            }

            usedFallback = true;
            return GetDefaultBackgroundColor();
        }

        internal static double[] ResolveBackgroundColorOrDefault(string value)
        {
            return ResolveBackgroundColor(value, out _);
        }

        private static bool TryParseHex(string value, out double[] color)
        {
            color = null;

            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (value.Length == 7)
                {
                    color = ColorUtil.Hex2Doubles(value, DefaultAlpha);
                    return true;
                }

                if (value.Length == 9)
                {
                    color = ColorUtil.Hex2Doubles(value);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryParseRgb(string value, out double[] color)
        {
            color = null;

            if (!value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int start = value.IndexOf('(');
            int end = value.LastIndexOf(')');
            if (start < 0 || end <= start)
            {
                return false;
            }

            string inner = value.Substring(start + 1, end - start - 1);
            string[] parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts.Length > 4)
            {
                return false;
            }

            double[] channels = new double[4];
            for (int i = 0; i < 3; i++)
            {
                if (!TryParseChannel(parts[i], out double component))
                {
                    return false;
                }

                channels[i] = component;
            }

            if (parts.Length == 4)
            {
                if (!TryParseChannel(parts[3], out double alpha))
                {
                    return false;
                }

                channels[3] = alpha;
            }
            else
            {
                channels[3] = DefaultAlpha;
            }

            color = channels;
            return true;
        }

        private static bool TryParseChannel(string value, out double component)
        {
            string trimmed = value.Trim();
            component = 0.0;

            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                if (!double.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double percentage))
                {
                    return false;
                }

                component = Math.Clamp(percentage / 100.0, 0.0, 1.0);
                return true;
            }

            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            component = raw > 1.0 ? Math.Clamp(raw / 255.0, 0.0, 1.0) : Math.Clamp(raw, 0.0, 1.0);
            return true;
        }
    }
}
