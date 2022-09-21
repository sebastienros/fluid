using Fluid.Values;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Fluid.Filters
{
    public static class ColorFilters
    {
        public static FilterCollection WithColorFilters(this FilterCollection filters)
        {
            filters.AddFilter("color_to_rgb", ToRgb);
            filters.AddFilter("color_to_hex", ToHex);
            filters.AddFilter("color_to_hsl", ToHsl);
            filters.AddFilter("color_extract", ColorExtract);
            filters.AddFilter("color_modify", ColorModify);
            filters.AddFilter("color_brightness", CalculateBrightness);
            filters.AddFilter("color_saturate", ColorSaturate);
            filters.AddFilter("color_desaturate", ColorDesaturate);
            filters.AddFilter("color_lighten", ColorLighten);
            filters.AddFilter("color_darken", ColorDarken);
            filters.AddFilter("color_difference", GetColorDifference);
            filters.AddFilter("brightness_difference", GetColorBrightnessDifference);
            filters.AddFilter("color_contrast", GetColorContrast);

            return filters;
        }

        public static ValueTask<FluidValue> ToRgb(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                var rgbColor = (RgbColor)hexColor;

                return new StringValue(rgbColor.ToString());
            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                var rgbColor = (RgbColor)hslColor;

                return new StringValue(rgbColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ToHex(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (RgbColor.TryParse(value, out RgbColor rgbColor))
            {
                var hexColor = (HexColor)rgbColor;

                return new StringValue(hexColor.ToString());
            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                var hexColor = (HexColor)hslColor;

                return new StringValue(hexColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ToHsl(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                var hslColor = (HslColor)hexColor;

                return new StringValue(hslColor.ToString());
            }
            else if (RgbColor.TryParse(value, out RgbColor rgbColor))
            {
                var hslColor = (HslColor)rgbColor;

                return new StringValue(hslColor.ToString());
            }
            else
            {
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ColorExtract(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            RgbColor rgbColor;
            HslColor hslColor;
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                rgbColor = (RgbColor)hexColor;
                hslColor = (HslColor)hexColor;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                hslColor = (HslColor)rgbColor;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                rgbColor = (RgbColor)hslColor;
            }
            else
            {
                return NilValue.Empty;
            }

            return arguments.At(0).ToStringValue() switch
            {
                "alpha" => new StringValue(rgbColor.A.ToString(CultureInfo.InvariantCulture)),
                "red" => new StringValue(rgbColor.R.ToString(CultureInfo.InvariantCulture)),
                "green" => new StringValue(rgbColor.G.ToString(CultureInfo.InvariantCulture)),
                "blue" => new StringValue(rgbColor.B.ToString(CultureInfo.InvariantCulture)),
                "hue" => new StringValue(hslColor.H.ToString(CultureInfo.InvariantCulture)),
                "saturation" => new StringValue(Convert.ToInt32(hslColor.S * 100.0).ToString(CultureInfo.InvariantCulture)),
                "lightness" => new StringValue(Convert.ToInt32(hslColor.L * 100.0).ToString(CultureInfo.InvariantCulture)),
                _ => NilValue.Empty,
            };
        }

        public static ValueTask<FluidValue> ColorModify(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            bool isRgb = false;
            bool isHsl = false;
            bool isHex = false;
            RgbColor rgbColor;
            HslColor hslColor;
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                isHex = true;
                rgbColor = (RgbColor)hexColor;
                hslColor = (HslColor)hexColor;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                isRgb = true;
                hslColor = (HslColor)rgbColor;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                isHsl = true;
                rgbColor = (RgbColor)hslColor;
            }
            else
            {
                return NilValue.Empty;
            }

            var modifiedValue = arguments.At(1).ToNumberValue();
            if (isRgb)
            {
                hslColor = (HslColor)rgbColor;

                return arguments.At(0).ToStringValue() switch
                {
                    "alpha" => new StringValue(new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue).ToString()),
                    "red" => new StringValue(new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A).ToString()),
                    "green" => new StringValue(new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A).ToString()),
                    "blue" => new StringValue(new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A).ToString()),
                    "hue" => new StringValue(((RgbColor)new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A)).ToString()),
                    "saturation" => new StringValue(((RgbColor)new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A)).ToString()),
                    "lightness" => new StringValue(((RgbColor)new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A)).ToString()),
                    _ => NilValue.Empty,
                };
            }
            else if (isHsl)
            {
                rgbColor = (RgbColor)hslColor;

                return arguments.At(0).ToStringValue() switch
                {
                    "alpha" => new StringValue(((HslColor)new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue)).ToString()),
                    "red" => new StringValue(((HslColor)new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A)).ToString()),
                    "green" => new StringValue(((HslColor)new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A)).ToString()),
                    "blue" => new StringValue(((HslColor)new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A)).ToString()),
                    "hue" => new StringValue(new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A).ToString()),
                    "saturation" => new StringValue(new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A).ToString()),
                    "lightness" => new StringValue(new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A).ToString()),
                    _ => NilValue.Empty,
                };
            }
            else if (isHex)
            {
                rgbColor = (RgbColor)hexColor;
                hslColor = (HslColor)hexColor;

                return arguments.At(0).ToStringValue() switch
                {
                    "alpha" => new StringValue(new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue).ToString()),
                    "red" => new StringValue(((HexColor)new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A)).ToString()),
                    "green" => new StringValue(((HexColor)new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A)).ToString()),
                    "blue" => new StringValue(((HexColor)new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A)).ToString()),
                    "hue" => new StringValue(((HexColor)new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A)).ToString()),
                    "saturation" => new StringValue(((HexColor)new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A)).ToString()),
                    "lightness" => new StringValue(((HexColor)new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A)).ToString()),
                    _ => NilValue.Empty,
                };
            }
            else
            {
                // The code is unreachable
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> CalculateBrightness(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            RgbColor rgbColor;
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                rgbColor = (RgbColor)hexColor;
            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                rgbColor = (RgbColor)hslColor;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {

            }
            else
            {
                return NilValue.Empty;
            }

            var brightness = Convert.ToDouble(rgbColor.R * 299 + rgbColor.G * 587 + rgbColor.B * 114) / 1000.0;

            return NumberValue.Create((decimal) Math.Round(brightness, 2));
        }

        public static ValueTask<FluidValue> ColorSaturate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            bool isHex = false;
            bool isHsl = false;
            bool isRgb = false;
            var hslColor = HslColor.Empty;
            var rgbColor = RgbColor.Empty;
            if (HexColor.TryParse(value, out var hexColor))
            {
                isHex = true;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                isRgb = true;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                isHsl = true;
            }
            else
            {
                return NilValue.Empty;
            }

            if (isHex)
            {
                hslColor = (HslColor)hexColor;

                var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((HexColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
            }
            else if (isHsl)
            {
                var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A).ToString());
            }
            else if (isRgb)
            {
                hslColor = (HslColor)rgbColor;

                var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((RgbColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
            }
            else
            {
                // The code is unreachable
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ColorDesaturate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            bool isHex = false;
            bool isHsl = false;
            bool isRgb = false;
            var hslColor = HslColor.Empty;
            var rgbColor = RgbColor.Empty;
            if (HexColor.TryParse(value, out var hexColor))
            {
                isHex = true;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                isRgb = true;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                isHsl = true;
            }
            else
            {
                return NilValue.Empty;
            }

            if (isHex)
            {
                hslColor = (HslColor)hexColor;

                var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((HexColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
            }
            else if (isHsl)
            {
                var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A).ToString());
            }
            else if (isRgb)
            {
                hslColor = (HslColor)rgbColor;

                var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((RgbColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
            }
            else
            {
                // The code is unreachable
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ColorLighten(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            bool isHex = false;
            bool isHsl = false;
            bool isRgb = false;
            var hslColor = HslColor.Empty;
            var rgbColor = RgbColor.Empty;
            if (HexColor.TryParse(value, out var hexColor))
            {
                isHex = true;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                isRgb = true;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                isHsl = true;
            }
            else
            {
                return NilValue.Empty;
            }

            if (isHex)
            {
                hslColor = (HslColor)hexColor;

                var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((HexColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
            }
            else if (isHsl)
            {
                var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A).ToString());
            }
            else if (isRgb)
            {
                hslColor = (HslColor)rgbColor;

                var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((RgbColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
            }
            else
            {
                // The code is unreachable
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> ColorDarken(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            bool isHex = false;
            bool isHsl = false;
            bool isRgb = false;
            var hslColor = HslColor.Empty;
            var rgbColor = RgbColor.Empty;
            var hexColor = HexColor.Empty;
            if (HexColor.TryParse(value, out hexColor))
            {
                isHex = true;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {
                isRgb = true;
            }
            else if (HslColor.TryParse(value, out hslColor))
            {
                isHsl = true;
            }
            else
            {
                return NilValue.Empty;
            }

            if (isHex)
            {
                hslColor = (HslColor)hexColor;

                var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((HexColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
            }
            else if (isHsl)
            {
                var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A).ToString());
            }
            else if (isRgb)
            {
                hslColor = (HslColor)rgbColor;

                var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

                return new StringValue(((RgbColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
            }
            else
            {
                // The code is unreachable
                return NilValue.Empty;
            }
        }

        public static ValueTask<FluidValue> GetColorDifference(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var rgbColor1 = GetRgbColor(input.ToStringValue());
            var rgbColor2 = GetRgbColor(arguments.At(0).ToStringValue());
            if (rgbColor1.Equals(RgbColor.Empty) || rgbColor2.Equals(RgbColor.Empty))
            {
                return NilValue.Empty;
            }
            else
            {
                var colorDifference = Math.Max(rgbColor1.R, rgbColor2.R) - Math.Min(rgbColor1.R, rgbColor2.R) +
                    Math.Max(rgbColor1.G, rgbColor2.G) - Math.Min(rgbColor1.G, rgbColor2.G) +
                    Math.Max(rgbColor1.B, rgbColor2.B) - Math.Min(rgbColor1.B, rgbColor2.B);

                return NumberValue.Create(colorDifference);
            }
        }

        public static ValueTask<FluidValue> GetColorBrightnessDifference(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var rgbColor1 = GetRgbColor(input.ToStringValue());
            var rgbColor2 = GetRgbColor(arguments.At(0).ToStringValue());
            if (rgbColor1.Equals(RgbColor.Empty) || rgbColor2.Equals(RgbColor.Empty))
            {
                return NilValue.Empty;
            }
            else
            {
                var colorBrightness1 = ((rgbColor1.R * 299) + (rgbColor1.G * 587) + (rgbColor1.B * 114)) / 1000;
                var colorBrightness2 = ((rgbColor2.R * 299) + (rgbColor2.G * 587) + (rgbColor2.B * 114)) / 1000;
                var colorBrightnessDifference = colorBrightness1 - colorBrightness2;

                return NumberValue.Create(colorBrightnessDifference);
            }
        }

        public static ValueTask<FluidValue> GetColorContrast(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var rgbColor1 = GetRgbColor(input.ToStringValue());
            var rgbColor2 = GetRgbColor(arguments.At(0).ToStringValue());
            if (rgbColor1.Equals(RgbColor.Empty) || rgbColor2.Equals(RgbColor.Empty))
            {
                return NilValue.Empty;
            }
            else
            {
                var luminance1 = GetRelativeLuminance(rgbColor2);
                var luminance2 = GetRelativeLuminance(rgbColor1);
                var colorContrast = Math.Round((luminance1 + 0.05) / (luminance2 + 0.05), 1);

                return NumberValue.Create((decimal) colorContrast);
            }
        }

        // https://www.w3.org/TR/WCAG20/#relativeluminancedef
        private static double GetRelativeLuminance(RgbColor color)
        {
            var RsRGB = color.R / 255.0;
            var GsRGB = color.G / 255.0;
            var BsRGB = color.B / 255.0;
            var R = (RsRGB <= 0.03928) ? RsRGB / 12.92 : Math.Pow((RsRGB + 0.055) / 1.055, 2.4);
            var G = (GsRGB <= 0.03928) ? GsRGB / 12.92 : Math.Pow((GsRGB + 0.055) / 1.055, 2.4);
            var B = (BsRGB <= 0.03928) ? BsRGB / 12.92 : Math.Pow((BsRGB + 0.055) / 1.055, 2.4);
            var L = 0.2126 * R + 0.7152 * G + 0.0722 * B;

            return L;
        }

        private static RgbColor GetRgbColor(string value)
        {
            var rgbColor = RgbColor.Empty;
            if (HexColor.TryParse(value, out HexColor hexColor))
            {
                rgbColor = (RgbColor)hexColor;
            }
            else if (RgbColor.TryParse(value, out rgbColor))
            {

            }
            else if (HslColor.TryParse(value, out HslColor hslColor))
            {
                rgbColor = (RgbColor)hslColor;
            }

            return rgbColor;
        }

        private readonly struct HexColor
        {
            public static readonly HexColor Empty = default;

            public HexColor(string red, string green, string blue)
            {
                if (!IsHexadecimal(red))
                {
                    ExceptionHelper.ThrowArgumentNullException(nameof(red), "The red value is not hexadecimal");
                }

                if (!IsHexadecimal(green))
                {
                    ExceptionHelper.ThrowArgumentNullException(nameof(green), "The green value is not hexadecimal");
                }

                if (!IsHexadecimal(blue))
                {
                    ExceptionHelper.ThrowArgumentNullException(nameof(blue), "The blue value is not hexadecimal");
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

            public override string ToString() => $"#{R}{G}{B}".ToLower();

            public static explicit operator HexColor(HslColor hslColor) => (HexColor)(RgbColor)hslColor;

            public static explicit operator HexColor(RgbColor rgbColor)
                => new HexColor(
                    rgbColor.R.ToString("X2", null),
                    rgbColor.G.ToString("X2", null),
                    rgbColor.B.ToString("X2", null));

            private static bool IsHexadecimal(string value) => value.All(c => "0123456789abcdefABCDEF".Contains(c));
        }

        private readonly struct RgbColor : IEquatable<RgbColor>
        {
            private const double DefaultTransperency = 1.0;

            private static readonly char[] _colorSeparators = new[] { '(', ',', ' ', ')' };

            public static readonly RgbColor Empty = default;

            public RgbColor(Color color) : this(color.R, color.G, color.B)
            {

            }

            public RgbColor(int red, int green, int blue, double alpha = DefaultTransperency)
            {
                if ((uint) red > 255)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(red), "The red value must in rage [0-255]");
                }

                if ((uint) green > 255)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(green), "The green value must in rage [0-255]");
                }

                if ((uint) blue > 255)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(blue), "The blue value must in rage [0-255]");
                }

                if (alpha < 0.0 || alpha > 1.0)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(alpha), "The alpha value must in rage [0-1]");
                }

                R = red;
                G = green;
                B = blue;
                A = alpha;
            }

            public double A { get; }

            public int R { get; }

            public int G { get; }

            public int B { get; }

            public static bool TryParse(string value, out RgbColor color)
            {
                if ((value.StartsWith("rgb(") || value.StartsWith("rgba(")) && value.EndsWith(")"))
                {
                    var rgbColor = value.Split(_colorSeparators, StringSplitOptions.RemoveEmptyEntries);

                    if (rgbColor.Length == 4 &&
                        Int32.TryParse(rgbColor[1], NumberStyles.Float, CultureInfo.InvariantCulture, out int red) &&
                        Int32.TryParse(rgbColor[2], NumberStyles.Float, CultureInfo.InvariantCulture, out int green) &&
                        Int32.TryParse(rgbColor[3], NumberStyles.Float, CultureInfo.InvariantCulture, out int blue))
                    {
                        color = new RgbColor(red, green, blue);

                        return true;
                    }

                    if (rgbColor.Length == 5 &&
                        Int32.TryParse(rgbColor[1], NumberStyles.Float, CultureInfo.InvariantCulture, out red) &&
                        Int32.TryParse(rgbColor[2], NumberStyles.Float, CultureInfo.InvariantCulture, out green) &&
                        Int32.TryParse(rgbColor[3], NumberStyles.Float, CultureInfo.InvariantCulture, out blue) &&
                        Single.TryParse(rgbColor[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha))
                    {
                        color = new RgbColor(red, green, blue, alpha);

                        return true;
                    }
                }

                color = RgbColor.Empty;

                return false;
            }

            private static double QqhToRgb(double q1, double q2, double hue)
            {
                if (hue > 360.0)
                {
                    hue -= 360.0;
                }
                else if (hue < 0)
                {
                    hue += 360.0;
                }

                if (hue < 60.0)
                {
                    return q1 + (q2 - q1) * hue / 60.0;
                }

                if (hue < 180.0)
                {
                    return q2;
                }

                if (hue < 240.0)
                {
                    return q1 + (q2 - q1) * (240.0 - hue) / 60.0;
                }

                return q1;
            }

            public static implicit operator Color(RgbColor rgbColor)
                => Color.FromArgb(rgbColor.R, rgbColor.G, rgbColor.B);

            public static explicit operator RgbColor(Color color) => new RgbColor(color);

            public static explicit operator RgbColor(HexColor hexColor)
            {
                if (hexColor.R.Length == 1)
                {
                    var red = Convert.ToInt32(hexColor.R + hexColor.R, 16);
                    var green = Convert.ToInt32(hexColor.G + hexColor.G, 16);
                    var blue = Convert.ToInt32(hexColor.B + hexColor.B, 16);

                    return new RgbColor(red, green, blue);
                }
                else
                {
                    var red = Convert.ToInt32(hexColor.R, 16);
                    var green = Convert.ToInt32(hexColor.G, 16);
                    var blue = Convert.ToInt32(hexColor.B, 16);

                    return new RgbColor(red, green, blue);
                }
            }

            public static explicit operator RgbColor(HslColor hslColor)
            {
                // http://csharphelper.com/blog/2016/08/convert-between-rgb-and-hls-color-models-in-c/
                double p2;
                if (hslColor.L <= 0.5)
                {
                    p2 = hslColor.L * (1 + hslColor.S);
                }
                else
                {
                    p2 = hslColor.L + hslColor.S - hslColor.L * hslColor.S;
                }

                var p1 = 2.0 * hslColor.L - p2;
                double r, g, b;
                if (hslColor.S == 0.0)
                {
                    r = hslColor.L;
                    g = hslColor.L;
                    b = hslColor.L;
                }
                else
                {
                    r = QqhToRgb(p1, p2, hslColor.H + 120.0);
                    g = QqhToRgb(p1, p2, hslColor.H);
                    b = QqhToRgb(p1, p2, hslColor.H - 120.0);
                }

                return new RgbColor(
                    (int)Math.Round(r * 255.0),
                    (int)Math.Round(g * 255.0),
                    (int)Math.Round(b * 255.0),
                    hslColor.A
                    );
            }

            public override string ToString() => A == DefaultTransperency
                ? FormattableString.Invariant($"rgb({R}, {G}, {B})")
                : FormattableString.Invariant($"rgba({R}, {G}, {B}, {Math.Round(A, 1)})");

            public bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
        }

        private readonly struct HslColor
        {
            private const double DefaultTransparency = 1.0;

            private static readonly char[] _colorSeparators = new[] { '(', ',', ' ', ')' };

            public static readonly HslColor Empty = default;

            public HslColor(double hue, double saturation, double lightness, double alpha = DefaultTransparency)
            {
                if (hue < 0 || hue > 360)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(hue), "The hue value must in rage [0-360]");
                }

                if (saturation < 0.0 || saturation > 1.0)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(saturation), "The saturation value must in rage [0-1]");
                }

                if (lightness < 0.0 || lightness > 1.0)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(lightness), "The lightness value must in rage [0-1]");
                }

                if (alpha < 0.0 || alpha > 1.0)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(alpha), "The alpha value must in rage [0-1]");
                }

                H = hue;
                S = saturation;
                L = lightness;
                A = alpha;
            }

            public double H { get; }

            public double S { get; }

            public double L { get; }

            public double A { get; }

            public static bool TryParse(string value, out HslColor color)
            {
                if ((value.StartsWith("hsl(") || value.StartsWith("hsla(")) && value.EndsWith(")"))
                {
                    var hslColor = value.Split(_colorSeparators, StringSplitOptions.RemoveEmptyEntries);

                    if (hslColor.Length == 4 && hslColor[2].EndsWith("%") && hslColor[3].EndsWith("%") &&
                        Double.TryParse(hslColor[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double hue) &&
                        Double.TryParse(hslColor[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double saturation) &&
                        Double.TryParse(hslColor[3].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lightness))
                    {
                        color = new HslColor(hue, saturation / 100.0, lightness / 100.0);

                        return true;
                    }

                    if (hslColor.Length == 5 && hslColor[2].EndsWith("%") && hslColor[3].EndsWith("%") &&
                        Double.TryParse(hslColor[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hue) &&
                        Double.TryParse(hslColor[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out saturation) &&
                        Double.TryParse(hslColor[3].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out lightness) &&
                        Double.TryParse(hslColor[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double alpha))
                    {
                        color = new HslColor(hue, saturation / 100.0, lightness / 100.0, alpha);

                        return true;
                    }
                }

                color = HslColor.Empty;

                return false;
            }

            public static explicit operator HslColor(HexColor hexColor) => (HslColor)(RgbColor)hexColor;

            public static explicit operator HslColor(RgbColor rgbColor)
            {
                // http://csharphelper.com/blog/2016/08/convert-between-rgb-and-hls-color-models-in-c/
                double h;
                double s;
                var r = rgbColor.R / 255.0;
                var g = rgbColor.G / 255.0;
                var b = rgbColor.B / 255.0;
                var max = Math.Max(Math.Max(r, g), b);
                var min = Math.Min(Math.Min(r, g), b);
                var diff = max - min;
                var l = (max + min) / 2.0;

                if (Math.Abs(diff) < 0.00001)
                {
                    s = 0.0;
                    h = 0.0;
                }
                else
                {
                    if (l <= 0.5)
                    {
                        s = diff / (max + min);
                    }
                    else
                    {
                        s = diff / (2 - max - min);
                    }

                    var rDist = (max - r) / diff;
                    var gDist = (max - g) / diff;
                    var bDist = (max - b) / diff;

                    if (r == max)
                    {
                        h = bDist - gDist;
                    }
                    else if (g == max)
                    {
                        h = 2 + rDist - bDist;
                    }
                    else
                    {
                        h = 4 + gDist - rDist;
                    }

                    h *= 60;

                    if (h < 0)
                    {
                        h += 360;
                    }
                }

                return new HslColor(Convert.ToInt32(h), Math.Round(s, 2), Math.Round(l, 2), rgbColor.A);
            }

            public override string ToString() => A == DefaultTransparency
                ? FormattableString.Invariant($"hsl({H}, {S * 100.0}%, {L * 100.0}%)")
                : FormattableString.Invariant($"hsla({H}, {S * 100.0}%, {L * 100.0}%, {Math.Round(A, 1)})");
        }
    }
}
