using System;
using Cairo;
using Vintagestory.API.Client;

namespace Enhanced_Handbook
{
    internal sealed class HandbookSetupDialog : GuiDialogGeneric
    {
        private const string DialogKey = "handbookcategories-setup-dialog";
        private const string OkButtonKey = "handbookcategories-setup-ok";

        private readonly Action runDefaultCategoriesAction;
        private HandbookCategoriesConfig config;

        internal HandbookSetupDialog(ICoreClientAPI capi, HandbookCategoriesConfig config, Action runDefaultCategoriesAction)
            : base(HandbookCategoryManager.GetSetupDialogTitle(), capi)
        {
            this.config = config ?? HandbookCategoriesConfig.CreateDefault();
            this.runDefaultCategoriesAction = runDefaultCategoriesAction;
            ComposeDialog();
        }

        public override bool TryOpen()
        {
            ComposeDialog();
            return base.TryOpen();
        }

        internal void UpdateFromConfig(HandbookCategoriesConfig updatedConfig)
        {
            config = updatedConfig ?? HandbookCategoriesConfig.CreateDefault();

            if (IsOpened())
            {
                ComposeDialog();
            }
        }

        private void ComposeDialog()
        {
            if (capi == null)
            {
                return;
            }

            SingleComposer?.Dispose();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0.0);

            ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            backgroundBounds.BothSizing = ElementSizing.FitToChildren;

            GuiComposer composer = capi.Gui.CreateCompo(DialogKey, dialogBounds)
                .AddShadedDialogBG(backgroundBounds, false)
                .AddDialogTitleBar(HandbookCategoryManager.GetSetupDialogTitle(), OnTitleBarClose)
                .BeginChildElements(backgroundBounds);

            CairoFont headerFont = CairoFont.WhiteSmallText();
            CairoFont detailFont = CairoFont.WhiteDetailText();

            ElementBounds headerBounds = ElementBounds.Fixed(0.0, 30.0, 420.0, 20.0);
            ElementBounds restartBounds = headerBounds.BelowCopy(0.0, 4.0).WithFixedHeight(20.0);

            composer
                .AddStaticText(HandbookCategoryManager.GetSetupDialogHeaderText(), headerFont, headerBounds)
                .AddStaticText(HandbookCategoryManager.GetSetupDialogRestartNotice(), detailFont, restartBounds);

            double currentY = restartBounds.fixedY + restartBounds.fixedHeight + 12.0;

            currentY = AddToggle(composer,
                "hide-tutorial",
                HandbookCategoryManager.GetHideTutorialToggleText(),
                HandbookCategoryManager.GetHideTutorialTooltipText(),
                config?.DisableTutorialTab == true,
                OnHideTutorialToggled,
                currentY);

            currentY = AddToggle(composer,
                "hide-blocksitems",
                HandbookCategoryManager.GetHideBlocksItemsToggleText(),
                HandbookCategoryManager.GetHideBlocksItemsTooltipText(),
                config?.DisableBlocksAndItemsTab == true,
                OnHideBlocksItemsToggled,
                currentY);

            currentY = AddToggle(composer,
                "hide-guides",
                HandbookCategoryManager.GetHideGuidesToggleText(),
                HandbookCategoryManager.GetHideGuidesTooltipText(),
                config?.DisableGuidesTab == true,
                OnHideGuidesToggled,
                currentY);

            currentY = AddToggle(composer,
                "hide-originalsearch",
                HandbookCategoryManager.GetHideOriginalSearchToggleText(),
                HandbookCategoryManager.GetHideOriginalSearchTooltipText(),
                config?.DisableOriginalSearchButton == true,
                OnHideOriginalSearchToggled,
                currentY);

            currentY = AddToggle(composer,
                "grouped-category",
                HandbookCategoryManager.GetGroupedCategoryToggleText(),
                HandbookCategoryManager.GetGroupedCategoryTooltipText(),
                config?.CreateEverythingGrouped == true,
                OnGroupedCategoryToggled,
                currentY);

            currentY = AddToggle(composer,
                "variant-category",
                HandbookCategoryManager.GetVariantCategoryToggleText(),
                HandbookCategoryManager.GetVariantCategoryTooltipText(),
                config?.CreateVariantCategories == true,
                OnVariantCategoryToggled,
                currentY);

