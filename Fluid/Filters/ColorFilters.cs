using System;
using System.Drawing;
using System.Text.RegularExpressions;
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
            if (!HexColor.TryParse(value, out RgbColor color))
            {
                return NilValue.Empty;
            }

            return new StringValue(color.ToString());
        }

        public static FluidValue ToHex(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (!RgbColor.TryParse(value, out Color color))
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

            public static string FromRgb(Color color) => new HexColor(color).ToString();

            public static Color ToRgb(string color) => RgbColor.FrmHex(color);

            public static bool TryParse(string value, out RgbColor color)
            {
                if (Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}){1,2}$"))
                {
                    var rgbColor = RgbColor.FrmHex(value);
                    color = new RgbColor(rgbColor);

                    return true;
                }
                else
                {
                    color = RgbColor.Empty;

                    return false;
                }
            }

            public static bool TryParse(string value, out Color color)
            {
                if (Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}){1,2}$"))
                {
                    color = RgbColor.FrmHex(value);

                    return true;
                }
                else
                {
                    color = Color.Empty;

                    return false;
                }
            }

            public override string ToString() => $"#{R}{G}{B}".ToLower();
        }

        private struct RgbColor
        {
            public static readonly RgbColor Empty = default;

            public RgbColor(Color color) : this(color.R, color.G, color.B)
            {

            }

            public RgbColor(double red, double green, double blue)
            {
                R = red;
                G = green;
                B = blue;
            }

            public double R { get; }

            public double G { get; }

            public double B { get; }

            public static bool TryParse(string value, out Color color)
            {
                if (Regex.IsMatch(value, @"^rgb\((\d{1,3}), (\d{1,3}), (\d{1,3})\)$"))
                {
                    var rgbColor = value.Substring(4, value.Length - 5).Split(',');
                    var red = Convert.ToInt32(rgbColor[0]);
                    var green = Convert.ToInt32(rgbColor[1]);
                    var blue = Convert.ToInt32(rgbColor[2]);

                    color = Color.FromArgb(red, green, blue);

                    return true;
                }
                else
                {
                    color = Color.Empty;

                    return false;
                }
            }

            public static Color FrmHex(string hexColor)
            {
                var color = Color.Empty;
                if ((hexColor == null) || (hexColor.Length == 0))
                {
                    return color;
                }

                if ((hexColor[0] == '#') && (hexColor.Length == 7 || hexColor.Length == 4))
                {
                    if (hexColor.Length == 7)
                    {
                        color = Color.FromArgb(Convert.ToInt32(hexColor.Substring(1, 2), 16),
                                           Convert.ToInt32(hexColor.Substring(3, 2), 16),
                                           Convert.ToInt32(hexColor.Substring(5, 2), 16));
                    }
                    else
                    {
                        var r = Char.ToString(hexColor[1]);
                        var g = Char.ToString(hexColor[2]);
                        var b = Char.ToString(hexColor[3]);
                        color = Color.FromArgb(Convert.ToInt32(r + r, 16),
                                           Convert.ToInt32(g + g, 16),
                                           Convert.ToInt32(b + b, 16));
                    }
                }

                return color;
            }

            public static string ToHex(Color color)
            {
                var colorString = String.Empty;
                if (color.IsEmpty)
                {
                    return colorString;
                }

                colorString = "#" + color.R.ToString("X2", null) + color.G.ToString("X2", null) + color.B.ToString("X2", null);

                return colorString;
            }

            public override string ToString() => $"rgb({R}, {G}, {B})";
        }

        private struct HslColor
        {
            public HslColor(double hue, double saturation, double luminosity)
            {
                H = hue;
                S = saturation;
                L = luminosity;
            }

            public double H { get; }

            public double S { get; }

            public double L { get; }

            public static HslColor FromRgb(Color color)
            {
                var r = color.R / 255.0;
                var g = color.G / 255.0;
                var b = color.B / 255.0;
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

                return new HslColor(h, s, l);
            }

            public override string ToString()
                => $"hsl({H}, {Convert.ToInt32(S * 100.0)}%, {Convert.ToInt32(L * 100.0)}%)";
        }
    }
}
