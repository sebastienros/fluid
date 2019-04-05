using Fluid.Tags;

namespace Fluid.MvcViewEngine
{
    public class FluidTagOptions
    {
        public void AddTag(string name, ITag tag)
        {
            FluidViewTemplate.Factory.RegisterTag(name, tag);
        }

        public void AddTag<T>(string name) where T : ITag, new()
        {
            AddTag(name, new T());
        }
    }
}