            currentY = AddToggle(composer,
                "group-hotkeys",
                HandbookCategoryManager.GetGroupHotkeysToggleText(),
                HandbookCategoryManager.GetGroupHotkeysTooltipText(),
                config?.EnableGroupCreationHotkeys == true,
                OnGroupHotkeysToggled,
                currentY);

            ElementBounds categoryLabelBounds = ElementBounds.Fixed(0.0, currentY + 6.0, 240.0, 20.0);
            ElementBounds categoryButtonBounds = ElementBounds.Fixed(260.0, currentY, 200.0, 30.0);

            composer
                .AddStaticText(HandbookCategoryManager.GetCreateDefaultsLabelText(), detailFont, categoryLabelBounds)
                .AddSmallButton(HandbookCategoryManager.GetCreateDefaultsButtonText(), OnCreateDefaultsClicked, categoryButtonBounds);

            ElementBounds okButtonBounds = categoryButtonBounds.BelowCopy(0.0, 18.0).WithFixedWidth(120.0);
            okButtonBounds.fixedX = categoryButtonBounds.fixedX + categoryButtonBounds.fixedWidth - okButtonBounds.fixedWidth;

            composer
                .AddSmallButton(HandbookCategoryManager.GetSetupDialogOkText(), OnOkClicked, okButtonBounds, EnumButtonStyle.Normal, OkButtonKey)
                .EndChildElements();

            SingleComposer = composer.Compose();
        }

        private double AddToggle(
            GuiComposer composer,
            string elementKey,
            string label,
            string tooltip,
            bool enabled,
            Action<bool> onToggle,
            double currentY)
        {
            ElementBounds toggleBounds = ElementBounds.Fixed(0.0, currentY, 460.0, 28.0);
            LeftReleaseToggleButton toggle = new LeftReleaseToggleButton(
                capi,
                string.Empty,
                label,
                CairoFont.WhiteDetailText(),
                value => onToggle?.Invoke(value),
                toggleBounds,
                true);

            toggle.SetValue(enabled);

            composer
                .AddInteractiveElement(toggle, $"{DialogKey}-{elementKey}")
                .AddHoverText(tooltip, CairoFont.WhiteSmallText(), 520, toggleBounds.FlatCopy(), $"{DialogKey}-{elementKey}-hover");

            return currentY + toggleBounds.fixedHeight + 8.0;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnOkClicked()
        {
            TryClose();
            return true;
        }

        private bool OnCreateDefaultsClicked()
        {
            runDefaultCategoriesAction?.Invoke();
            return true;
        }

        private void OnHideTutorialToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.DisableTutorialTab == enabled)
                {
                    return false;
                }

                config.DisableTutorialTab = enabled;
                return true;
            });
        }

        private void OnHideBlocksItemsToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.DisableBlocksAndItemsTab == enabled)
                {
                    return false;
                }

                config.DisableBlocksAndItemsTab = enabled;
                return true;
            });
        }

        private void OnHideGuidesToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.DisableGuidesTab == enabled)
                {
                    return false;
                }

                config.DisableGuidesTab = enabled;
                return true;
            });
        }

        private void OnHideOriginalSearchToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.DisableOriginalSearchButton == enabled)
                {
                    return false;
                }

                config.DisableOriginalSearchButton = enabled;
                return true;
            });
        }

        private void OnGroupedCategoryToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.CreateEverythingGrouped == enabled)
                {
                    return false;
                }

                config.CreateEverythingGrouped = enabled;
                return true;
            });
        }

        private void OnVariantCategoryToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.CreateVariantCategories == enabled)
                {
                    return false;
                }

                config.CreateVariantCategories = enabled;
                return true;
            });
        }

        private void OnGroupHotkeysToggled(bool enabled)
        {
            UpdateSetting(config =>
            {
                if (config.EnableGroupCreationHotkeys == enabled)
                {
                    return false;
                }

                config.EnableGroupCreationHotkeys = enabled;
                return true;
            });
        }

        private void UpdateSetting(Func<HandbookCategoriesConfig, bool> applyChange)
        {
            if (capi == null)
            {
                return;
            }

            config ??= HandbookCategoriesConfig.CreateDefault();

            if (!applyChange(config))
            {
                return;
            }

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            HandbookCategoryManager.ReloadConfiguration();
            HandbookCategoryManager.RequestTabsRebuild();
        }
    }
}
