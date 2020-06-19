namespace Fluid.Tests.Domain.WithInterfaces
{
    public interface IDog : IAnimal
    {
        string Name { get; set; }
    }
}
