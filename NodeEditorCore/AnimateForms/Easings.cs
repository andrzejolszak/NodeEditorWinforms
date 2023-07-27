namespace AnimateForms.Core
{
    public static class Easings
    {
        public static Animate.Function[] AllEasings = new Animate.Function[]
        {
            Linear, QuadIn, QuadOut, QuadInOut, CubicIn, CubicOut, CubicInOut,
            QuartIn, QuartOut, QuartInOut, QuintIn, QuintOut, QuintInOut, SinIn,
            SinOut, SinInOut, ExpIn, ExpOut, ExpInOut, CircIn, CircOut, CircInOut
        };

        // Easing equations pulled from http://gizma.com/easing/

        public static double Linear(double t, double b, double c, double d)
        {
            return c * t / d + b;
        }

        public static double QuadIn(double t, double b, double c, double d)
        {
            t /= d;
            return c * t * t + b;
        }

        public static double QuadOut(double t, double b, double c, double d)
        {
            t /= d;
            return -c * t * (t - 2) + b;
        }

        public static double QuadInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(c / 2 * t * t + b);
            t--;
            return -c / 2 * (t * (t - 2) - 1) + b;
        }

        public static double CubicIn(double t, double b, double c, double d)
        {
            t /= d;
            return c * t * t * t + b;
        }

        public static double CubicOut(double t, double b, double c, double d)
        {
            t /= d;
            t--;
            return c * (t * t * t + 1) + b;
        }

        public static double CubicInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(c / 2 * t * t * t + b);
            t -= 2;
            return c / 2 * (t * t * t + 2) + b;
        }

        public static double QuartIn(double t, double b, double c, double d)
        {
            t /= d;
            return c * t * t * t * t + b;
        }

        public static double QuartOut(double t, double b, double c, double d)
        {
            t /= d;
            t--;
            return -c * (t * t * t * t - 1) + b;
        }

        public static double QuartInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(c / 2 * t * t * t * t + b);
            t -= 2;
            return -c / 2 * (t * t * t * t - 2) + b;
        }

        public static double QuintIn(double t, double b, double c, double d)
        {
            t /= d;
            return c * t * t * t * t * t + b;
        }

        public static double QuintOut(double t, double b, double c, double d)
        {
            t /= d;
            t--;
            return c * (t * t * t * t * t + 1) + b;
        }

        public static double QuintInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(c / 2 * t * t * t * t * t + b);
            t -= 2;
            return c / 2 * (t * t * t * t * t + 2) + b;
        }

        public static double SinIn(double t, double b, double c, double d)
        {
            return (double)(-c * Math.Cos(t / d * (Math.PI / 2)) + c + b);
        }

        public static double SinOut(double t, double b, double c, double d)
        {
            return (double)(c * Math.Sin(t / d * (Math.PI / 2)) + b);
        }

        public static double SinInOut(double t, double b, double c, double d)
        {
            return (double)(-c / 2 * (Math.Cos(Math.PI * t / d) - 1) + b);
        }

        public static double ExpIn(double t, double b, double c, double d)
        {
            return (double)(c * Math.Pow(2, 10 * (t / d - 1)) + b);
        }

        public static double ExpOut(double t, double b, double c, double d)
        {
            return (double)(c * (-Math.Pow(2, -10 * t / d) + 1) + b);
        }

        public static double ExpInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(c / 2 * Math.Pow(2, 10 * (t - 1)) + b);
            t--;
            return (double)(c / 2 * (-Math.Pow(2, -10 * t) + 2) + b);
        }

        public static double CircIn(double t, double b, double c, double d)
        {
            t /= d;
            return (double)(-c * (Math.Sqrt(1 - t * t) - 1) + b);
        }

        public static double CircOut(double t, double b, double c, double d)
        {
            t /= d;
            t--;
            return (double)(c * Math.Sqrt(1 - t * t) + b);
        }

        public static double CircInOut(double t, double b, double c, double d)
        {
            t /= d / 2;
            if (t < 1) return (double)(-c / 2 * (Math.Sqrt(1 - t * t) - 1) + b);
            t -= 2;
            return (double)(c / 2 * (Math.Sqrt(1 - t * t) + 1) + b);
        }
    }
}
