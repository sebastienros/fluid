using System.Threading.Tasks;

namespace Fluid
{
    public interface IMemberAccessor
    {
        object Get(object obj, string name, TemplateContext ctx);
    }

    public interface IAsyncMemberAccessor : IMemberAccessor
    {
        Task<object> GetAsync(object obj, string name, TemplateContext ctx);
    }
}
