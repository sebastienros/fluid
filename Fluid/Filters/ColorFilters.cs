using System;
using System.Drawing;
using System.Linq;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class ColorFilters
    {
        public static FilterCollection WithColorFilters(this FilterCollection filters)
        {
            filters.AddFilter("color_to_rgb", ToRgb);
            filters.AddFilter("color_to_hex", ToHex);
            filters.AddFilter("color_to_hsl", ToHsl);

            return filters;
        }

        public static FluidValue ToRgb(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (HexColor.TryParse(value, out RgbColor color))
            {
                return new StringValue(color.ToString());
            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                var rgbColor = RgbColor.FromHsl(hslColor);

                return new StringValue(rgbColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        public static FluidValue ToHex(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (RgbColor.TryParse(value, out Color color) == false)
            {
                return NilValue.Empty;
            }

            var hexColor = new HexColor(color);

            return new StringValue(hexColor.ToString());
        }

        public static FluidValue ToHsl(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (RgbColor.TryParse(value, out Color color) == false && HexColor.TryParse(value, out color) == false)
            {
                return NilValue.Empty;
            }

            var hslColor = HslColor.FromRgb(color);

            return new StringValue(hslColor.ToString());
        }

        private struct HexColor
        {
            public HexColor(Color color)
            {
                R = color.R.ToString("X2", null);
                G = color.G.ToString("X2", null);
                B = color.B.ToString("X2", null);
            }

            public string R { get; }

            public string G { get; }

            public string B { get; }

            public static bool TryParse(string value, out RgbColor color)
            {
                color = RgbColor.Empty;

                if ((value == null) || (value.Length == 0))
                {
                    return false;
                }

                if ((value[0] == '#') && (value.Length == 7 || value.Length == 4))
                {
                    if (value.Length == 7)
                    {
                        color = new RgbColor(Color.FromArgb(Convert.ToInt32(value.Substring(1, 2), 16),
                                           Convert.ToInt32(value.Substring(3, 2), 16),
                                           Convert.ToInt32(value.Substring(5, 2), 16)));
                    }
                    else
                    {
                        var r = Char.ToString(value[1]);
                        var g = Char.ToString(value[2]);
                        var b = Char.ToString(value[3]);
                        color = new RgbColor(Color.FromArgb(Convert.ToInt32(r + r, 16),
                                           Convert.ToInt32(g + g, 16),
                                           Convert.ToInt32(b + b, 16)));
                    }

                    return true;
                }

                return false;
            }

            public static bool TryParse(string value, out Color color)
            {
                color = Color.Empty;

                if (String.IsNullOrEmpty(value))
                {
                    return false;
                }

                if (value[0] == '#')
                {
                    string red, blue, green;
                    switch (value.Length)
                    {
                        case 4:
                            red = Char.ToString(value[1]);
                            green = Char.ToString(value[2]);
                            blue = Char.ToString(value[3]);
                            if (IsHexadecimal(red) && IsHexadecimal(green) && IsHexadecimal(blue))
                            {
                                color = Color.FromArgb(Convert.ToInt32(red + red, 16), Convert.ToInt32(green + green, 16), Convert.ToInt32(blue + blue, 16));

                                return true;
                            }

                            break;
                        case 7:
                            red = value.Substring(1, 2);
                            green = value.Substring(3, 2);
                            blue = value.Substring(5, 2);
                            if (IsHexadecimal(red) && IsHexadecimal(green) && IsHexadecimal(blue))
                            {
                                color = Color.FromArgb(Convert.ToInt32(red, 16), Convert.ToInt32(green, 16), Convert.ToInt32(blue, 16));

                                return true;
                            }
                            
                            break;
                    }
                }

                return false;

                static bool IsHexadecimal(string value) => value.All(c => "0123456789abcdefABCDEF".Contains(c));
            }

            public override string ToString() => $"#{R}{G}{B}".ToLower();
        }

        private struct RgbColor
        {
            private const float DefaultTransperency = 1.0f;
            
            private static readonly char[] _colorSeparators = new[] { '(', ',', ' ', ')' };

            public static readonly RgbColor Empty = default;

            public RgbColor(Color color) : this(color.R, color.G, color.B)
            {

            }

            public RgbColor(int red, int green, int blue, float alpha = DefaultTransperency)
            {
                if (red < 0 || red > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(red), "The red value must in rage [0-255]");
                }

                if (green < 0 || green > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(green), "The green value must in rage [0-255]");
                }

                if (blue < 0 || blue > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(blue), "The blue value must in rage [0-255]");
                }

                if (alpha < 0.0f || alpha > 1.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(alpha), "The alpha value must in rage [0-1]");
                }

                R = red;
                G = green;
                B = blue;
                A = alpha;
            }

            public float A { get; }

            public int R { get; }

            public int G { get; }

            public int B { get; }

            public static bool TryParse(string value, out Color color)
            {
                if ((value.StartsWith("rgb(") || value.StartsWith("rgba(")) && value.EndsWith(")"))
                {
                    var rgbColor = value.Split(_colorSeparators, StringSplitOptions.RemoveEmptyEntries);
                    if (rgbColor.Length == 4 &&
                        Int32.TryParse(rgbColor[1], out int red) &&
                        Int32.TryParse(rgbColor[2], out int green) && 
                        Int32.TryParse(rgbColor[3], out int blue))
                    {
                        color = Color.FromArgb(red, green, blue);

                        return true;
                    }

                    if (rgbColor.Length == 5 &&
                        Int32.TryParse(rgbColor[1], out red) &&
                        Int32.TryParse(rgbColor[2], out green) &&
                        Int32.TryParse(rgbColor[3], out blue) &&
                        Single.TryParse(rgbColor[4], out float alpha))
                    {
                        color = Color.FromArgb(Convert.ToInt32(alpha * 255), red, green, blue);

                        return true;
                    }
                }

                color = Color.Empty;

                return false;
            }

            // https://www.codeproject.com/Articles/19045/Manipulating-colors-in-NET-Part-1
            public static RgbColor FromHsl(HslColor color)
            {
                var h = Convert.ToDouble(color.H);
                var s = Convert.ToDouble(color.S) / 100.0;
                var l = Convert.ToDouble(color.L) / 100.0;
                if (s == 0)
                {
                    return new RgbColor(
                        Convert.ToInt32(Math.Round(l * 255.0)),
                        Convert.ToInt32(Math.Round(l * 255.0)),
                        Convert.ToInt32(Math.Round(l * 255.0)),
                        color.A
                        );
                }
                else
                {
                    var q = (l < 0.5) ? (l * (1.0 + s)) : (l + s - (l * s));
                    var p = (2.0 * l) - q;
                    var Hk = h / 360.0;
                    var T = new double[3];
                    T[0] = Hk + (1.0 / 3.0);
                    T[1] = Hk;
                    T[2] = Hk - (1.0 / 3.0);

                    for (int i = 0; i < 3; i++)
                    {
                        if (T[i] < 0)
                        {
                            T[i] += 1.0;
                        }

                        if (T[i] > 1)
                        {
                            T[i] -= 1.0;
                        }

                        if (T[i] * 6 < 1)
                        {
                            T[i] = p + ((q - p) * 6.0 * T[i]);
                        }
                        else if (T[i] * 2.0 < 1)
                        {
                            T[i] = q;
                        }
                        else if (T[i] * 3.0 < 2)
                        {
                            T[i] = p + (q - p) * ((2.0 / 3.0) - T[i]) * 6.0;
                        }
                        else
                        {
                            T[i] = p;
                        }
                    }

                    return new RgbColor(
                        Convert.ToInt32(Math.Round(T[0] * 255.0, 2)),
                        Convert.ToInt32(Math.Round(T[1] * 255.0, 2)),
                        Convert.ToInt32(Math.Round(T[2] * 255.0, 2)),
                        color.A
                        );
                }
            }

            public override string ToString() => A == DefaultTransperency
                ? $"rgb({R}, {G}, {B})"
                : $"rgba({R}, {G}, {B}, {Math.Round(A, 1)})";
        }

        private struct HslColor
        {
            private const float DefaultTransperency = 1.0f;

            private static readonly char[] _colorSeparators = new[] { '(', ',', ' ', ')' };
            
            public static readonly HslColor Empty = default;

            public HslColor(int hue, int saturation, int luminosity, float alpha = DefaultTransperency)
            {
                if (hue < 0 || hue > 360)
                {
                    throw new ArgumentOutOfRangeException(nameof(hue), "The hue value must in rage [0-360]");
                }

                if (saturation < 0 || saturation > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(saturation), "The saturation value must in rage [0-100]");
                }

                if (luminosity < 0 || luminosity > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(luminosity), "The luminosity value must in rage [0-100]");
                }

                if (alpha < 0.0f || alpha > 1.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(alpha), "The alpha value must in rage [0-1]");
                }

                H = hue;
                S = saturation;
                L = luminosity;
                A = alpha;
            }

            public int H { get; }

            public int S { get; }

            public int L { get; }

            public float A { get; }

            public static bool TryParse(string value, out HslColor color)
            {
                if ((value.StartsWith("hsl(") || value.StartsWith("hsla(")) && value.EndsWith(")"))
                {
                    var hslColor = value.Split(_colorSeparators, StringSplitOptions.RemoveEmptyEntries);
                    if (hslColor.Length == 4 && hslColor[2].EndsWith("%") && hslColor[3].EndsWith("%") &&
                        Int32.TryParse(hslColor[1], out int hue) &&
                        Int32.TryParse(hslColor[2].TrimEnd('%'), out int saturation) &&
                        Int32.TryParse(hslColor[3].TrimEnd('%'), out int luminosity))
                    {
                        color = new HslColor(hue, saturation, luminosity);

                        return true;
                    }

                    if (hslColor.Length == 5 && hslColor[2].EndsWith("%") && hslColor[3].EndsWith("%") &&
                        Int32.TryParse(hslColor[1], out hue) &&
                        Int32.TryParse(hslColor[2].TrimEnd('%'), out saturation) &&
                        Int32.TryParse(hslColor[3].TrimEnd('%'), out luminosity) &&
                        Single.TryParse(hslColor[4], out float alpha))
                    {
                        color = new HslColor(hue, saturation, luminosity, alpha);

                        return true;
                    }
                }

                color = HslColor.Empty;

                return false;
            }

            // https://www.codeproject.com/Articles/19045/Manipulating-colors-in-NET-Part-1
            public static HslColor FromRgb(Color color)
            {
                var r = color.R / 255.0;
                var g = color.G / 255.0;
                var b = color.B / 255.0;
                var a = color.A / 255.0f;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var h = 0.0;
                if (max == r && g >= b)
                {
                    h = 60 * (g - b) / (max - min);
                }
                else if (max == r && g < b)
                {
                    h = 60 * (g - b) / (max - min) + 360;
                }
                else if (max == g)
                {
                    h = 60 * (b - r) / (max - min) + 120;
                }
                else if (max == b)
                {
                    h = 60 * (r - g) / (max - min) + 240;
                }

                if (double.IsNaN(h))
                {
                    h = 0;
                }

                var s = (max == 0) ? 0.0 : (1.0 - (min / max));
                var l = (max + min) / 2;

                return new HslColor(Convert.ToInt32(h), Convert.ToInt32(s * 100.0), Convert.ToInt32(l * 100.0), a);
            }

            public override string ToString() => A == DefaultTransperency
                ? $"hsl({H}, {S}%, {L}%)"
                : $"hsla({H}, {S}%, {L}%, {Math.Round(A, 1)})";
        }
    }
}
