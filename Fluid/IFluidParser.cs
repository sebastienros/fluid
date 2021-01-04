using System;

namespace Fluid
{
    public interface IFluidParser
    {
        IFluidTemplate Parse(string template);

        public bool TryParse(string template, out IFluidTemplate result, out string error)
        {
            try
            {
                error = null;
                result = Parse(template);
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
        
        public bool TryParse(string template, out IFluidTemplate result)
        {
            return TryParse(template, out result, out _);
        }
    }
}