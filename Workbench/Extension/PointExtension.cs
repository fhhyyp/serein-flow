using System.Windows;

namespace Serein.Workbench.Extension
{
    /// <summary>
    /// 点(Point)和向量(Vector)的扩展方法
    /// </summary>
    public static class PointExtension
    {
        /// <summary>
        /// 将两个点相加，返回一个新的点。
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Point Add(this Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        /// <summary>
        /// 将两个点相减，返回一个新的点。
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Point Sub(this Point a, Point b)
        {
            return new Point(a.X - b.X, a.Y - b.Y);
        }

        /// <summary>
        /// 将点转换为向量。
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        public static Vector ToVector(this Point me)
        {
            return new Vector(me.X, me.Y);
        }
    }
}
