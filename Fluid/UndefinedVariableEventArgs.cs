namespace Fluid
{
    /// <summary>
    /// Provides data for undefined variable notifications raised by <see cref="TemplateContext"/>.
    /// </summary>
    public sealed class UndefinedVariableEventArgs
    {
        internal UndefinedVariableEventArgs(TemplateContext context, string path, bool isFirstOccurrence)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            IsFirstOccurrence = isFirstOccurrence;
        }

        /// <summary>
        /// Gets the <see cref="TemplateContext"/> in which the undefined variable was encountered.
        /// </summary>
        public TemplateContext Context { get; }

        /// <summary>
        /// Gets the path that was evaluated and resolved to an undefined value.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets a value indicating whether this is the first time this undefined path was observed during the render.
        /// </summary>
        public bool IsFirstOccurrence { get; }
    }
}
