using System;
using System.Collections.Generic;

namespace Fluid
{
    public abstract class MemberAccessStrategy
    {
        public abstract IMemberAccessor GetAccessor(Type type, string name);

        public abstract void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors);

        public MemberNameStrategy MemberNameStrategy { get; set; } = MemberNameStrategies.Default;

        /// <summary>
        /// Gets or sets whether the member casing is ignored or not.
        /// </summary>
        public bool IgnoreCasing { get; set; }
    }
}