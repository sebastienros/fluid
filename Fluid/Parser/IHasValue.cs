using System;
using System.Collections.Generic;
using System.Text;

namespace Fluid.Parser
{
    public interface IHasValue
    {
        object Value { get; }
    }

    public interface IHasValue<out T> : IHasValue
    {
        new T Value { get; }
    }
}
