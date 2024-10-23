using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{

    #region Model，不科学的全局变量
    public class MyLine
    {
        public MyLine(Canvas canvas, Line line)
        {
            Canvas = canvas;
            VirtualLine = line;
            canvas?.Children.Add(line);
        }

        public Canvas Canvas { get; set; }
        public Line VirtualLine { get; set; }

        public void Remove()
        {
            Canvas?.Children.Remove(VirtualLine);
        }
    }

    public class ConnectingData
    {
        public JunctionControlBase StartJunction { get; set; }
        public JunctionControlBase ChangingJunction { get; set; }
        public Point StartPoint { get; set; }
        public MyLine VirtualLine { get; set; }
    }

    public static class GlobalJunctionData
    {
        private static ConnectingData? myGlobalData;

        public static ConnectingData? MyGlobalData
        {
            get => myGlobalData;
            set
            {
                if (myGlobalData == null)
                {
                    myGlobalData = value;
                }
            }
        }



        public static bool IsCreatingConnection => myGlobalData is not null;

        public static bool CanCreate => myGlobalData?.ChangingJunction.Equals(myGlobalData?.StartJunction) == false;

        public static void OK()
        {
            myGlobalData?.VirtualLine.Remove();
            myGlobalData = null;
        }
    }
    #endregion
}
