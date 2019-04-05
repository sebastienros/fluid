using Fluid.Tags;

namespace Fluid.MvcViewEngine
{
    public class FluidBlockOptions
    {
        public void AddBlock(string name, ITag tag)
        {
            FluidViewTemplate.Factory.RegisterBlock(name, tag);
        }

        public void AddBlock<T>(string name) where T : ITag, new()
        {
            AddBlock(name, new T());
        }
    }
}
