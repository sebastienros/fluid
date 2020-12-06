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
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                var rgbColor = RgbColor.FromHex(hexColor);

                return new StringValue(rgbColor.ToString());
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
            if (RgbColor.TryParse(value, out RgbColor rgbColor))
            {
                var hexColor = HexColor.FromRgb(rgbColor);

                return new StringValue(hexColor.ToString());
            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                var hexColor = HexColor.FromHsl(hslColor);

                return new StringValue(hexColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        public static FluidValue ToHsl(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                var hslColor = HslColor.FromHex(hexColor);

                return new StringValue(hslColor.ToString());
            }
            else if (RgbColor.TryParse(value, out RgbColor rgbColor))
            {
                var hslColor = HslColor.FromRgb(rgbColor);

                return new StringValue(hslColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        private struct HexColor
        {
            public static readonly HexColor Empty = default;

            public HexColor(string red, string green, string blue)
            {
                if (!IsHexadecimal(red))
                {
                    throw new ArgumentNullException(nameof(red), "The red value is not hexadecimal");
                }

                if (!IsHexadecimal(green))
                {
                    throw new ArgumentNullException(nameof(green), "The green value is not hexadecimal");
                }

                if (!IsHexadecimal(blue))
                {
                    throw new ArgumentNullException(nameof(blue), "The blue value is not hexadecimal");
                }

                R = red;
                G = green;
                B = blue;
            }

            public string R { get; }

            public string G { get; }

            public string B { get; }

            public static bool TryParse(string value, out HexColor color)
            {
                color = HexColor.Empty;

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
                                color = new HexColor(red, green, blue);

                                return true;
                            }

                            break;
                        case 7:
                            red = value.Substring(1, 2);
                            green = value.Substring(3, 2);
                            blue = value.Substring(5, 2);
                            if (IsHexadecimal(red) && IsHexadecimal(green) && IsHexadecimal(blue))
                            {
                                color = new HexColor(red, green, blue);

                                return true;
                            }

                            break;
                    }
                }

                return false;
            }


            public static HexColor FromRgb(RgbColor color)
            {
                var red = color.R.ToString("X2", null);
                var green = color.G.ToString("X2", null);
                var blue = color.B.ToString("X2", null);

                return new HexColor(red, green, blue);
            }

            public static HexColor FromHsl(HslColor color) => FromRgb(RgbColor.FromHsl(color));

            public override string ToString() => $"#{R}{G}{B}".ToLower();

            private static bool IsHexadecimal(string value) => value.All(c => "0123456789abcdefABCDEF".Contains(c));
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

            public static bool TryParse(string value, out RgbColor color)
            {
                if ((value.StartsWith("rgb(") || value.StartsWith("rgba(")) && value.EndsWith(")"))
                {
                    var rgbColor = value.Split(_colorSeparators, StringSplitOptions.RemoveEmptyEntries);
                    if (rgbColor.Length == 4 &&
                        Int32.TryParse(rgbColor[1], out int red) &&
                        Int32.TryParse(rgbColor[2], out int green) &&
                        Int32.TryParse(rgbColor[3], out int blue))
                    {
                        color = new RgbColor(red, green, blue);

                        return true;
                    }

                    if (rgbColor.Length == 5 &&
                        Int32.TryParse(rgbColor[1], out red) &&
                        Int32.TryParse(rgbColor[2], out green) &&
                        Int32.TryParse(rgbColor[3], out blue) &&
                        Single.TryParse(rgbColor[4], out float alpha))
                    {
                        color = new RgbColor(red, green, blue, alpha);

                        return true;
                    }
                }

                color = RgbColor.Empty;

                return false;
            }

            public static RgbColor FromHex(HexColor color)
            {
                if (color.R.Length == 1)
                {
                    var red = Convert.ToInt32(color.R + color.R, 16);
                    var green = Convert.ToInt32(color.G + color.G, 16);
                    var blue = Convert.ToInt32(color.B + color.B, 16);

                    return new RgbColor(red, green, blue);
                }
                else
                {
                    var red = Convert.ToInt32(color.R, 16);
                    var green = Convert.ToInt32(color.G, 16);
                    var blue = Convert.ToInt32(color.B, 16);

                    return new RgbColor(red, green, blue);
                }
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
            public static HslColor FromRgb(RgbColor color)
            {
                var r = color.R / 255.0;
                var g = color.G / 255.0;
                var b = color.B / 255.0;
                var a = color.A / 1.0f;
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

            public static HslColor FromHex(HexColor color)=> FromRgb(RgbColor.FromHex(color));

            public override string ToString() => A == DefaultTransperency
                ? $"hsl({H}, {S}%, {L}%)"
                : $"hsla({H}, {S}%, {L}%, {Math.Round(A, 1)})";
        }
    }
}
