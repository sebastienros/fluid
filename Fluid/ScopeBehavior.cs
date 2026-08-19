namespace Fluid
{
    /// <summary>
    /// Controls variable lookup and assignment for a scope entered through
    /// <see cref="TemplateContext.EnterScope(ScopeBehavior)"/>.
    /// </summary>
    public enum ScopeBehavior
    {
        /// <summary>
        /// Reads parent values and keeps assignments in the new scope.
        /// </summary>
        Local,

        /// <summary>
        /// Reads parent values, keeps explicitly local values in the new scope, and writes normal
        /// assignments to the nearest non-write-through scope.
        /// </summary>
        WriteThrough,

        /// <summary>
        /// Reads only values from the context root and global values, and keeps assignments local.
        /// </summary>
        Isolated,
    }
}
