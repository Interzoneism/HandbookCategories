using System;
using System.Collections.Generic;
using System.Reflection;
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
        private readonly string dialogTitle;
        private readonly string dialogMessage;
        private readonly string placeholderText;
        private readonly string okButtonText;
        private readonly string cancelButtonText;
        private readonly string dialogKey;
        private readonly string initialValue;
        private string lastValidInput = string.Empty;
        private bool isUpdatingTextInput;

        private static readonly FieldInfo SelectedTextStartField = typeof(GuiElementEditableTextBase)
            .GetField("selectedTextStart", BindingFlags.NonPublic | BindingFlags.Instance);

        public override double DrawOrder => 2.5;

        public override double InputOrder => 0.1;

        internal CreateCategoryPromptDialog(
            ICoreClientAPI capi,
            Action<string> onConfirm,
            string title = null,
            string message = null,
            string placeholder = null,
            string okText = null,
            string cancelText = null,
            string dialogKey = "handbookcategories-createprompt",
            string initialValue = null)
            : base(title ?? HandbookCategoryManager.GetCreateCategoryPromptTitle(), capi)
        {
            this.onConfirm = onConfirm;
            dialogTitle = title ?? HandbookCategoryManager.GetCreateCategoryPromptTitle();
            dialogMessage = message ?? HandbookCategoryManager.GetCreateCategoryPromptMessage();
            placeholderText = placeholder ?? HandbookCategoryManager.GetCreateCategoryPromptPlaceholder();
            okButtonText = okText ?? HandbookCategoryManager.GetCreateCategoryPromptOkText();
            cancelButtonText = cancelText ?? HandbookCategoryManager.GetCreateCategoryPromptCancelText();
            this.dialogKey = dialogKey ?? "handbookcategories-createprompt";
            this.initialValue = initialValue;
            ComposeDialog();
        }

        private void ComposeDialog()
        {
            bool hasMessage = !string.IsNullOrWhiteSpace(dialogMessage);

            SingleComposer?.Dispose();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0.0);

            ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            backgroundBounds.BothSizing = ElementSizing.FitToChildren;

            const double contentTopPadding = 18.0;

            ElementBounds messageBounds = ElementBounds.Fixed(0.0, contentTopPadding, 360.0, 0.0);
            CairoFont messageFont = null;
            if (hasMessage)
            {
                messageFont = CairoFont.WhiteSmallText();
                float messageHeight = (float)new TextDrawUtil().GetMultilineTextHeight(messageFont, dialogMessage, messageBounds.fixedWidth);
                messageBounds.fixedHeight = Math.Max(30.0f, messageHeight);
            }

            ElementBounds inputBounds = hasMessage
                ? messageBounds.BelowCopy(0.0, 10.0).WithFixedWidth(360.0).WithFixedHeight(30.0)
                : ElementBounds.Fixed(0.0, contentTopPadding, 360.0, 30.0);
            ElementBounds buttonBounds = inputBounds.BelowCopy(0.0, 15.0).WithFixedWidth(140.0).WithFixedHeight(30.0);

            GuiComposer composer = capi.Gui.CreateCompo(dialogKey, dialogBounds)
                .AddShadedDialogBG(backgroundBounds, false)
                .AddDialogTitleBar(dialogTitle, OnTitleBarClose)
                .BeginChildElements(backgroundBounds);

            if (hasMessage)
            {
                composer.AddStaticText(dialogMessage, messageFont, messageBounds);
            }

            composer
                    .AddTextInput(inputBounds, OnNameChanged, CairoFont.TextInput(), TextInputKey)
                    .AddSmallButton(cancelButtonText, OnCancelClicked, buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.LeftFixed))
                    .AddSmallButton(okButtonText, OnOkClicked, buttonBounds.FlatCopy().WithAlignment(EnumDialogArea.RightFixed), EnumButtonStyle.Normal, OkButtonKey)
                .EndChildElements();

            SingleComposer = composer.Compose();
            GuiElementTextInput input = SingleComposer.GetTextInput(TextInputKey);
            if (input != null)
            {
                input.SetPlaceHolderText(placeholderText);
                if (!string.IsNullOrEmpty(initialValue))
                {
                    input.SetValue(initialValue);
                    SelectAllText(input);
                }

                input.OnTryTextChangeText = EnsureTextWithinLimit;
            }
            lastValidInput = input?.GetText() ?? string.Empty;

            GuiElementTextButton okButton = SingleComposer.GetButton(OkButtonKey);
            if (okButton != null)
            {
                if (!string.IsNullOrEmpty(initialValue))
                {
                    OnNameChanged(initialValue);
                }
                else
                {
                    okButton.Enabled = false;
                }
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
            if (isUpdatingTextInput)
            {
                return;
            }

            string normalized = NormalizeInput(value, out _, out _);
            if (normalized != null && normalized.Length > HandbookCategoryManager.MaxCategoryNameLength)
            {
                GuiElementTextInput input = SingleComposer?.GetTextInput(TextInputKey);
                if (input != null)
                {
                    isUpdatingTextInput = true;
                    input.SetValue(lastValidInput);
                    isUpdatingTextInput = false;
                }

                UpdateOkButtonState(lastValidInput);
                return;
            }

            lastValidInput = value ?? string.Empty;
            UpdateOkButtonState(lastValidInput);
        }

        private bool OnOkClicked()
        {
            string rawText = SingleComposer?.GetTextInput(TextInputKey)?.GetText();
            string normalized = NormalizeInput(rawText, out string trimmed, out string sanitized);
            if (string.IsNullOrWhiteSpace(trimmed) || string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized.Length > HandbookCategoryManager.MaxCategoryNameLength)
            {
                capi?.ShowChatMessage(HandbookCategoryManager.GetCategoryNameTooLongMessage());
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

        private static string NormalizeInput(string value, out string trimmed, out string sanitized)
        {
            trimmed = value?.Trim();
            sanitized = trimmed;
            return sanitized;
        }

        private static void SelectAllText(GuiElementTextInput input)
        {
            if (input == null || SelectedTextStartField == null)
            {
                return;
            }

            try
            {
                SelectedTextStartField.SetValue(input, 0);
                input.CaretPosWithoutLineBreaks = input.TextLengthWithoutLineBreaks;
            }
            catch
            {
                // Swallow reflection errors silently; selecting text is a convenience feature.
            }
        }

        private bool EnsureTextWithinLimit(List<string> lines)
        {
            if (lines == null)
            {
                return true;
            }

            int totalLength = 0;
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                totalLength += line.Length;
                if (totalLength > HandbookCategoryManager.MaxCategoryNameLength)
                {
                    return false;
                }
            }

            string normalized = NormalizeInput(string.Concat(lines), out _, out _);
            return normalized == null || normalized.Length <= HandbookCategoryManager.MaxCategoryNameLength;
        }

        private void UpdateOkButtonState(string value)
        {
            GuiElementTextButton okButton = SingleComposer?.GetButton(OkButtonKey);
            if (okButton == null)
            {
                return;
            }

            string normalized = NormalizeInput(value, out _, out _);
            bool hasText = !string.IsNullOrWhiteSpace(normalized);
            bool withinLimit = normalized != null && normalized.Length <= HandbookCategoryManager.MaxCategoryNameLength;
            okButton.Enabled = hasText && withinLimit;
        }
    }
}
