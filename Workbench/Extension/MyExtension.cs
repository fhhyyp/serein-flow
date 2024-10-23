using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Serein.Workbench.Extension
{
    public static class PointExtension
    {
        public static Point Add(this Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        public static Point Sub(this Point a, Point b)
        {
            return new Point(a.X - b.X, a.Y - b.Y);
        }

        public static Vector ToVector(this Point me)
        {
            return new Vector(me.X, me.Y);
        }
    }
    public static class VectorExtension
    {
        public static double DotProduct(this Vector a, Vector b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        public static Vector NormalizeTo(this Vector v)
        {
            var temp = v;
            temp.Normalize();

            return temp;
        }
    }
}
