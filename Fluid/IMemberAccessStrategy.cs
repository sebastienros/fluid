using System;

namespace Fluid
{
    public interface IMemberAccessStrategy
    {
        IMemberAccessor GetAccessor(Type type, string name);

        void Register(Type type, string name, IMemberAccessor getter);

        MemberNameStrategy MemberNameStrategy { get; set; }

        /// <summary>
        /// Gets or sets whether the member casing is ignored or not.
        /// </summary>
        /// <remarks>This property should be set before calling <see cref="Register(Type, string, IMemberAccessor)"/>.</remarks>
        bool IgnoreCasing { get; set; }
    }
}