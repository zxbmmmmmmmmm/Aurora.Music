using Colourful;
using Impressionist.Abstractions;
using System;
using System.Numerics;

namespace Impressionist.Implementations
{
    public static class ColorUtilities
    {
        public static HSVColor RGBVectorToHSVColor(this Vector3 color)
        {
            HSVColor hsv = new HSVColor();

            float max = Math.Max(Math.Max(color.X, color.Y), color.Z);
            float min = Math.Min(Math.Min(color.X, color.Y), color.Z);

            hsv.V = max * 100 / 255;

            if (max == min)
            {
                hsv.H = 0;
                hsv.S = 0;
            }
            else
            {
                hsv.S = (((max - min) / max) * 100);

                hsv.H = 0;

                if (max == color.X)
                {
                    hsv.H = (60 * (color.Y - color.Z) / (max - min));
                    if (hsv.H < 0) hsv.H += 360;
                }
                else if (max == color.Y)
                {
                    hsv.H = (60 * (2 + (color.Z - color.X) / (max - min)));
                    if (hsv.H < 0) hsv.H += 360;
                }
                else if (max == color.Z)
                {
                    hsv.H = (60 * (4 + (color.X - color.Y) / (max - min)));
                    if (hsv.H < 0) hsv.H += 360;
                }

            }
            return hsv;
        }
        public static Vector3 HSVColorToRGBVector(this HSVColor hsv)
        {
            if (hsv.H == 360) hsv.H = 0;
            int Hi = (int)Math.Floor((float)hsv.H / 60) % 6;

            float f = (hsv.H / 60) - Hi;
            float p = (hsv.V / 100) * (1 - (hsv.S / 100));
            float q = (hsv.V / 100) * (1 - f * (hsv.S / 100));
            float t = (hsv.V / 100) * (1 - (1 - f) * (hsv.S / 100));

            p *= 255;
            q *= 255;
            t *= 255;

            Vector3 rgb = Vector3.Zero;

            switch (Hi)
            {
                case 0:
                    rgb = new Vector3(hsv.V * 255 / 100, t, p);
                    break;
                case 1:
                    rgb = new Vector3(q, hsv.V * 255 / 100, p);
                    break;
                case 2:
                    rgb = new Vector3(p, hsv.V * 255 / 100, t);
                    break;
                case 3:
                    rgb = new Vector3(p, q, hsv.V * 255 / 100);
                    break;
                case 4:
                    rgb = new Vector3(t, p, hsv.V * 255 / 100);
                    break;
                case 5:
                    rgb = new Vector3(hsv.V * 255 / 100, p, q);
                    break;
            }

            return rgb;
        }
        internal static Vector3 RGBColorToRGBVector(this RGBColor color)
        {
            return new Vector3((float)color.R, (float)color.G, (float)color.B);
        }
        internal static RGBColor RGBVectorToRGBColor(this Vector3 color)
        {
            return new RGBColor(color.X, color.Y, color.Z);
        }
        internal static Vector3 LABColorToLABVector(this LabColor color)
        {
            return new Vector3((float)color.L, (float)color.a, (float)color.b);
        }
        internal static LabColor LABVectorToLABColor(this Vector3 color)
        {
            return new LabColor(color.X, color.Y, color.Z);
        }

    }
}
