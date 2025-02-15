﻿namespace AnimateForms.Core
{
    public class Animate
    {
        public static Animate? Instance = new Animate();

        public delegate double Function(double t, double b, double c, double d);
        private readonly HashSet<(string, string)> _animating = new HashSet<(string, string)>();

        private int _prevAnimatingCount = 0;
        
        public bool AnimationUpdated 
        {
            get
            {
                lock (this._animating)
                {
                    int currCount = this._animating.Count;
                    if (currCount > 0 || this._prevAnimatingCount != currCount)
                    {
                        this._prevAnimatingCount = currCount;
                        return true;
                    }

                    return false;
                }
            }
        }

        public async Task<bool> Ease(string name, int initial, Action<int> setter, Function easing, int duration, int target)
        {
            int dif = target - initial;
            lock (this._animating)
            {
                if (!_animating.Add((name, "ease")))
                    return false;

                if (dif == 0)
                {
                    _animating.Remove((name, "ease"));
                    return false;
                }
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                setter((int)easing(time, initial, dif, duration));
            }

            lock (this._animating)
            {
                _animating.Remove((name, "ease"));
            }

            return true;
        }

        public async Task<bool> Ease(string name, double initial, Action<double> setter, Function easing, int duration, double target)
        {
            double dif = target - initial;
            lock (this._animating)
            {
                if (!_animating.Add((name, "ease")))
                    return false;

                if (dif == 0)
                {
                    _animating.Remove((name, "ease"));
                    return false;
                }
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                setter((double)easing(time, (float)initial, (float)dif, duration));
            }
            lock (this._animating)
            {
                _animating.Remove((name, "ease"));
            }
            return true;
        }

        public async Task<bool> Recolor(string name, Color initial, Action<Color> setter, Function easing, int duration, Color colorTo, int intervalMs = 11)
        {
            Color color = initial;
            lock (this._animating)
            {
                if (!_animating.Add((name, "recolor")))
                    return false;

                if (color == colorTo)
                {
                    _animating.Remove((name, "recolor"));
                    return false;
                }
            }

            int aDif = colorTo.A - color.A;
            int rDif = colorTo.R - color.R;
            int gDif = colorTo.G - color.G;
            int bDif = colorTo.B - color.B;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(intervalMs);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                Color newColor;
                newColor = Color.FromArgb((byte)easing(time, color.A, aDif, duration),
                                          (byte)easing(time, color.R, rDif, duration),
                                          (byte)easing(time, color.G, gDif, duration),
                                          (byte)easing(time, color.B, bDif, duration));

                setter(newColor);
            }

            lock (this._animating)
            {
                _animating.Remove((name, "recolor"));
            }

            setter(colorTo);

            return true;
        }
    }
}
