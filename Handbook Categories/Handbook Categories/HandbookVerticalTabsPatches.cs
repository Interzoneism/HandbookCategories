using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace Handbook_Categories
{
    internal static class HandbookVerticalTabsPatches
    {
        private const double DefaultUnscaledTabHeight = 25.0;
        private const double DefaultUnscaledTabSpacing = 5.0;
        private const double MinUnscaledTabHeight = 6.0;
        private const double MinUnscaledTabSpacing = 0.0;
        private const double EmergencyMinUnscaledTabHeight = 3.0;

        private static readonly System.Reflection.FieldInfo TabsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabs");
        private static readonly System.Reflection.FieldInfo UnscaledTabHeightField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabHeight");
        private static readonly System.Reflection.FieldInfo UnscaledTabSpacingField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabSpacing");

        public static void AdjustTabLayout(GuiElementVerticalTabs __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (TabsField?.GetValue(__instance) is not GuiTab[] tabs || tabs.Length == 0)
            {
                return;
            }

            bool containsHandbookTab = false;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] is HandbookTab)
                {
                    containsHandbookTab = true;
                    break;
                }
            }

            if (!containsHandbookTab)
            {
                return;
            }

            double availableHeight = __instance.Bounds?.InnerHeight ?? 0.0;
            if (availableHeight <= 0.0)
            {
                return;
            }

            double totalPadding = 0.0;
            for (int i = 0; i < tabs.Length; i++)
            {
                totalPadding += Math.Max(0.0, tabs[i].PaddingTop);
            }

            double contentHeight = Math.Max(0.0, availableHeight - totalPadding);
            if (contentHeight <= 0.0)
            {
                UnscaledTabHeightField?.SetValue(__instance, MinUnscaledTabHeight);
                UnscaledTabSpacingField?.SetValue(__instance, MinUnscaledTabSpacing);
                return;
            }

            double scale = GuiElement.scaled(1.0);
            if (scale <= 0.0)
            {
                scale = 1.0;
            }

            double contentHeightUnscaled = contentHeight / scale;
            if (contentHeightUnscaled <= 0.0)
            {
                UnscaledTabHeightField?.SetValue(__instance, MinUnscaledTabHeight);
                UnscaledTabSpacingField?.SetValue(__instance, MinUnscaledTabSpacing);
                return;
            }

            double availablePerTab = contentHeightUnscaled / tabs.Length;
            double perTabDefault = DefaultUnscaledTabHeight + DefaultUnscaledTabSpacing;
            double heightContribution = DefaultUnscaledTabHeight / perTabDefault;
            double spacingContribution = DefaultUnscaledTabSpacing / perTabDefault;

            double newHeight = availablePerTab * heightContribution;
            double newSpacing = availablePerTab * spacingContribution;

            newHeight = Math.Clamp(newHeight, MinUnscaledTabHeight, DefaultUnscaledTabHeight);
            newSpacing = Math.Clamp(newSpacing, MinUnscaledTabSpacing, DefaultUnscaledTabSpacing);

            double perTabCurrent = newHeight + newSpacing;
            double totalNeeded = perTabCurrent * tabs.Length;

            if (totalNeeded > contentHeightUnscaled)
            {
                double overflow = totalNeeded - contentHeightUnscaled;

                double reducibleSpacingTotal = (newSpacing - MinUnscaledTabSpacing) * tabs.Length;
                if (reducibleSpacingTotal > 0.0)
                {
                    double reduceSpacing = Math.Min(overflow, reducibleSpacingTotal);
                    newSpacing -= reduceSpacing / tabs.Length;
                    overflow -= reduceSpacing;
                }

                double reducibleHeightTotal = (newHeight - MinUnscaledTabHeight) * tabs.Length;
                if (overflow > 0.0 && reducibleHeightTotal > 0.0)
                {
                    double reduceHeight = Math.Min(overflow, reducibleHeightTotal);
                    newHeight -= reduceHeight / tabs.Length;
                    overflow -= reduceHeight;
                }

                if (overflow > 0.0)
                {
                    double extraReductionPerTab = overflow / tabs.Length;
                    newHeight = Math.Max(EmergencyMinUnscaledTabHeight, newHeight - extraReductionPerTab);
                }
            }

            newHeight = Math.Clamp(newHeight, EmergencyMinUnscaledTabHeight, DefaultUnscaledTabHeight);
            newSpacing = Math.Clamp(newSpacing, MinUnscaledTabSpacing, DefaultUnscaledTabSpacing);

            UnscaledTabHeightField?.SetValue(__instance, newHeight);
            UnscaledTabSpacingField?.SetValue(__instance, newSpacing);
        }
    }
}

