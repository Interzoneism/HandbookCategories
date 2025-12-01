using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Enhanced_Handbook
{
    internal class LeftReleaseToggleButton : GuiElementToggleButton
    {
        private readonly Action<bool> onToggle;
        private bool mouseDownOnElement;

        public LeftReleaseToggleButton(
            ICoreClientAPI capi,
            string icon,
            string text,
            CairoFont font,
            Action<bool> onToggle,
            ElementBounds bounds,
            bool toggleable = false)
            : base(capi, icon, text, font, null, bounds, toggleable)
        {
            this.onToggle = onToggle;
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (args == null || args.Button != EnumMouseButton.Left || !Enabled)
            {
                return;
            }

            args.Handled = true;
            mouseDownOnElement = true;
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            if (mouseDownOnElement)
            {
                OnMouseUpOnElement(api, args);
                return;
            }

            base.OnMouseUp(api, args);
        }

        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            try
            {
                if (mouseDownOnElement && args?.Button == EnumMouseButton.Left && Bounds.PointInside(args.X, args.Y))
                {
                    On = !On;
                    onToggle?.Invoke(On);
                    api?.Gui.PlaySound("toggleswitch");
                    args.Handled = true;
                }

                if (!Toggleable)
                {
                    On = false;
                }
            }
            finally
            {
                mouseDownOnElement = false;
            }
        }
    }
}
