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
                if (value.StartsWith("rgb(") && value.EndsWith(")"))
                {
                    var rgbColor = value.Substring(4, value.IndexOf(")") - 4).Replace(" ", String.Empty).Split(',');
                    if (rgbColor.Length == 3 &&
                        Int32.TryParse(rgbColor[0], out int red) &&
                        Int32.TryParse(rgbColor[1], out int green) && 
                        Int32.TryParse(rgbColor[2], out int blue))
                    {
                        color = Color.FromArgb(red, green, blue);

                        return true;
                    }
                }

                color = Color.Empty;

                return false;
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
