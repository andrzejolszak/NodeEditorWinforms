﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AnimateForms.Core
{
    public class Animate
    {
        public delegate float Function(float t, float b, float c, float d);
        private readonly HashSet<(string, string)> _animating = new HashSet<(string, string)>();

        private int _prevAnimatingCount = 0;
        
        public bool AnimationUpdated 
        {
            get
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

        public async Task<bool> Ease(string name, int initial, Action<int> setter, Function easing, int duration, int target)
        {
            if (_animating.Contains((name, "ease")))
                return false;
            else
                _animating.Add((name, "ease"));

            int dif = target - initial;
            if (dif == 0)
            {
                _animating.Remove((name, "ease"));
                return false;
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

            _animating.Remove((name, "ease"));
            return true;
        }

        public async Task<bool> Ease(string name, float initial, Action<float> setter, Function easing, int duration, float target)
        {
            if (_animating.Contains((name, "ease")))
                return false;
            else
                _animating.Add((name, "ease"));

            float dif = target - initial;
            if (dif == 0)
            {
                _animating.Remove((name, "ease"));
                return false;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                setter(easing(time, initial, dif, duration));
            }

            _animating.Remove((name, "ease"));
            return true;
        }

        public async Task<bool> Ease(string name, double initial, Action<double> setter, Function easing, int duration, double target)
        {
            if (_animating.Contains((name, "ease")))
                return false;
            else
                _animating.Add((name, "ease"));

            double dif = target - initial;
            if (dif == 0)
            {
                _animating.Remove((name, "ease"));
                return false;
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

            _animating.Remove((name, "ease"));
            return true;
        }

        public async Task<bool> Move(Control control, Function easing, int duration, Point moveTo)
        {
            if (_animating.Contains((control.Name, "move")))
                return false;
            else
                _animating.Add((control.Name, "move"));

            Point location = control.Location;
            int yDif = moveTo.Y - location.Y;
            int xDif = moveTo.X - location.X;
            if (yDif == 0 && xDif == 0)
            {
                _animating.Remove((control.Name, "move"));
                return false;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                control.Location = new Point((int)easing(time, location.X, xDif, duration),
                                             (int)easing(time, location.Y, yDif, duration));
            }

            _animating.Remove((control.Name, "move"));
            return true;
        }

        public async Task<bool> Move(Options o, Point moveTo)
        {
            await Task.Delay(o.Delay);
            Point destination = moveTo;

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Move(o.Controls[i], o.Easings[i % o.Easings.Length], o.Duration, destination);
                destination = new Point(moveTo.X, moveTo.Y);
                await Task.Delay(o.Interval);
            }

            bool success = await Move(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                      o.Duration, destination);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Move(Options o, Point moveTo, Point offset)
        {
            await Task.Delay(o.Delay);
            Point destination = moveTo;

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Move(o.Controls[i], o.Easings[i % o.Easings.Length], o.Duration, destination);
                destination = new Point(moveTo.X + (offset.X * (i + 1)),
                                        moveTo.Y + (offset.Y * (i + 1)));
                await Task.Delay(o.Interval);
            }

            bool success = await Move(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                      o.Duration, destination);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> MoveRelative(Control control, Function easing, int duration, Point offset)
        {
            if (offset.X == 0 && offset.Y == 0) return false;
            Point prevPoint = new Point(0, 0);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                Point newPoint = new Point((int)easing(time, 0, offset.X, duration),
                                           (int)easing(time, 0, offset.Y, duration));

                control.Location = new Point(control.Location.X + (newPoint.X - prevPoint.X),
                                             control.Location.Y + (newPoint.Y - prevPoint.Y));

                prevPoint = newPoint;
            }

            return true;
        }

        public async Task<bool> MoveRelative(Options o, Point offset)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = MoveRelative(o.Controls[i], o.Easings[i % o.Easings.Length], o.Duration, offset);
                await Task.Delay(o.Interval);
            }

            bool success = await MoveRelative(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                               o.Duration, offset);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Recolor(Control control, Function easing, int duration, Color colorTo, bool backColor = true)
        {
            if (_animating.Contains((control.Name, "recolor")))
                return false;
            else
                _animating.Add((control.Name, "recolor"));

            Color color;
            if (backColor) color = control.BackColor;
            else color = control.ForeColor;
            if (color == colorTo)
            {
                _animating.Remove((control.Name, "recolor"));
                return false;
            }

            int aDif = colorTo.A - color.A;
            int rDif = colorTo.R - color.R;
            int gDif = colorTo.G - color.G;
            int bDif = colorTo.B - color.B;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                Color newColor;
                newColor = Color.FromArgb((int)easing(time, color.A, aDif, duration),
                                          (int)easing(time, color.R, rDif, duration),
                                          (int)easing(time, color.G, gDif, duration),
                                          (int)easing(time, color.B, bDif, duration));
                if (backColor)
                    control.BackColor = newColor;
                else
                    control.ForeColor = newColor;
            }

            _animating.Remove((control.Name, "recolor"));
            return true;
        }

        public async Task<bool> Recolor(string name, Color initial, Action<Color> setter, Function easing, int duration, Color colorTo, int intervalMs = 11)
        {
            if (_animating.Contains((name, "recolor")))
                return false;
            else
                _animating.Add((name, "recolor"));

            Color color = initial;
            if (color == colorTo)
            {
                _animating.Remove((name, "recolor"));
                return false;
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
                newColor = Color.FromArgb((int)easing(time, color.A, aDif, duration),
                                          (int)easing(time, color.R, rDif, duration),
                                          (int)easing(time, color.G, gDif, duration),
                                          (int)easing(time, color.B, bDif, duration));
                
                setter(newColor);
            }

            _animating.Remove((name, "recolor"));
            setter(colorTo);

            return true;
        }

        public async Task<bool> Recolor(Control control, Function easing, int duration, Helpers.HSV colorTo, bool backColor = true)
        {
            if (_animating.Contains((control.Name, "recolor")))
                return false;
            else
                _animating.Add((control.Name, "recolor"));

            Helpers.HSV color;
            if (backColor) color = Helpers.RGBtoHSV(control.BackColor);
            else color = Helpers.RGBtoHSV(control.ForeColor);
            if (color.Hue == colorTo.Hue &&
                color.Hue == colorTo.Saturation &&
                color.Value == colorTo.Value)
            {
                _animating.Remove((control.Name, "recolor"));
                return false;
            }

            int hDif = (int)(colorTo.Hue - color.Hue);
            float sDif = colorTo.Saturation - color.Saturation;
            float vDif = colorTo.Value - color.Value;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                Helpers.HSV newHSV = new Helpers.HSV
                {
                    Hue = easing(time, color.Hue, hDif, duration),
                    Saturation = easing(time, color.Saturation, sDif, duration),
                    Value = easing(time, color.Value, vDif, duration)
                };
                Color newColor = Helpers.HSVtoRGB(newHSV);

                if (backColor)
                    control.BackColor = newColor;
                else
                    control.ForeColor = newColor;
            }

            _animating.Remove((control.Name, "recolor"));
            return true;
        }

        public async Task<bool> Recolor(Options o, Color color, bool backColor = true)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Recolor(o.Controls[i], o.Easings[i % o.Easings.Length], o.Duration,
                            color, backColor);
                await Task.Delay(o.Interval);
            }

            bool success = await Recolor(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                         o.Duration, color, backColor);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Recolor(Options o, Color[] colors, bool backColor = true)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Recolor(o.Controls[i], o.Easings[i % o.Easings.Length],
                            o.Duration, colors[i % colors.Length], backColor);
                await Task.Delay(o.Interval);
            }

            bool success = await Recolor(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                         o.Duration, colors[maxIndex % colors.Length], backColor);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Recolor(Options o, Helpers.HSV color, bool backColor = true)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Recolor(o.Controls[i], o.Easings[i % o.Easings.Length],
                            o.Duration, color, backColor);
                await Task.Delay(o.Interval);
            }

            bool success = await Recolor(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                         o.Duration, color, backColor);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Recolor(Options o, Helpers.HSV[] colors, bool backColor = true)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Recolor(o.Controls[i], o.Easings[i % o.Easings.Length],
                            o.Duration, colors[i % colors.Length], backColor);
                await Task.Delay(o.Interval);
            }

            bool success = await Recolor(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                         o.Duration, colors[maxIndex % colors.Length], backColor);
            await Task.Delay(o.EndDelay);
            return success;
        }

        public async Task<bool> Resize(Control control, Function easing, int duration, Size sizeTo)
        {
            if (_animating.Contains((control.Name, "resize")))
                return false;
            else
                _animating.Add((control.Name, "resize"));

            Size size = control.Size;
            int heightDif = sizeTo.Height - size.Height;
            int widthDif = sizeTo.Width - size.Width;
            if (widthDif == 0 && heightDif == 0)
            {
                _animating.Remove((control.Name, "resize"));
                return false;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < duration)
            {
                await Task.Delay(1);
                int time = (int)stopwatch.ElapsedMilliseconds;
                if (time > duration) time = duration;

                control.Size = new Size((int)easing(time, size.Width, widthDif, duration),
                                        (int)easing(time, size.Height, heightDif, duration));
            }

            _animating.Remove((control.Name, "resize"));
            return true;
        }

        public async Task<bool> Resize(Options o, Size sizeTo)
        {
            await Task.Delay(o.Delay);

            int maxIndex = o.Controls.Length - 1;
            for (int i = 0; i < maxIndex; i++)
            {
                _ = Resize(o.Controls[i], o.Easings[i % o.Easings.Length], o.Duration, sizeTo);
                await Task.Delay(o.Interval);
            }

            bool success = await Resize(o.Controls.Last(), o.Easings[maxIndex % o.Easings.Length],
                                         o.Duration, sizeTo);
            await Task.Delay(o.EndDelay);
            return success;
        }
    }
}
