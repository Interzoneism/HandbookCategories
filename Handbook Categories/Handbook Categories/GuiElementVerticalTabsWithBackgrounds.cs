using System;
using Cairo;
using Vintagestory.API.Client;

namespace Handbook_Categories
{
    internal sealed class GuiElementVerticalTabsWithBackgrounds : GuiElementVerticalTabs
    {
        public GuiElementVerticalTabsWithBackgrounds(ICoreClientAPI capi, GuiTab[] tabs, CairoFont font, CairoFont selectedFont, ElementBounds bounds, Action<int, GuiTab> onTabClicked)
            : base(capi, tabs, font, selectedFont, bounds, onTabClicked)
        {
        }

        public override void ComposeTextElements(Context ctxStatic, ImageSurface surfaceStatic)
        {
            Bounds.CalcWorldBounds();

            using var surface = new ImageSurface(Format.Argb32, (int)Bounds.InnerWidth + 1, (int)Bounds.InnerHeight + 1);
            using var ctx = new Context(surface);

            double outlineThickness = GuiElement.scaled(1.0);
            double tabSpacing = GuiElement.scaled(unscaledTabSpacing);
            double tabPadding = GuiElement.scaled(unscaledTabPadding);
            tabHeight = GuiElement.scaled(unscaledTabHeight);

            Font.Color[3] = 0.85;

            double tabHeightWithBorder = tabHeight + 1.0;
            Font.SetupContext(ctx);
            FontExtents fontExtents = Font.GetFontExtents();
            textOffsetY = (tabHeightWithBorder - fontExtents.Height) / 2.0;

            double maxTabWidth = 0.0;
            for (int i = 0; i < tabs.Length; i++)
            {
                GuiTab tab = tabs[i];
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
                if (Right)
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
                    xStart = (int)Bounds.InnerWidth + 1;
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
                ShadePath(ctx);

                Font.SetupContext(ctx);
                DrawTextLineAt(ctx, tab.Name ?? string.Empty, xStart - (!Right ? tabWidths[i] : 0) + tabPadding, currentY + textOffsetY);

                currentY += tabHeight + tabSpacing;
            }

            Font.Color[3] = 1.0;
            ComposeOverlaysWithBackgrounds(tabPadding, outlineThickness);
            generateTexture(surface, ref baseTexture);
        }

        private void ComposeOverlaysWithBackgrounds(double tabPadding, double outlineThickness)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                GuiTab tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                using var surface = new ImageSurface(Format.Argb32, tabWidths[i] + 1, (int)tabHeight + 1);
                using var ctx = genContext(surface);

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
                if (Right)
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
                DrawTextLineAt(ctx, tab.Name ?? string.Empty, tabPadding + 2.0, textOffsetY);

                generateTexture(surface, ref hoverTextures[i]);
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
