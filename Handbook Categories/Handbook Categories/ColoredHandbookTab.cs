using Vintagestory.GameContent;

namespace Enhanced_Handbook
{
    internal interface IHandbookTabBackground
    {
        double[] BackgroundColor { get; }
    }

    internal sealed class ColoredHandbookTab : HandbookTab, IHandbookTabBackground
    {
        private double[] backgroundColor;

        public double[] BackgroundColor
        {
            get => backgroundColor;
            init => backgroundColor = value != null ? (double[])value.Clone() : null;
        }
    }
}
