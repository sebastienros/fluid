using System.ComponentModel;

namespace Fluid
{
    /// <summary>
    /// Registers source-generated member accessors for the <see cref="TemplateOptions"/> built by a <see cref="TemplateOptionsBuilder"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITemplateOptionsMemberAccessorRegistrar
    {
        /// <summary>
        /// Registers source-generated member accessors on the specified <see cref="TemplateOptionsBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder to register accessors on.</param>
        void RegisterMemberAccessors(TemplateOptionsBuilder builder);
    }
}
