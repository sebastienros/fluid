namespace Fluid.Tests.Domain.WithInterfaces
{
    public class Dog : Animal, IDog, IPet
    {
        public string Name { get; set; }
    }
}
