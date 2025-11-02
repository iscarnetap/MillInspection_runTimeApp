using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BeckhoffBasler
{
    public static class GraphicsExtension
    {
        private static float Height;

        public static void SetParameters(this System.Drawing.Graphics g, float height)
        {
            Height = height;
        }

        public static void SetTransform(this System.Drawing.Graphics g)
        {
            g.PageUnit = System.Drawing.GraphicsUnit.Millimeter;
            g.TranslateTransform(0, Height);
            g.ScaleTransform(1.0f, Height - 1.0f);
        }

        public static void DrawRegion(this System.Drawing.Graphics g, System.Drawing.Pen pen, System.Collections.ObjectModel.ReadOnlyCollection<ViDi2.Point> outer, int width, int height)
        {
            foreach (ViDi2.Point p in outer)
            {
                g.DrawEllipse(pen, (float)p.X, (float)p.Y, width, height);
                g.ResetTransform();
                //ToPointF((float)p.X, (float)p.Y);
            }
        }

        public static void  DrawPoint(this System.Drawing.Graphics g, System.Drawing.Pen pen, Vector3 Position)
        {
            g.SetTransform();
            System.Drawing.PointF p =  Position.ToPointF;
            g.DrawEllipse(pen, p.X - 1, p.Y - 1, 2, 2);
            g.ResetTransform();
        }
    }
}
