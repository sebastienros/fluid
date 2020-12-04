using System;
using System.Drawing;
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
            var htmlColor = input.ToStringValue();
            var rgbColor = ColorTranslator.FromHtml(htmlColor);
            if (rgbColor == Color.Empty)
            {
                return NilValue.Empty;
            }

            return new StringValue($"rgb({rgbColor.R}, {rgbColor.G}, {rgbColor.B})");
        }

        public static FluidValue ToHex(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var color = input.ToStringValue();
            if (!color.StartsWith("rgb(") && color.EndsWith(")") && color.Length >= 10)
            {
                return NilValue.Empty;
            }

            var rgbColor = color.Substring(4, color.Length - 5).Split(',');
            if (rgbColor.Length != 3)
            {
                return NilValue.Empty;
            }

            var red = Convert.ToInt32(rgbColor[0]);
            var green = Convert.ToInt32(rgbColor[1]);
            var blue = Convert.ToInt32(rgbColor[2]);
            var htmlColor = ColorTranslator.ToHtml(Color.FromArgb(red, green, blue));
            if (htmlColor == String.Empty)
            {
                return NilValue.Empty;
            }

            return new StringValue(htmlColor.ToLower());
        }

        public static FluidValue ToHsl(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var color = input.ToStringValue();
            if (!(color.StartsWith("#") || color.StartsWith("rgb(")))
            {
                return NilValue.Empty;
            }

            var rgbColor = color.StartsWith("#")
                ? ColorTranslator.FromHtml(color)
                : GetRgbColor(color);
            var hslColor = HslColorTranslator.FromRgb(rgbColor);

            return new StringValue($"({hslColor.H}, {Convert.ToInt32(hslColor.S * 100.0)}%, {Convert.ToInt32(hslColor.L * 100.0)}%)");
        }

        private static Color GetRgbColor(string rgbColorString)
        {
            if (!(rgbColorString.StartsWith("rgb(") && rgbColorString.EndsWith(")") && rgbColorString.Length > 13))
            {
                return Color.Empty;
            }

            var rgbColor = rgbColorString.Substring(4, rgbColorString.Length - 5).Split(',');
            if (rgbColor.Length != 3)
            {
                return Color.Empty;
            }

            var red = Convert.ToInt32(rgbColor[0]);
            var green = Convert.ToInt32(rgbColor[1]);
            var blue = Convert.ToInt32(rgbColor[2]);

            return Color.FromArgb(red, green, blue);
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
        }

        private static class HslColorTranslator
        {
            public static HslColor FromRgb(Color color)
            {
                var r = ((double)color.R / 255.0);
                var g = ((double)color.G / 255.0);
                var b = ((double)color.B / 255.0);
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
        }

#if !NET461
        private static class ColorTranslator
        {
            public static Color FromHtml(string htmlColor)
            {
                var color = Color.Empty;
                if ((htmlColor == null) || (htmlColor.Length == 0))
                {
                    return color;
                }

                if ((htmlColor[0] == '#') && (htmlColor.Length == 7 || htmlColor.Length == 4))
                {
                    if (htmlColor.Length == 7)
                    {
                        color = Color.FromArgb(Convert.ToInt32(htmlColor.Substring(1, 2), 16),
                                           Convert.ToInt32(htmlColor.Substring(3, 2), 16),
                                           Convert.ToInt32(htmlColor.Substring(5, 2), 16));
                    }
                    else
                    {
                        var r = Char.ToString(htmlColor[1]);
                        var g = Char.ToString(htmlColor[2]);
                        var b = Char.ToString(htmlColor[3]);
                        color = Color.FromArgb(Convert.ToInt32(r + r, 16),
                                           Convert.ToInt32(g + g, 16),
                                           Convert.ToInt32(b + b, 16));
                    }
                }

                return color;
            }

            public static string ToHtml(Color color)
            {
                var colorString = String.Empty;
                if (color.IsEmpty)
                {
                    return colorString;
                }

                colorString = "#" + color.R.ToString("X2", null) + color.G.ToString("X2", null) + color.B.ToString("X2", null);

                return colorString;
            }
        }
#endif
    }
}
