using Fluid.MvcViewEngine.Tags;

namespace Fluid.MvcViewEngine
{
    public class FluidViewTemplate : BaseFluidTemplate<FluidViewTemplate>
    {
        static FluidViewTemplate()
        {
            Factory.RegisterTag<LayoutTag>("layout");
            Factory.RegisterBlock<RegisterSectionBlock>("section");
            Factory.RegisterTag<RenderBodyTag>("renderbody");
            Factory.RegisterBlock<RenderSectionTag>("rendersection");
            Factory.RegisterTag<IncludeTag>("include");
        }
    }
}
