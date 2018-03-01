namespace Fluid
{
    public interface IMemberAccessor
    {
        object Get(object obj, string name, TemplateContext ctx);
    }
}
