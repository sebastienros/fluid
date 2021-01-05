using System;

namespace Fluid
{
    public interface IFluidParser
    {
        IFluidTemplate Parse(string template);
    }

    public static class IFluidParserExtensions
    {
        public static bool TryParse(this IFluidParser parser, string template, out IFluidTemplate result, out string error)
        {
            try
            {
                error = null;
                result = parser.Parse(template);
                return true;
            }
            catch (ParseException e)
            {
                error = e.Message;
                result = null;
                return false;
            }
            catch (Exception e)
            {
                error = e.Message;
                result = null;
                return false;
            }
        }

        public static bool TryParse(this IFluidParser parser, string template, out IFluidTemplate result)
        {
            return parser.TryParse(template, out result, out _);
        }
    }
}