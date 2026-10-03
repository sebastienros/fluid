using System.ComponentModel;

namespace Fluid
{
    /// <summary>
    /// Registers source-generated member accessors for a <see cref="TemplateOptions"/> instance.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITemplateOptionsMemberAccessorRegistrar
    {
        /// <summary>
        /// Registers source-generated member accessors for the specified <see cref="TemplateOptions"/> instance.
        /// </summary>
        /// <param name="options">The options instance to register accessors on.</param>
        void RegisterMemberAccessors(TemplateOptions options);
    }
}
