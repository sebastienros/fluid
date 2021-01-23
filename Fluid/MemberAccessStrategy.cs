using System;

namespace Fluid
{
    public abstract class MemberAccessStrategy
    {
        public abstract IMemberAccessor GetAccessor(Type type, string name);

        public abstract void Register(Type type, string name, IMemberAccessor getter);

        public MemberNameStrategy MemberNameStrategy { get; set; } = MemberNameStrategies.Default;

        /// <summary>
        /// Gets or sets whether the member casing is ignored or not.
        /// </summary>
        /// <remarks>This property should be set before calling <see cref="Register(Type, string, IMemberAccessor)"/>.</remarks>
        public bool IgnoreCasing { get; set; }
    }
}