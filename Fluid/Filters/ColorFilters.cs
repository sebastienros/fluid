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
