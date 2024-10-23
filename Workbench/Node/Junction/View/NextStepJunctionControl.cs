using System.Windows.Media;
using System.Windows.Shapes;
using Serein.Library;

namespace Serein.Workbench.Node.View
{

    public class NextStepJunctionControl : JunctionControlBase
    {
        //public override JunctionType JunctionType { get; } = JunctionType.NextStep;
        public NextStepJunctionControl()
        {
            base.JunctionType = JunctionType.NextStep;
            Render();
        }

        public override void Render()
        {
            if (double.IsNaN(base.Width))
            {
                base.Width = base._MyWidth;
            }
            if (double.IsNaN(base.Height))
            {
                base.Height = base._MyHeight;
            }

            var rect = new Rectangle
            {
                Width = base.Width,
                Height = base.Height,
                Fill = Brushes.Blue,
                ToolTip = "下一个方法值"
            };
            Content = rect;
        }
    }
}
