using System;

namespace Fluid
{
    public interface IMemberAccessStrategy
    {
        IMemberAccessor GetAccessor(object obj, string name);

        void Register(Type type, string name, IMemberAccessor getter);
    }
}