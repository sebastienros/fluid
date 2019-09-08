using System;

namespace Fluid.Guards
{
    public class RequiresArgumentAttribute : Attribute
    {
        /// <summary>
        /// Custom ErrorMessage to display on Guard exception (optional)
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}