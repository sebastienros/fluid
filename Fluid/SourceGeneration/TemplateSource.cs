namespace Fluid.SourceGeneration
{
    public sealed record TemplateSource(
        string Namespace,
        string ClassName,
        string SourceCode)
    {
        public string FullTypeName => string.IsNullOrEmpty(Namespace) ? ClassName : Namespace + "." + ClassName;
    }
}
