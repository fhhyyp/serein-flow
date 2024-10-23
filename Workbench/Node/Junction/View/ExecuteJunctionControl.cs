using System.Windows.Media;
using System.Windows.Shapes;
using Serein.Library;

namespace Serein.Workbench.Node.View
{
    public class ExecuteJunctionControl : JunctionControlBase
    {
        //public override JunctionType JunctionType { get; } = JunctionType.Execute;
        public ExecuteJunctionControl()
        {
            base.JunctionType = JunctionType.Execute;
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
                Fill = Brushes.Green,
                ToolTip = "方法执行"
            };
            Content = rect;
        }
    }


}
