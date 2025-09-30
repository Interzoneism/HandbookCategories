using System;
using System.Linq;
using System.Reflection;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;

namespace Enhanced_Handbook
{
    internal static class GuiElementVerticalTabsWithBackgrounds
    {
        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabs");
        private static readonly FieldInfo TabWidthsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabWidths");
        private static readonly FieldInfo TabHeightField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabHeight");
        private static readonly FieldInfo TextOffsetYField = AccessTools.Field(typeof(GuiElementVerticalTabs), "textOffsetY");
        private static readonly FieldInfo UnscaledTabSpacingField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabSpacing");
        private static readonly FieldInfo UnscaledTabPaddingField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabPadding");
        private static readonly FieldInfo UnscaledTabHeightField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabHeight");
        private static readonly FieldInfo SelectedFontField = AccessTools.Field(typeof(GuiElementVerticalTabs), "selectedFont");
        private static readonly FieldInfo HoverTexturesField = AccessTools.Field(typeof(GuiElementVerticalTabs), "hoverTextures");
        private static readonly FieldInfo BaseTextureField = AccessTools.Field(typeof(GuiElementVerticalTabs), "baseTexture");
        private static readonly FieldInfo ApiField = AccessTools.Field(typeof(GuiElement), "api");

        public static bool TryCompose(GuiElementVerticalTabs element)
        {
            if (TabsField?.GetValue(element) is not GuiTab[] tabs || tabs.Length == 0)
            {
                return false;
            }

            if (!tabs.Any(tab => tab is IHandbookTabBackground))
            {
                return false;
            }

            ElementBounds bounds = element.Bounds;
            bounds.CalcWorldBounds();

            using var surface = new ImageSurface(Format.Argb32, (int)bounds.InnerWidth + 1, (int)bounds.InnerHeight + 1);
            using var ctx = new Context(surface);

            if (ApiField?.GetValue(element) is not ICoreClientAPI api)
            {
                return false;
            }

            double outlineThickness = GuiElement.scaled(1.0);
            double tabSpacing = GuiElement.scaled((double)(UnscaledTabSpacingField?.GetValue(element) ?? 5.0));
            double tabPadding = GuiElement.scaled((double)(UnscaledTabPaddingField?.GetValue(element) ?? 3.0));
            double tabHeight = GuiElement.scaled((double)(UnscaledTabHeightField?.GetValue(element) ?? 25.0));
            TabHeightField?.SetValue(element, tabHeight);

            CairoFont font = element.Font;
            font.Color[3] = 0.85;

            double tabHeightWithBorder = tabHeight + 1.0;
            font.SetupContext(ctx);
            FontExtents fontExtents = font.GetFontExtents();
            double textOffsetY = (tabHeightWithBorder - fontExtents.Height) / 2.0;
            TextOffsetYField?.SetValue(element, textOffsetY);

            int[] tabWidths = GetTabWidths(element, tabs.Length);

            double maxTabWidth = 0.0;
            foreach (GuiTab tab in tabs)
            {
                if (tab == null)
                {
                    continue;
                }

                TextExtents extents = ctx.TextExtents(tab.Name ?? string.Empty);
                double width = extents.Width + 1.0 + 2.0 * tabPadding;
                if (width > maxTabWidth)
                {
                    maxTabWidth = width;
                }
            }

            double currentY = 0.0;
            bool right = element.Right;

            for (int i = 0; i < tabs.Length; i++)
            {
                GuiTab tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                tabWidths[i] = (int)maxTabWidth + 1;
                currentY += tab.PaddingTop;

                double xStart;
                ctx.NewPath();
                if (right)
                {
                    xStart = 1.0;
                    ctx.MoveTo(xStart, currentY + tabHeight);
                    ctx.LineTo(xStart, currentY);
                    ctx.LineTo(xStart + tabWidths[i] + outlineThickness, currentY);
                    ctx.ArcNegative(xStart + tabWidths[i], currentY + outlineThickness, outlineThickness, 4.71238899230957, 3.1415927410125732);
                    ctx.ArcNegative(xStart + tabWidths[i], currentY - outlineThickness + tabHeight, outlineThickness, 3.1415927410125732, 1.5707963705062866);
                }
                else
                {
                    xStart = (int)bounds.InnerWidth + 1;
                    ctx.MoveTo(xStart, currentY + tabHeight);
                    ctx.LineTo(xStart, currentY);
                    ctx.LineTo(xStart - tabWidths[i] + outlineThickness, currentY);
                    ctx.ArcNegative(xStart - tabWidths[i], currentY + outlineThickness, outlineThickness, 4.71238899230957, 3.1415927410125732);
                    ctx.ArcNegative(xStart - tabWidths[i], currentY - outlineThickness + tabHeight, outlineThickness, 3.1415927410125732, 1.5707963705062866);
                }

                ctx.ClosePath();

                double[] color = GetBackgroundColor(tab);
                ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
                ctx.FillPreserve();
                element.ShadePath(ctx);

                font.SetupContext(ctx);
                element.DrawTextLineAt(ctx, tab.Name ?? string.Empty, xStart - (!right ? tabWidths[i] : 0) + tabPadding, currentY + textOffsetY);

                currentY += tabHeight + tabSpacing;
            }

            font.Color[3] = 1.0;
            ComposeOverlays(element, api, tabs, tabWidths, tabPadding, outlineThickness, tabHeight, textOffsetY);

            if (BaseTextureField?.GetValue(element) is LoadedTexture baseTexture)
            {
                api.Gui.LoadOrUpdateCairoTexture(surface, true, ref baseTexture);
                BaseTextureField.SetValue(element, baseTexture);
            }

            return true;
        }

