using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Serein.Workbench.Extension
{

    /// <summary>
    /// 向量(Vector)的扩展方法
    /// </summary>
    public static class VectorExtension
    {
        /// <summary>
        /// 计算两个向量的点积。
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double DotProduct(this Vector a, Vector b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// <summary>
        /// 计算两个向量的叉积。
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static Vector NormalizeTo(this Vector v)
        {
            var temp = v;
            temp.Normalize();

            return temp;
        }
    }
}
