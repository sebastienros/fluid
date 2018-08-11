using System;

namespace Fluid
{
    public interface IMemberAccessStrategy
    {
        IMemberAccessor GetAccessor(Type type, string name);

        void Register(Type type, string name, IMemberAccessor getter);
    }
}