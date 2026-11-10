using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// The <c>self</c> object, like Liquid's <c>SelfDrop</c>: its members are the variables, looked up like a variable
/// of the template is, from the current scope to the values of the model. <c>self.title</c> and
/// <c>self['title']</c> are <c>title</c>. A variable named <c>self</c> takes precedence over it.
/// </summary>
/// <remarks>
/// Like in Liquid, the object belongs to the template or <c>render</c> partial that reads it: in a partial, its
/// members are the arguments of the partial and the model, not the variables of the template that renders it.
/// An object passed to a partial keeps reading the variables of the template it comes from.
/// </remarks>
public sealed class SelfValue : FluidValue
{
    /// <summary>
    /// The name of the variable that resolves to the <c>self</c> object when no variable has it.
    /// </summary>
    internal const string Name = "self";

    // How Liquid renders its SelfDrop, the name of the class like any Liquid drop.
    private const string DisplayName = "Liquid::SelfDrop";

    /// <summary>
    /// The object of the template that reads it, while an expression like <c>self.title</c> reads one of its members.
    /// Unlike <see cref="Create(TemplateContext)"/>, it doesn't allocate, and is never the value of an expression.
    /// </summary>
    internal static readonly SelfValue Current = new(null);

    // The first scope of the template or partial the object belongs to (see TemplateContext.GetFrame), or null for Current.
    private readonly Scope _frame;

    private SelfValue(Scope frame)
    {
        _frame = frame;
    }

    /// <summary>
    /// Creates the <c>self</c> object of the template or partial that the context is rendering.
    /// </summary>
    public static SelfValue Create(TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new SelfValue(context.GetFrame(context.LocalScope));
    }

    public override FluidValues Type => FluidValues.Object;

    public override bool Equals(FluidValue other)
    {
        // Like Liquid, two self objects are equal when they belong to the same template or partial.
        return other is SelfValue self && ReferenceEquals(self._frame, _frame);
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        var scope = GetScope(context);

        // Like a variable of the template: the scopes, which counters can shadow, then the model.
        var value = scope is null
            ? context.ShadowWithCounter(name, context.LocalScope.GetValue(name))
            : context.ShadowWithCounter(scope, name, scope.GetValue(name));

        if (!value.IsNil())
        {
            return value;
        }

        // Like Liquid, where the self member of self is a variable lookup too: when no variable is named self, it
        // is the self object. A variable assigned nil takes precedence.
        var isSelf = name == Name && value == UndefinedValue.Instance;

        if (!context.Model.IsNil())
        {
            var task = context.Model.GetValueAsync(name, context);

            if (!isSelf)
            {
                return task;
            }

            if (task.IsCompletedSuccessfully)
            {
                return task.Result.IsNil() ? AsValue(context) : task.Result;
            }

            return Awaited(task, context);
        }

        return isSelf ? AsValue(context) : NilValue.Instance;

        async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> task, TemplateContext context)
        {
            var member = await task;
            return member.IsNil() ? AsValue(context) : member;
        }
    }

    // The shared instance is never the value of an expression: it only reads the members of the current template.
    private SelfValue AsValue(TemplateContext context) => _frame is null ? Create(context) : this;

    /// <summary>
    /// Gets the scope to read the variables from, or null when they are the ones of the current scope.
    /// </summary>
    private Scope GetScope(TemplateContext context)
    {
        if (_frame is null)
        {
            return null;
        }

        var localScope = context.LocalScope;

        if (ReferenceEquals(context.GetFrame(localScope), _frame))
        {
            return null;
        }

        // The object comes from a template that renders the current partial, maybe through other partials: its
        // variables are those of the scope it was in when it rendered the partial.
        for (var scope = localScope.Previous; scope != null; scope = scope.Previous)
        {
            if (ReferenceEquals(context.GetFrame(scope), _frame))
            {
                return scope;
            }
        }

        return _frame;
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        // Like Liquid, where variables are only named by strings: self[1] is nil, even with a variable named "1".
        if (index.Type != FluidValues.String)
        {
            return NilValue.Instance;
        }

        return GetValueAsync(index.ToStringValue(context), context);
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return DisplayName;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return DisplayName;
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(encoder, DisplayName);
        return default;
    }

    public override bool Equals(object obj)
    {
        return obj is SelfValue self && Equals(self);
    }

    public override int GetHashCode()
    {
        return _frame is null ? 0 : RuntimeHelpers.GetHashCode(_frame);
    }
}
