namespace Fluid
{
    public abstract class MemberAccessStrategy
    {
        public abstract IMemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer);

        public abstract void Register(Type type, string name, IMemberAccessor accessor);

        /// <summary>
        /// Gets a token identifying the current set of accessors this strategy would return, or
        /// <c>null</c> to disable caching. Call sites may remember the <see cref="IMemberAccessor"/>
        /// resolved for a type and name, and re-resolve it only once this token changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Defaults to <c>null</c>, so a custom strategy is consulted on every member access and never
        /// serves a stale accessor.
        /// </para>
        /// <para>
        /// An implementation that opts in must return a token that is not equal (by reference) to any
        /// previous token whenever the accessor it would return for <em>any</em> type and name changes,
        /// including changes originating from a source other than <see cref="Register"/>. Returning a
        /// token that fails to change leaves call sites bound to a superseded accessor indefinitely,
        /// with no error. The simplest correct implementation is to return the immutable map the
        /// accessors are resolved from, replacing it on every mutation.
        /// </para>
        /// <para>
        /// The token must be published such that it is visible to threads that render templates
        /// concurrently; assigning a freshly built map to a field satisfies this.
        /// </para>
        /// </remarks>
        protected internal virtual object AccessorCacheToken => null;
    }
}
