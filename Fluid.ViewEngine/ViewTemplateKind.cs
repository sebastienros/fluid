namespace Fluid.ViewEngine;

/// <summary>
/// The role of a template rendered by an <see cref="IFluidViewRenderer"/>.
/// </summary>
public enum ViewTemplateKind
{
    /// <summary>A view, or one of its <c>_ViewStart</c> files.</summary>
    View,

    /// <summary>A layout applied with the <c>layout</c> tag.</summary>
    Layout,

    /// <summary>A partial rendered with the <c>partial</c> tag.</summary>
    Partial,
}
