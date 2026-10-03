namespace Fluid
{
    public abstract class MemberAccessStrategy
    {
        public abstract IMemberAccessor GetAccessor(Type type, string name);

        public abstract void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors);

        internal virtual void RegisterGeneratedAccessor(Type type)
        {
        }

        internal virtual IMemberAccessor GetModelAccessor(Type type, string name)
            => MemberAccessStrategyExtensions.GetNamedAccessor(type, name, MemberNameStrategy);

        public MemberNameStrategy MemberNameStrategy { get; set; } = MemberNameStrategies.Default;

        /// <summary>
        /// Gets or sets whether the member casing is ignored or not.
        /// </summary>
        public bool IgnoreCasing { get; set; }
    }
}