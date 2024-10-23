using System.Windows.Media;
using System.Windows.Shapes;
using Serein.Library;

namespace Serein.Workbench.Node.View
{

    public class ResultJunctionControl : JunctionControlBase
    {
        //public override JunctionType JunctionType { get; } = JunctionType.ReturnData;

        public ResultJunctionControl()
        {
            base.JunctionType = JunctionType.ReturnData;
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
                Fill = Brushes.Red,
                ToolTip = "返回值"
            };
            Content = rect;
        }
    }
}
