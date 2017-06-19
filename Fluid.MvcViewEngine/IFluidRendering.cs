using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FluidMvcViewEngine
{
    public interface IFluidRendering
    {
        Task<string> Render(string path, object model, ViewDataDictionary viewData, ModelStateDictionary modelState);
    }
}