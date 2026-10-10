namespace Fluid.Values;

public sealed class ObjectValue : ObjectValueBase
{
    public static ObjectValue Create(object value) => new ObjectValue(value);

    private ObjectValue(object value) : base(value)
    {
    }
}
