using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Enhanced_Handbook
{
    internal sealed class CreateCategoryPromptDialog : GuiDialogGeneric
    {
        private const string TextInputKey = "handbookcategories-createprompt-input";
        private const string OkButtonKey = "handbookcategories-createprompt-ok";

        private readonly Action<string> onConfirm;

        internal CreateCategoryPromptDialog(ICoreClientAPI capi, Action<string> onConfirm)
            : base(HandbookCategoryManager.GetCreateCategoryPromptTitle(), capi)
        {
            this.onConfirm = onConfirm;
            ComposeDialog();
        }

        private void ComposeDialog()
        {
            string title = HandbookCategoryManager.GetCreateCategoryPromptTitle();
            string message = HandbookCategoryManager.GetCreateCategoryPromptMessage();

            SingleComposer?.Dispose();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0.0);

            ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            backgroundBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds messageBounds = ElementBounds.Fixed(0.0, 0.0, 360.0, 0.0);
            CairoFont messageFont = CairoFont.WhiteSmallText();
            float messageHeight = (float)new TextDrawUtil().GetMultilineTextHeight(messageFont, message, messageBounds.fixedWidth);
            messageBounds.fixedHeight = Math.Max(30.0f, messageHeight);

            ElementBounds inputBounds = messageBounds.BelowCopy(0.0, 10.0).WithFixedWidth(360.0).WithFixedHeight(30.0);
            ElementBounds buttonBounds = inputBounds.BelowCopy(0.0, 15.0).WithFixedWidth(140.0).WithFixedHeight(30.0);

            GuiComposer composer = capi.Gui.CreateCompo("handbookcategories-createprompt", dialogBounds)
                .AddShadedDialogBG(backgroundBounds, false)
                .AddDialogTitleBar(title, OnTitleBarClose)
                .BeginChildElements(backgroundBounds)
                    .AddStaticText(message, messageFont, messageBounds)
                    .AddTextInput(inputBounds, OnNameChanged, CairoFont.TextInput(), TextInputKey)
                    .AddSmallButton(HandbookCategoryManager.GetCreateCategoryPromptCancelText(), OnCancelClicked, buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.LeftFixed))
                    .AddSmallButton(HandbookCategoryManager.GetCreateCategoryPromptOkText(), OnOkClicked, buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.RightFixed), EnumButtonStyle.Normal, OkButtonKey)
                .EndChildElements();

            SingleComposer = composer.Compose();
            SingleComposer.GetTextInput(TextInputKey)?.SetPlaceHolderText(HandbookCategoryManager.GetCreateCategoryPromptPlaceholder());
            GuiElementTextButton okButton = SingleComposer.GetButton(OkButtonKey);
            if (okButton != null)
            {
                okButton.Enabled = false;
            }
        }

        public override bool TryOpen()
        {
            ComposeDialog();
            return base.TryOpen();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer?.FocusElement(0);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private void OnNameChanged(string value)
        {
            GuiElementTextButton okButton = SingleComposer?.GetButton(OkButtonKey);
            if (okButton != null)
            {
                string trimmed = value?.Trim();
                string sanitized = trimmed?.TrimStart('#').Trim();
                okButton.Enabled = !string.IsNullOrWhiteSpace(sanitized);
            }
        }

        private bool OnOkClicked()
        {
            string rawText = SingleComposer?.GetTextInput(TextInputKey)?.GetText();
            string trimmed = rawText?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            string sanitized = trimmed.TrimStart('#').Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return false;
            }

            TryClose();
            onConfirm?.Invoke(sanitized);
            return true;
        }

        private bool OnCancelClicked()
        {
            TryClose();
            return true;
        }
    }
}