        private static int[] GetTabWidths(GuiElementVerticalTabs element, int length)
        {
            if (TabWidthsField?.GetValue(element) is int[] widths && widths.Length == length)
            {
                return widths;
            }

            widths = new int[length];
            TabWidthsField?.SetValue(element, widths);
            return widths;
        }

        private static void ComposeOverlays(GuiElementVerticalTabs element, ICoreClientAPI api, GuiTab[] tabs, int[] tabWidths, double tabPadding, double outlineThickness, double tabHeight, double textOffsetY)
        {
            if (HoverTexturesField?.GetValue(element) is not LoadedTexture[] hoverTextures || hoverTextures.Length != tabs.Length)
            {
                return;
            }

            if (SelectedFontField?.GetValue(element) is not CairoFont selectedFont)
            {
                return;
            }

            bool right = element.Right;

            for (int i = 0; i < tabs.Length; i++)
            {
                GuiTab tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                using var surface = new ImageSurface(Format.Argb32, tabWidths[i] + 1, (int)tabHeight + 1);
                using var ctx = GuiElement.GenContext(surface);

                double width = tabWidths[i] + 1;
                ctx.SetSourceRGBA(1.0, 1.0, 1.0, 0.0);
                ctx.Paint();
                ctx.NewPath();
                ctx.MoveTo(width, tabHeight + 1.0);
                ctx.LineTo(width, 0.0);
                ctx.LineTo(outlineThickness, 0.0);
                ctx.ArcNegative(0.0, outlineThickness, outlineThickness, 4.71238899230957, 3.1415927410125732);
                ctx.ArcNegative(0.0, tabHeight - outlineThickness, outlineThickness, 3.1415927410125732, 1.5707963705062866);
                ctx.ClosePath();

                double[] color = GetBackgroundColor(tab);
                ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
                ctx.Fill();

                ctx.NewPath();
                if (right)
                {
                    ctx.LineTo(1.0, 1.0);
                    ctx.LineTo(width, 1.0);
                    ctx.LineTo(width, tabHeight);
                    ctx.LineTo(1.0, tabHeight - 1.0);
                }
                else
                {
                    ctx.LineTo(1.0 + width, 1.0);
                    ctx.LineTo(1.0, 1.0);
                    ctx.LineTo(1.0, tabHeight - 1.0);
                    ctx.LineTo(1.0 + width, tabHeight);
                }

                const float borderWidth = 2f;
                ctx.SetSourceRGBA(GuiStyle.DialogLightBgColor[0] * 1.6, GuiStyle.DialogStrongBgColor[1] * 1.6, GuiStyle.DialogStrongBgColor[2] * 1.6, 1.0);
                ctx.LineWidth = borderWidth * 1.75;
                ctx.StrokePreserve();
                SurfaceTransformBlur.BlurPartial(surface, 8.0, 16);
                ctx.SetSourceRGBA(0.17647058823529413, 7.0 / 51.0, 11.0 / 85.0, 1.0);
                ctx.LineWidth = borderWidth;
                ctx.Stroke();

                selectedFont.SetupContext(ctx);
                element.DrawTextLineAt(ctx, tab.Name ?? string.Empty, tabPadding + 2.0, textOffsetY);

                api.Gui.LoadOrUpdateCairoTexture(surface, true, ref hoverTextures[i]);
            }
        }

        private static double[] GetBackgroundColor(GuiTab tab)
        {
            if (tab is IHandbookTabBackground colored && colored.BackgroundColor is { Length: 4 } values)
            {
                return values;
            }

            return GuiStyle.DialogDefaultBgColor;
        }
    }
}
