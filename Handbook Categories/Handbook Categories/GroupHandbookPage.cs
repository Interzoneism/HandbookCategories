using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
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
        private bool? lastRenderedRecipesOnly;
        private DummySlot iconSlot;
        private GuiHandbookPage weightSourcePage;
        private int sortOrderHint = int.MaxValue;
        private ElementBounds iconScissorBounds;

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
            EnsureIconSlotInitialized();
        }

        internal DummySlot GetIconSlot()
        {
            EnsureIconSlotInitialized();
            return iconSlot;
        }

        internal bool AddMembers(IEnumerable<GuiHandbookPage> newMembers)
        {
            if (newMembers == null)
            {
                return false;
            }

            bool added = false;
            foreach (GuiHandbookPage member in newMembers)
            {
                if (member == null)
                {
                    continue;
                }

                if (members.Exists(existing => ReferenceEquals(existing, member)))
                {
                    continue;
                }

                members.Add(member);
                added = true;
            }

            if (added)
            {
                UpdateDisplayName(displayName);
            }

            return added;
        }

        internal bool RemoveMember(GuiHandbookPage member)
        {
            if (member == null)
            {
                return false;
            }

            bool removed = false;
            string memberCode = member.PageCode;

            for (int i = members.Count - 1; i >= 0; i--)
            {
                GuiHandbookPage existing = members[i];
                if (existing == null)
                {
                    continue;
                }

                if (ReferenceEquals(existing, member))
                {
                    members.RemoveAt(i);
                    removed = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(memberCode)
                    && !string.IsNullOrEmpty(existing.PageCode)
                    && string.Equals(existing.PageCode, memberCode, StringComparison.OrdinalIgnoreCase))
                {
                    members.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return false;
            }

            if (ReferenceEquals(weightSourcePage, member))
            {
                weightSourcePage = members.FirstOrDefault(page => page != null);
                iconSlot = CloneSlotFromPage(weightSourcePage);
            }

            UpdateDisplayName(displayName);
            return true;
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

        public override float SearchWeightOffset => Math.Max(1.5f, weightSourcePage?.SearchWeightOffset ?? 0f);

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (capi == null)
            {
                return;
            }

            float iconSize = (float)GuiElement.scaled(25.0);
            float iconOffset = (float)GuiElement.scaled(10.0);
            EnsureIconSlotInitialized();
            bool hasIcon = iconSlot?.Itemstack != null;

            if (hasIcon)
            {
                EnsureIconScissorBounds(capi);

                if (iconScissorBounds != null)
                {
                    iconScissorBounds.fixedX = ((double)iconOffset + x - (double)(iconSize / 2f)) / (double)RuntimeEnv.GUIScale;
                    iconScissorBounds.fixedY = (y - (double)(iconSize / 2f)) / (double)RuntimeEnv.GUIScale;
                    iconScissorBounds.CalcWorldBounds();

                    if (iconScissorBounds.InnerWidth > 0.0 && iconScissorBounds.InnerHeight > 0.0)
                    {
                        capi.Render.PushScissor(iconScissorBounds, stacking: true);
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
                        capi.Render.PopScissor();
                    }
                }
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
            iconScissorBounds = null;
        }

        public override PageText GetPageText()
        {
            return new PageText
            {
                Title = normalizedName ?? string.Empty,
                Text = searchableText ?? string.Empty
            };
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
            lastRenderedRecipesOnly = null;
        }

        private void EnsureIconScissorBounds(ICoreClientAPI capi)
        {
            if (capi == null)
            {
                return;
            }

            if (iconScissorBounds != null)
            {
                return;
            }

            iconScissorBounds = ElementBounds.FixedSize(50.0, 50.0);
            iconScissorBounds.ParentBounds = capi.Gui.WindowBounds;
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

        private void EnsureIconSlotInitialized()
        {
            if (iconSlot?.Itemstack != null)
            {
                return;
            }

            if (TryCloneIconFrom(weightSourcePage))
            {
                return;
            }

            if (members == null)
            {
                return;
            }

            foreach (GuiHandbookPage member in members)
            {
                if (TryCloneIconFrom(member))
                {
                    weightSourcePage = member;
                    return;
                }
            }
        }

        private bool TryCloneIconFrom(GuiHandbookPage candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            DummySlot clone = CloneSlotFromPage(candidate);
            if (clone?.Itemstack == null)
            {
                return false;
            }

            iconSlot = clone;
            return true;
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

        private int CountRecipeMembers()
        {
            if (members == null)
            {
                return 0;
            }

            int count = 0;
            foreach (GuiHandbookPage member in members)
            {
                if (HandbookCategoryManager.IsGridRecipePage(member))
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureTexture(ICoreClientAPI capi)
        {
            if (capi == null)
            {
                return;
            }

            bool currentRecipesOnly = HandbookCategoryManager.RecipesOnlyEnabled;
            if (titleTexture != null && lastRenderedRecipesOnly == currentRecipesOnly)
            {
                return;
            }

            DisposeTexture();

            string text;
            if (currentRecipesOnly)
            {
                int recipeCount = CountRecipeMembers();
                text = BuildListDisplayText(displayName, recipeCount);
            }
            else
            {
                text = listDisplayText;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = displayName ?? string.Empty;
            }

            titleTexture = GenerateLinkStyleTexture(capi, text, titleTexture);
            lastRenderedRecipesOnly = currentRecipesOnly;
        }

        private static LoadedTexture GenerateLinkStyleTexture(ICoreClientAPI capi, string text, LoadedTexture existing)
        {
            if (capi == null)
            {
                return existing;
            }

            CairoFont font = CairoFont.WhiteSmallText().Clone().WithColor(GuiStyle.ActiveButtonTextColor);

            if (string.IsNullOrEmpty(text))
            {
                text = string.Empty;
            }

            ElementBounds bounds = new ElementBounds();
            font.AutoBoxSize(text, bounds);

            int width = Math.Max(1, (int)Math.Ceiling(GuiElement.scaled(bounds.fixedWidth + 1.0)));
            int baseHeight = Math.Max(1, (int)Math.Ceiling(GuiElement.scaled(bounds.fixedHeight + 1.0)));

            int height = baseHeight;

            LoadedTexture texture = existing ?? new LoadedTexture(capi);

            using (ImageSurface surface = new ImageSurface(Format.Argb32, width, height))
            using (Context context = GuiElement.GenContext(surface))
            {
                font.SetupContext(context);

                FontExtents extents = context.FontExtents;
                double lineHeight = extents.Height * (font.LineHeightMultiplier <= 0.0 ? 1.0 : font.LineHeightMultiplier);
                double baseline = extents.Ascent;

                string[] lines = text.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    double yOffset = baseline + lineHeight * i;

                    if (font.StrokeWidth > 0.0 && font.StrokeColor != null)
                    {
                        context.MoveTo(0.0, yOffset);
                        context.TextPath(line);
                        context.LineWidth = font.StrokeWidth;
                        SetSourceColor(context, font.StrokeColor);
                        context.StrokePreserve();
                        SetSourceColor(context, font.Color);
                        context.Fill();
                    }
                    else
                    {
                        SetSourceColor(context, font.Color);
                        context.MoveTo(0.0, yOffset);
                        context.ShowText(line);
                        if (font.RenderTwice)
                        {
                            context.MoveTo(0.0, yOffset);
                            context.ShowText(line);
                        }
                    }

                }

                capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref texture);
            }

            return texture;
        }

        private static void SetSourceColor(Context context, double[] color)
        {
            if (context == null || color == null)
            {
                return;
            }

            if (color.Length >= 4)
            {
                context.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            }
            else if (color.Length >= 3)
            {
                context.SetSourceRGB(color[0], color[1], color[2]);
            }
        }
    }
}
