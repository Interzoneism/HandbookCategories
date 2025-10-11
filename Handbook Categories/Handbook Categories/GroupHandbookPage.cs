using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Enhanced_Handbook
{
    internal sealed class GroupHandbookPage : GuiHandbookPage
    {
        private const double TextOffsetX = 12.0;
        private const double TextOffsetY = 7.0;

        private readonly string pageCode;
        private readonly string hiddenCategoryCode;
        private readonly string displayCategoryCode;
        private readonly List<GuiHandbookPage> members;

        private string displayName;
        private string normalizedName;
        private string listDisplayText;
        private string searchableText;
        private LoadedTexture titleTexture;
        private DummySlot iconSlot;
        private GuiHandbookPage weightSourcePage;
        private int sortOrderHint = int.MaxValue;

        internal GroupHandbookPage(
            string pageCode,
            string hiddenCategoryCode,
            string displayCategoryCode,
            string name,
            IEnumerable<GuiHandbookPage> members)
        {
            this.pageCode = pageCode ?? throw new ArgumentNullException(nameof(pageCode));
            this.hiddenCategoryCode = hiddenCategoryCode ?? throw new ArgumentNullException(nameof(hiddenCategoryCode));
            this.displayCategoryCode = displayCategoryCode;
            this.members = members?.Where(page => page != null).ToList() ?? new List<GuiHandbookPage>();

            UpdateDisplayName(name);
        }

        internal IReadOnlyList<GuiHandbookPage> Members => members;

        internal string DisplayCategoryCode => displayCategoryCode;

        internal string HiddenCategoryCode => hiddenCategoryCode;

        internal string DisplayName => displayName;

        internal string SearchableText => searchableText;

        internal int SortOrderHint => sortOrderHint;

        internal void UpdateDisplayName(string name)
        {
            string fallback = string.IsNullOrWhiteSpace(name) ? "Group" : name.Trim();
            displayName = fallback;
            normalizedName = fallback.ToSearchFriendly().ToLowerInvariant();
            listDisplayText = BuildListDisplayText(displayName, members.Count);
            searchableText = BuildSearchableText(displayName, members);
            DisposeTexture();
        }

        internal void SetSortOrderHint(int hint)
        {
            sortOrderHint = hint < 0 ? int.MaxValue : hint;
        }

        internal void AdoptAppearanceFrom(GuiHandbookPage sourcePage)
        {
            weightSourcePage = sourcePage ?? members.FirstOrDefault();
            iconSlot = CloneSlotFromPage(weightSourcePage);
        }

        private static string BuildListDisplayText(string name, int memberCount)
        {
            if (memberCount <= 0)
            {
                return name;
            }

            return string.Concat(name, " (", memberCount.ToString(), ")");
        }

        private static string BuildSearchableText(string name, IEnumerable<GuiHandbookPage> members)
        {
            var components = new List<string>
            {
                name.ToSearchFriendly()
            };

            if (members != null)
            {
                foreach (GuiHandbookPage member in members)
                {
                    if (member == null)
                    {
                        continue;
                    }

                    string title = HandbookCategoryManager.GetLocalizedPageTitle(member);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        components.Add(title.ToSearchFriendly());
                    }
                }
            }

            return string.Join(" ", components).ToLowerInvariant();
        }

        public override string PageCode => pageCode;

        public override string CategoryCode => hiddenCategoryCode;

        public override bool IsDuplicate => false;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (capi == null)
            {
                return;
            }

            float iconSize = (float)GuiElement.scaled(25.0);
            float iconOffset = (float)GuiElement.scaled(10.0);
            bool hasIcon = iconSlot?.Itemstack != null;

            if (hasIcon)
            {
                capi.Render.RenderItemstackToGui(
                    iconSlot,
                    x + (double)iconOffset + (double)(iconSize / 2f),
                    y + (double)(iconSize / 2f),
                    100.0,
                    iconSize,
                    -1,
                    shading: true,
                    rotate: false,
                    showStackSize: false);
            }

            EnsureTexture(capi);

            if (titleTexture == null)
            {
                return;
            }

            double textX = hasIcon
                ? x + (double)iconSize + GuiElement.scaled(25.0)
                : x + GuiElement.scaled(TextOffsetX);
            double textY = hasIcon
                ? y + (double)(iconSize / 4f) - GuiElement.scaled(3.0)
                : y + GuiElement.scaled(TextOffsetY);

            capi.Render.Render2DTexturePremultipliedAlpha(
                titleTexture.TextureId,
                textX,
                textY,
                titleTexture.Width,
                titleTexture.Height);
        }

        public override void Dispose()
        {
            DisposeTexture();
            iconSlot = null;
        }

        public override float GetTextMatchWeight(string searchText)
        {
            float fallback = GetGroupMatchWeight(searchText);

            if (weightSourcePage != null)
            {
                try
                {
                    float sourceWeight = weightSourcePage.GetTextMatchWeight(searchText);
                    if (sourceWeight > fallback)
                    {
                        return sourceWeight;
                    }
                }
                catch
                {
                    // Ignore failures from the original page to avoid breaking the UI.
                }
            }

            return fallback;
        }

        public override void ComposePage(
            GuiComposer detailViewGui,
            ElementBounds textBounds,
            ItemStack[] allStacks,
            ActionConsumable<string> openDetailPageFor)
        {
            if (detailViewGui == null || textBounds == null)
            {
                return;
            }

            string description = listDisplayText;
            if (string.IsNullOrWhiteSpace(description))
            {
                description = displayName ?? string.Empty;
            }

            detailViewGui.AddStaticText(description, CairoFont.WhiteSmallText(), textBounds);
        }

        internal void DisposeTexture()
        {
            titleTexture?.Dispose();
            titleTexture = null;
        }

        private float GetGroupMatchWeight(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return 2.5f;
            }

            string normalized = searchText.ToSearchFriendly().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                return 2.5f;
            }

            if (!string.IsNullOrEmpty(normalizedName))
            {
                if (normalizedName.Equals(normalized, StringComparison.Ordinal))
                {
                    return 3.5f;
                }

                if (normalizedName.StartsWith(normalized, StringComparison.Ordinal))
                {
                    return 3.2f;
                }

                if (normalizedName.Contains(normalized, StringComparison.Ordinal))
                {
                    return 3f;
                }
            }

            if (!string.IsNullOrEmpty(searchableText) && searchableText.Contains(normalized, StringComparison.Ordinal))
            {
                return 2.5f;
            }

            return 0f;
        }

        private static DummySlot CloneSlotFromPage(GuiHandbookPage sourcePage)
        {
            if (sourcePage == null)
            {
                return null;
            }

            return sourcePage switch
            {
                GuiHandbookItemStackPage itemPage => CloneDummySlot(itemPage.dummySlot),
                GuiHandbookMealRecipePage mealPage => CloneDummySlot(mealPage.dummySlot),
                _ => null
            };
        }

        private static DummySlot CloneDummySlot(DummySlot source)
        {
            if (source?.Itemstack == null)
            {
                return null;
            }

            ItemStack clone = source.Itemstack.Clone();
            if (clone == null)
            {
                return null;
            }

            return source.Inventory != null ? new DummySlot(clone, source.Inventory) : new DummySlot(clone);
        }

        private void EnsureTexture(ICoreClientAPI capi)
        {
            if (capi == null)
            {
                return;
            }

            if (titleTexture != null)
            {
                return;
            }

            string text = listDisplayText;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = displayName ?? string.Empty;
            }

            titleTexture = new TextTextureUtil(capi).GenTextTexture(text, CairoFont.WhiteSmallText());
        }
    }
}
