using Fluid.Values;
using System.Drawing;
using System.Globalization;

namespace Fluid.Filters;

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

            return StringValue.Create(rgbColor.ToString());
        }
        else if (HslColor.TryParse(value, out HslColor hslColor))
        {
            var rgbColor = (RgbColor)hslColor;

            return StringValue.Create(rgbColor.ToString());
        }
        else
        {
            return EmptyValue.Instance;
        }
    }

    public static ValueTask<FluidValue> ToHex(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var value = input.ToStringValue();
        if (RgbColor.TryParse(value, out RgbColor rgbColor))
        {
            var hexColor = (HexColor)rgbColor;

            return StringValue.Create(hexColor.ToString());
        }
        else if (HslColor.TryParse(value, out HslColor hslColor))
        {
            var hexColor = (HexColor)hslColor;

            return StringValue.Create(hexColor.ToString());
        }
        else
        {
            return EmptyValue.Instance;
        }
    }

    public static ValueTask<FluidValue> ToHsl(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var value = input.ToStringValue();
        if (HexColor.TryParse(value, out HexColor hexColor))
        {
            var hslColor = (HslColor)hexColor;

            return StringValue.Create(hslColor.ToString());
        }
        else if (RgbColor.TryParse(value, out RgbColor rgbColor))
        {
            var hslColor = (HslColor)rgbColor;

            return StringValue.Create(hslColor.ToString());
        }
        else
        {
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }

        return arguments.At(0).ToStringValue() switch
        {
            "alpha" => StringValue.Create(rgbColor.A.ToString(CultureInfo.InvariantCulture)),
            "red" => StringValue.Create(rgbColor.R.ToString(CultureInfo.InvariantCulture)),
            "green" => StringValue.Create(rgbColor.G.ToString(CultureInfo.InvariantCulture)),
            "blue" => StringValue.Create(rgbColor.B.ToString(CultureInfo.InvariantCulture)),
            "hue" => StringValue.Create(hslColor.H.ToString(CultureInfo.InvariantCulture)),
            "saturation" => StringValue.Create(Convert.ToInt32(hslColor.S * 100.0).ToString(CultureInfo.InvariantCulture)),
            "lightness" => StringValue.Create(Convert.ToInt32(hslColor.L * 100.0).ToString(CultureInfo.InvariantCulture)),
            _ => EmptyValue.Instance,
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
            return EmptyValue.Instance;
        }

        var modifiedValue = arguments.At(1).ToNumberValue();
        if (isRgb)
        {
            hslColor = (HslColor)rgbColor;

            return arguments.At(0).ToStringValue() switch
            {
                "alpha" => StringValue.Create(new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue).ToString()),
                "red" => StringValue.Create(new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A).ToString()),
                "green" => StringValue.Create(new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A).ToString()),
                "blue" => StringValue.Create(new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A).ToString()),
                "hue" => StringValue.Create(((RgbColor)new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A)).ToString()),
                "saturation" => StringValue.Create(((RgbColor)new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A)).ToString()),
                "lightness" => StringValue.Create(((RgbColor)new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A)).ToString()),
                _ => EmptyValue.Instance,
            };
        }
        else if (isHsl)
        {
            rgbColor = (RgbColor)hslColor;

            return arguments.At(0).ToStringValue() switch
            {
                "alpha" => StringValue.Create(((HslColor)new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue)).ToString()),
                "red" => StringValue.Create(((HslColor)new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A)).ToString()),
                "green" => StringValue.Create(((HslColor)new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A)).ToString()),
                "blue" => StringValue.Create(((HslColor)new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A)).ToString()),
                "hue" => StringValue.Create(new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A).ToString()),
                "saturation" => StringValue.Create(new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A).ToString()),
                "lightness" => StringValue.Create(new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A).ToString()),
                _ => EmptyValue.Instance,
            };
        }
        else if (isHex)
        {
            rgbColor = (RgbColor)hexColor;
            hslColor = (HslColor)hexColor;

            return arguments.At(0).ToStringValue() switch
            {
                "alpha" => StringValue.Create(new RgbColor(rgbColor.R, rgbColor.G, rgbColor.B, (double)modifiedValue).ToString()),
                "red" => StringValue.Create(((HexColor)new RgbColor((int)modifiedValue, rgbColor.G, rgbColor.B, rgbColor.A)).ToString()),
                "green" => StringValue.Create(((HexColor)new RgbColor(rgbColor.R, (int)modifiedValue, rgbColor.B, rgbColor.A)).ToString()),
                "blue" => StringValue.Create(((HexColor)new RgbColor(rgbColor.R, rgbColor.G, (int)modifiedValue, rgbColor.A)).ToString()),
                "hue" => StringValue.Create(((HexColor)new HslColor((int)modifiedValue, hslColor.S, hslColor.L, hslColor.A)).ToString()),
                "saturation" => StringValue.Create(((HexColor)new HslColor(hslColor.H, (double)modifiedValue / 100.0, hslColor.L, hslColor.A)).ToString()),
                "lightness" => StringValue.Create(((HexColor)new HslColor(hslColor.H, hslColor.S, (double)modifiedValue / 100.0, hslColor.A)).ToString()),
                _ => EmptyValue.Instance,
            };
        }
        else
        {
            // The code is unreachable
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }

        var brightness = Convert.ToDouble(rgbColor.R * 299 + rgbColor.G * 587 + rgbColor.B * 114) / 1000.0;

        return NumberValue.Create((decimal)Math.Round(brightness, 2));
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
            return EmptyValue.Instance;
        }

        if (isHex)
        {
            hslColor = (HslColor)hexColor;

            var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((HexColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
        }
        else if (isHsl)
        {
            var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A).ToString());
        }
        else if (isRgb)
        {
            hslColor = (HslColor)rgbColor;

            var saturation = (hslColor.S * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((RgbColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
        }
        else
        {
            // The code is unreachable
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }

        if (isHex)
        {
            hslColor = (HslColor)hexColor;

            var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((HexColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
        }
        else if (isHsl)
        {
            var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A).ToString());
        }
        else if (isRgb)
        {
            hslColor = (HslColor)rgbColor;

            var saturation = (hslColor.S * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((RgbColor)new HslColor(hslColor.H, saturation, hslColor.L, hslColor.A)).ToString());
        }
        else
        {
            // The code is unreachable
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }

        if (isHex)
        {
            hslColor = (HslColor)hexColor;

            var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((HexColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
        }
        else if (isHsl)
        {
            var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A).ToString());
        }
        else if (isRgb)
        {
            hslColor = (HslColor)rgbColor;

            var lightness = (hslColor.L * 100.0 + Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((RgbColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
        }
        else
        {
            // The code is unreachable
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }

        if (isHex)
        {
            hslColor = (HslColor)hexColor;

            var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((HexColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
        }
        else if (isHsl)
        {
            var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A).ToString());
        }
        else if (isRgb)
        {
            hslColor = (HslColor)rgbColor;

            var lightness = (hslColor.L * 100.0 - Convert.ToDouble(arguments.At(0).ToNumberValue())) / 100.0;

            return StringValue.Create(((RgbColor)new HslColor(hslColor.H, hslColor.S, lightness, hslColor.A)).ToString());
        }
        else
        {
            // The code is unreachable
            return EmptyValue.Instance;
        }
    }

    public static ValueTask<FluidValue> GetColorDifference(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var rgbColor1 = GetRgbColor(input.ToStringValue());
        var rgbColor2 = GetRgbColor(arguments.At(0).ToStringValue());
        if (rgbColor1.Equals(RgbColor.Empty) || rgbColor2.Equals(RgbColor.Empty))
        {
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
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
            return EmptyValue.Instance;
        }
        else
        {
            var luminance1 = GetRelativeLuminance(rgbColor2);
            var luminance2 = GetRelativeLuminance(rgbColor1);
            var colorContrast = Math.Round((luminance1 + 0.05) / (luminance2 + 0.05), 1);

            return NumberValue.Create((decimal)colorContrast);
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

    private static bool TryReadComponent(scoped ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> component)
    {
        var start = 0;
        while (start < remaining.Length && IsColorSeparator(remaining[start]))
        {
            start++;
        }

        var end = start;
        while (end < remaining.Length && !IsColorSeparator(remaining[end]))
        {
            end++;
        }

        component = remaining.Slice(start, end - start);
        remaining = remaining.Slice(end);
        return !component.IsEmpty;
    }

    private static bool IsColorSeparator(char c) => c is '(' or ')' or ',' or ' ' or '\t' or '\r' or '\n' or '\f';

    private static bool TryReadComponents(ReadOnlySpan<char> remaining, out ReadOnlySpan<char> first,
        out ReadOnlySpan<char> second, out ReadOnlySpan<char> third, out ReadOnlySpan<char> alpha)
    {
        second = third = alpha = default;
        if (!TryReadComponent(ref remaining, out first) ||
            !TryReadComponent(ref remaining, out second) ||
            !TryReadComponent(ref remaining, out third))
        {
            return false;
        }

        TryReadComponent(ref remaining, out alpha);
        return !TryReadComponent(ref remaining, out _);
    }

    private static bool TryParseNumber(ReadOnlySpan<char> value, out double number)
    {
        number = 0;
        // CSS numbers must end in a digit, unlike .NET's floating-point syntax (for example, "1.").
        if (value.IsEmpty || value[^1] < '0' || value[^1] > '9')
        {
            return false;
        }

#if NETSTANDARD2_0
        return Double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
#else
        return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
#endif
            !Double.IsNaN(number) && !Double.IsInfinity(number);
    }

    private static double Clamp(double value, double maximum) => Math.Min(Math.Max(value, 0), maximum);

    private static bool TryParsePercentage(ReadOnlySpan<char> value, out double percentage)
    {
        percentage = 0;
        if (value.IsEmpty || value[^1] != '%' || !TryParseNumber(value.Slice(0, value.Length - 1), out var number))
        {
            return false;
        }

        percentage = Clamp(number, 100) / 100;
        return true;
    }

    private static bool TryParseAlpha(ReadOnlySpan<char> value, out double alpha)
    {
        alpha = 1;
        if (value.IsEmpty)
        {
            return true;
        }

        if (value[^1] == '%')
        {
            return TryParsePercentage(value, out alpha);
        }

        if (!TryParseNumber(value, out var number))
        {
            return false;
        }

        alpha = Clamp(number, 1);
        return true;
    }

    private static bool TryParseRgbComponent(ReadOnlySpan<char> value, out int channel)
    {
        channel = 0;
        double number;
        if (value[^1] == '%')
        {
            if (!TryParsePercentage(value, out number))
            {
                return false;
            }

            number *= 255;
        }
        else
        {
            if (!TryParseNumber(value, out number))
            {
                return false;
            }

            number = Clamp(number, 255);
        }

        channel = (int)Math.Round(number, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryParseHue(ReadOnlySpan<char> value, out double hue)
    {
        double revolution = 360;
        var suffixLength = 0;
        if (value.EndsWith("grad".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            revolution = 400;
            suffixLength = 4;
        }
        else if (value.EndsWith("rad".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            revolution = 2 * Math.PI;
            suffixLength = 3;
        }
        else if (value.EndsWith("turn".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            revolution = 1;
            suffixLength = 4;
        }
        else if (value.EndsWith("deg".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            suffixLength = 3;
        }

        if (!TryParseNumber(value.Slice(0, value.Length - suffixLength), out hue))
        {
            return false;
        }

        // Reduce before converting units so large, finite angles cannot overflow.
        hue %= revolution;
        if (suffixLength != 0 && revolution != 360)
        {
            hue = hue / revolution * 360;
        }

        if (hue < 0)
        {
            hue += 360;
        }

        if (hue >= 360)
        {
            hue = 0;
        }

        return true;
    }

    private readonly struct HexColor
    {
        public static readonly HexColor Empty = default;

        public HexColor(int red, int green, int blue)
        {
            R = red;
            G = green;
            B = blue;
        }

        public int R { get; }

        public int G { get; }

        public int B { get; }

        public static bool TryParse(string value, out HexColor color)
        {
            color = HexColor.Empty;

            if (value.Length is not (4 or 7) || value[0] != '#')
            {
                return false;
            }

            var digits = value.AsSpan(1);
            var number = 0;
            foreach (var c in digits)
            {
                var digit = HexDigit(c);
                if (digit < 0)
                {
                    return false;
                }

                number = (number << 4) | digit;
            }

            color = digits.Length == 3
                ? new HexColor((number >> 8) * 17, ((number >> 4) & 15) * 17, (number & 15) * 17)
                : new HexColor(number >> 16, (number >> 8) & 255, number & 255);
            return true;
        }

        public override string ToString() => FormattableString.Invariant($"#{R:x2}{G:x2}{B:x2}");

        public static explicit operator HexColor(HslColor hslColor) => (HexColor)(RgbColor)hslColor;

        public static explicit operator HexColor(RgbColor rgbColor)
            => new HexColor(rgbColor.R, rgbColor.G, rgbColor.B);

        private static int HexDigit(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };
    }

#pragma warning disable CA1067 // should override Equals because it implements IEquatable<T>
    private readonly struct RgbColor : IEquatable<RgbColor>
#pragma warning restore CA1067
    {
        private const double DefaultTransperency = 1.0;

        public static readonly RgbColor Empty = default;

        public RgbColor(Color color) : this(color.R, color.G, color.B)
        {

        }

        public RgbColor(int red, int green, int blue, double alpha = DefaultTransperency)
        {
            if ((uint)red > 255)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(red), "The red value must in rage [0-255]");
            }

            if ((uint)green > 255)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(green), "The green value must in rage [0-255]");
            }

            if ((uint)blue > 255)
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
            if ((value.StartsWith("rgb(", StringComparison.Ordinal) || value.StartsWith("rgba(", StringComparison.Ordinal)) && value.EndsWith(')'))
            {
                var start = value[3] == '(' ? 4 : 5;
                if (TryReadComponents(value.AsSpan(start, value.Length - start - 1), out var redValue,
                        out var greenValue, out var blueValue, out var alphaValue) &&
                    (redValue[^1] == '%') == (greenValue[^1] == '%') &&
                    (redValue[^1] == '%') == (blueValue[^1] == '%') &&
                    TryParseRgbComponent(redValue, out var red) &&
                    TryParseRgbComponent(greenValue, out var green) &&
                    TryParseRgbComponent(blueValue, out var blue) &&
                    TryParseAlpha(alphaValue, out var alpha))
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

        public static explicit operator RgbColor(HexColor hexColor) => new RgbColor(hexColor.R, hexColor.G, hexColor.B);

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
            if ((value.StartsWith("hsl(", StringComparison.Ordinal) || value.StartsWith("hsla(", StringComparison.Ordinal)) && value.EndsWith(')'))
            {
                var start = value[3] == '(' ? 4 : 5;
                if (TryReadComponents(value.AsSpan(start, value.Length - start - 1), out var hueValue,
                        out var saturationValue, out var lightnessValue, out var alphaValue) &&
                    TryParseHue(hueValue, out var hue) &&
                    TryParsePercentage(saturationValue, out var saturation) &&
                    TryParsePercentage(lightnessValue, out var lightness) &&
                    TryParseAlpha(alphaValue, out var alpha))
                {
                    color = new HslColor(hue, saturation, lightness, alpha);

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
