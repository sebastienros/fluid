using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Fluid.Ast;
using Fluid.Values;

#nullable enable

namespace Fluid;

public class TemplateContext
{
    protected int _recursion;
    protected int _steps;
    private Scope _localScope;

    // Partials resolved by the render in progress. A partial used in a loop would otherwise ask the
    // file provider for the same path on every iteration, which for a physical provider is a file
    // system call each time. Scoped to the outermost render so that a context reused for another
    // render still picks up a file that changed in between.
    private Dictionary<TemplateLoader.LoadedTemplateKey, TemplateLoader.LoadedTemplate>? _loadedTemplates;
    private int _renderDepth;

    // The output of the outermost render, which flushes it once when it ends.
    private IFluidOutput? _renderOutput;

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateContext"/>.
    /// </summary>
    public TemplateContext() : this(TemplateOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateContext"/>.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="options">The template options.</param>
    public TemplateContext(object model, TemplateOptions options) : this(options)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ReferenceEquals(options, TemplateOptions.Default))
        {
            options.MemberAccessStrategy.RegisterGeneratedAccessor(model.GetType());
        }

        if (model is FluidValue fluidValue)
        {
            Model = fluidValue;
        }
        else
        {
            Model = FluidValue.Create(model, options);
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateContext"/> with the specified <see cref="TemplateOptions"/>.
    /// </summary>
    /// <param name="options">The template options.</param>
    /// <param name="modelNamesComparer">An optional <see cref="StringComparer"/> instance used when comparing model names.</param>
    public TemplateContext(TemplateOptions options, StringComparer? modelNamesComparer = null)
    {
        modelNamesComparer ??= options.ModelNamesComparer;

        Options = options;
        _localScope = new Scope(options.GlobalScope, null, modelNamesComparer, null);
        RootScope = _localScope;
        CultureInfo = options.CultureInfo;
        MoneyOptions = options.MoneyOptions;
        TimeZone = options.TimeZone;
        Captured = options.Captured;
        Assigned = options.Assigned;
        Undefined = options.Undefined;
        Now = options.Now;
        MaxSteps = options.MaxSteps;
        MaxOutputSize = options.MaxOutputSize;
        MaxCollectionSize = options.MaxCollectionSize;
        ModelNamesComparer = modelNamesComparer;
        JsonSerializerOptions = options.JsonSerializerOptions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateContext"/> with a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="modelNamesComparer">An optional <see cref="StringComparer"/> instance used when comparing model names.</param>
    public TemplateContext(object model, StringComparer? modelNamesComparer = null) : this(TemplateOptions.Default, modelNamesComparer)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model is FluidValue fluidValue)
        {
            Model = fluidValue;
        }
        else
        {
            Model = FluidValue.Create(model, TemplateOptions.Default);
        }
    }

    /// <summary>
    /// Gets the <see cref="TemplateOptions"/>.
    /// </summary>
    public TemplateOptions Options { get; protected set; }

    /// <summary>
    /// Gets or sets the maximum number of steps a script can execute. Leave to 0 for unlimited.
    /// </summary>
    public int MaxSteps { get; set; } = TemplateOptions.Default.MaxSteps;

    /// <summary>
    /// Gets or sets the maximum number of characters a template or captured block can render.
    /// Leave to 0 for unlimited.
    /// </summary>
    public int MaxOutputSize { get; set; } = TemplateOptions.Default.MaxOutputSize;

    /// <summary>
    /// Gets or sets the maximum number of items a template operation can materialize.
    /// Leave to 0 for unlimited.
    /// </summary>
    public int MaxCollectionSize { get; set; } = TemplateOptions.Default.MaxCollectionSize;

    /// <summary>
    /// Gets <see cref="StringComparer"/> used when comparing model names.
    /// </summary>
    public StringComparer ModelNamesComparer { get; private set; }

    /// <summary>
    /// Gets or sets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
    /// </summary>
    public CultureInfo CultureInfo { get; set; } = TemplateOptions.Default.CultureInfo;

    /// <summary>
    /// Gets or sets the options used by the money filters. Assigning a different instance allows
    /// a currency to be selected per rendering, for example based on the current request.
    /// </summary>
    public MoneyOptions MoneyOptions { get; set; } = TemplateOptions.Default.MoneyOptions;

    /// <summary>
    /// Gets or sets the value to returned by the "now" keyword.
    /// </summary>
    public Func<DateTimeOffset> Now { get; set; } = TemplateOptions.Default.Now;

    /// <summary>
    /// Gets or sets the local time zone used when parsing or creating dates without specific ones.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TemplateOptions.Default.TimeZone;

    /// <summary>
    /// Gets or sets the <see cref="JsonSerializerOptions"/> used by the <c>json</c> filter.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = TemplateOptions.Default.JsonSerializerOptions;

    /// <summary>
    /// Gets or sets the token used to cancel asynchronous template operations.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Increments the number of statements the current template is processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementSteps()
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (MaxSteps > 0 && ++_steps > MaxSteps)
        {
            ExceptionHelper.ThrowMaximumStepsException(MaxSteps);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureOutputSize(long size)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (size < 0 || (MaxOutputSize > 0 && size > MaxOutputSize))
        {
            ExceptionHelper.ThrowMaximumOutputSizeException(MaxOutputSize);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCollectionSize(long size)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (size < 0 || (MaxCollectionSize > 0 && size > MaxCollectionSize))
        {
            ExceptionHelper.ThrowMaximumCollectionSizeException(MaxCollectionSize);
        }
    }

    /// <summary>
    /// Marks the start of a template render. Must be paired with <see cref="ExitRender"/>.
    /// </summary>
    internal void EnterRender(IFluidOutput output)
    {
        if (_renderDepth++ == 0)
        {
            _renderOutput = output;
            EnterTemplateScope();
        }
    }

    /// <summary>
    /// Makes sure the template's own variables are not written to the root scope.
    /// </summary>
    /// <remarks>
    /// <c>render</c> evaluates partials in a scope whose parent is the root scope, so that they see the
    /// model but not the variables of the template that renders them. A template rendered without an
    /// isolated scope would otherwise assign its variables in the root scope too. The template scope is
    /// kept after the render, so that later renders on this context (e.g., a layout) still see them.
    /// </remarks>
    internal void EnterTemplateScope()
    {
        if (ReferenceEquals(_localScope, RootScope))
        {
            _localScope = new Scope(RootScope, null, ModelNamesComparer, RootScope);
        }
    }

    /// <summary>
    /// Marks the end of a template render, forgetting the resolved partials once the outermost one ends.
    /// </summary>
    internal void ExitRender()
    {
        if (--_renderDepth == 0)
        {
            _loadedTemplates?.Clear();
            _renderOutput = null;

            // Interrupts that reached the end of the template don't apply to a later render.
            _pendingInterrupts = null;
        }
    }

    // Like Liquid, which keeps a stack of interrupts, the break and continue tags still pending under the one a
    // statement returns as its completion. Only an include over an array stacks more than one: one per item.
    private Stack<Completion>? _pendingInterrupts;

    /// <summary>
    /// Gets whether interrupts are pending under the completion being returned. Like Liquid, a block whose rendering
    /// starts while one is pending renders its text and its first tag or output, then stops with the interrupt on
    /// top of the stack as its completion, and a loop consumes one interrupt per iteration.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasPendingInterrupts => _pendingInterrupts is { Count: > 0 };

    /// <summary>
    /// Keeps a <c>break</c> or <c>continue</c> pending while the next item of an <c>include</c> renders.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void PushInterrupt(Completion completion)
    {
        (_pendingInterrupts ??= new Stack<Completion>()).Push(completion);
    }

    /// <summary>
    /// Removes the most recent pending interrupt and returns it. Only valid when <see cref="HasPendingInterrupts"/> is <c>true</c>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Completion PopInterrupt()
    {
        return _pendingInterrupts!.Pop();
    }

    /// <summary>
    /// Gets whether a render in progress writes to this output and will flush it when it ends, so
    /// that a partial rendered into the same output doesn't need to flush it itself.
    /// </summary>
    internal bool IsFlushedByEnclosingRender(IFluidOutput output)
    {
        return _renderDepth > 0 && ReferenceEquals(_renderOutput, output);
    }

    internal bool TryGetLoadedTemplate(TemplateLoader.LoadedTemplateKey key, out TemplateLoader.LoadedTemplate loadedTemplate)
    {
        if (_loadedTemplates is null)
        {
            loadedTemplate = default;
            return false;
        }

        return _loadedTemplates.TryGetValue(key, out loadedTemplate);
    }

    internal void SetLoadedTemplate(TemplateLoader.LoadedTemplateKey key, TemplateLoader.LoadedTemplate loadedTemplate)
    {
        // Outside of a template render there is nothing to tell when the entry should be dropped.
        if (_renderDepth == 0)
        {
            return;
        }

        _loadedTemplates ??= [];
        _loadedTemplates[key] = loadedTemplate;
    }

    /// <summary>
    /// Gets the current scope.
    /// </summary>
    public Scope LocalScope => _localScope;

    /// <summary>
    /// Gets or sets the root scope.
    /// </summary>
    internal Scope RootScope { get; set; }

    /// <summary>
    /// Used to define custom object on this instance to be used in filters and statements
    /// but which are not available from the template.
    /// </summary>
    public Dictionary<string, object> AmbientValues => field ??= [];

    /// <summary>
    /// Gets or sets a model object that is used to resolve properties in a template. This object is used if local and
    /// global scopes are unsuccessful.
    /// </summary>
    public FluidValue Model { get; } = NilValue.Instance;

    /// <summary>
    /// Gets or sets the delegate to execute when a Capture tag has been evaluated.
    /// </summary>
    public TemplateOptions.CapturedDelegate Captured { get; set; }

    /// <summary>
    /// Gets or sets the delegate to execute when an Assign tag has been evaluated.
    /// </summary>
    public TemplateOptions.AssignedDelegate Assigned { get; set; }

    /// <summary>
    /// Gets or sets the delegate to execute when an undefined value is used.
    /// </summary>
    public TemplateOptions.UndefinedDelegate Undefined { get; set; }

    /// <summary>
    /// Enters a scope with the specified lookup and assignment behavior.
    /// </summary>
    public ScopeLease EnterScope(ScopeBehavior behavior = ScopeBehavior.Local)
    {
        return new ScopeLease(this, EnterScopeCore(behavior));
    }

    private Scope EnterScopeCore(ScopeBehavior behavior)
    {
        if (behavior != ScopeBehavior.Local &&
            behavior != ScopeBehavior.WriteThrough &&
            behavior != ScopeBehavior.Isolated)
        {
            throw new ArgumentOutOfRangeException(nameof(behavior));
        }

        if (Options.MaxRecursion > 0 && _recursion >= Options.MaxRecursion)
        {
            ExceptionHelper.ThrowMaximumRecursionException();
        }

        _recursion++;

        var previous = _localScope;
        var parent = behavior == ScopeBehavior.Isolated ? RootScope : previous;
        var assignmentTarget = behavior == ScopeBehavior.WriteThrough
            ? previous.AssignmentScope
            : null;

        return _localScope = new Scope(parent, assignmentTarget, ModelNamesComparer, previous);
    }

    private void ReleaseScopeCore(Scope scope)
    {
        if (!ReferenceEquals(_localScope, scope))
        {
            ExceptionHelper.ThrowInvalidOperationException("Scopes must be released in reverse order");
        }

        _recursion--;
        _localScope = scope.Previous;
    }

    /// <summary>
    /// Restores the previous scope when disposed.
    /// </summary>
    public readonly struct ScopeLease : IDisposable
    {
        private readonly TemplateContext _context;
        private readonly Scope _scope;

        internal ScopeLease(TemplateContext context, Scope scope)
        {
            _context = context;
            _scope = scope;
        }

        /// <summary>
        /// Restores the scope that was active before this lease was created.
        /// </summary>
        public void Dispose()
        {
            _context?.ReleaseScopeCore(_scope);
        }
    }

    // Like Liquid's registers, the state of the tags that a partial rendered with render starts
    // over with, and doesn't share with the template rendering it: the positions of the cycle tags
    // and the last output of ifchanged.
    private Dictionary<string, int>? _cycleRegister;
    internal string? IfChangedRegister;

    // The number of render tags in progress: like Liquid, their partials can't use include.
    private int _renderTagDepth;

    /// <summary>
    /// Gets the position of each cycle tag, by cycle key.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Dictionary<string, int> CycleRegister => _cycleRegister ??= new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether a partial rendered with the <c>render</c> tag is being rendered, which can't use <c>include</c>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsIncludeDisabled => _renderTagDepth > 0;

    /// <summary>
    /// Enters the scope of a partial rendered with the <c>render</c> tag: an <see cref="ScopeBehavior.Isolated"/>
    /// scope in which the <c>cycle</c> and <c>ifchanged</c> tags start over and <c>include</c> is not allowed.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public RenderTagScopeLease EnterRenderTagScope()
    {
        var lease = new RenderTagScopeLease(this, EnterScopeCore(ScopeBehavior.Isolated), _cycleRegister, IfChangedRegister, _pendingInterrupts);

        // Like Liquid, which renders the partial in an isolated context, the interrupts of the caller don't
        // reach the partial and those of the partial don't leave it.
        _cycleRegister = null;
        IfChangedRegister = null;
        _pendingInterrupts = null;
        _renderTagDepth++;

        return lease;
    }

    private void ExitRenderTagScope(Scope scope, Dictionary<string, int>? cycleRegister, string? ifChangedRegister, Stack<Completion>? pendingInterrupts)
    {
        ReleaseScopeCore(scope);

        _cycleRegister = cycleRegister;
        IfChangedRegister = ifChangedRegister;
        _pendingInterrupts = pendingInterrupts;
        _renderTagDepth--;
    }

    /// <summary>
    /// Restores the scope and the tag state that were active before <see cref="EnterRenderTagScope"/> when disposed.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct RenderTagScopeLease : IDisposable
    {
        private readonly TemplateContext _context;
        private readonly Scope _scope;
        private readonly Dictionary<string, int>? _cycleRegister;
        private readonly string? _ifChangedRegister;
        private readonly Stack<Completion>? _pendingInterrupts;

        internal RenderTagScopeLease(TemplateContext context, Scope scope, Dictionary<string, int>? cycleRegister, string? ifChangedRegister, Stack<Completion>? pendingInterrupts)
        {
            _context = context;
            _scope = scope;
            _cycleRegister = cycleRegister;
            _ifChangedRegister = ifChangedRegister;
            _pendingInterrupts = pendingInterrupts;
        }

        /// <summary>
        /// Restores the scope and the tag state that were active before this lease was created.
        /// </summary>
        public void Dispose()
        {
            _context?.ExitRenderTagScope(_scope, _cycleRegister, _ifChangedRegister, _pendingInterrupts);
        }
    }

    /// <summary>
    /// Gets a value from the context.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    public FluidValue GetValue(string name)
    {
        return _localScope.GetValue(name);
    }

    /// <summary>
    /// Sets a value on the context.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <param name="value">The value to set.</param>
    /// <returns></returns>
    public TemplateContext SetValue(string name, FluidValue value)
    {
        if (!_hasCounters && name.Length > 0 && name[0] == '$' && name.StartsWith(IncrementStatement.Prefix, StringComparison.Ordinal))
        {
            _hasCounters = true;
        }

        _localScope.SetValue(name, value);
        return this;
    }

    // Whether an increment or decrement counter was set, so that reading a variable only checks
    // whether a counter shadows it once there is one.
    private bool _hasCounters;

    /// <summary>
    /// Returns the increment or decrement counter named like a variable when the variable isn't one of the
    /// template's (it is undefined, or comes from the values set before rendering or the global values),
    /// and the variable otherwise.
    /// </summary>
    /// <remarks>
    /// Like Liquid, where counters live in the template's environment: they are read before the data
    /// passed to the template, but after the template's own variables, even nil ones.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluidValue ShadowWithCounter(string name, FluidValue value)
    {
        // Inlined at every variable lookup: only a field check until a counter is set.
        return _hasCounters ? ShadowWithCounterSlow(_localScope, name, value) : value;
    }

    /// <summary>
    /// Like <see cref="ShadowWithCounter(string, FluidValue)"/>, for a variable read from a scope that isn't the current one.
    /// </summary>
    internal FluidValue ShadowWithCounter(Scope localScope, string name, FluidValue value)
    {
        return _hasCounters ? ShadowWithCounterSlow(localScope, name, value) : value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private FluidValue ShadowWithCounterSlow(Scope localScope, string name, FluidValue value)
    {
        // A variable of the template, assigned in a scope under the root one, isn't shadowed, even when it is nil.
        for (var scope = localScope; scope != null && !ReferenceEquals(scope, RootScope); scope = scope.Parent)
        {
            if (scope.TryGetOwnValue(name, out _))
            {
                return value;
            }
        }

        var counter = localScope.GetValue(IncrementStatement.Prefix + name);
        return counter.IsNil() ? value : counter;
    }

    /// <summary>
    /// Gets the first scope of the template or <c>render</c> partial that a scope belongs to, the one under the root
    /// scope, like a Liquid context: a partial is rendered in a scope of its own, and the scopes of loops or of
    /// <c>include</c> are under the one of their template.
    /// </summary>
    internal Scope GetFrame(Scope scope)
    {
        while (!ReferenceEquals(scope, RootScope) && scope.Parent is { } parent && !ReferenceEquals(parent, RootScope))
        {
            scope = parent;
        }

        return scope;
    }
}

public static class TemplateContextExtensions
{
    public static TemplateContext SetValue(this TemplateContext context, string name, int value)
    {
        return context.SetValue(name, NumberValue.Create(value));
    }

    public static TemplateContext SetValue(this TemplateContext context, string name, string value)
    {
        return context.SetValue(name, StringValue.Create(value));
    }

    public static TemplateContext SetValue(this TemplateContext context, string name, char value)
    {
        return context.SetValue(name, StringValue.Create(value));
    }

    public static TemplateContext SetValue(this TemplateContext context, string name, bool value)
    {
        return context.SetValue(name, BooleanValue.Create(value));
    }

    public static TemplateContext SetValue(this TemplateContext context, string name, object value)
    {
        if (value == null)
        {
            return context.SetValue(name, NilValue.Instance);
        }

        return context.SetValue(name, FluidValue.Create(value, context.Options));
    }

    public static TemplateContext SetValue(this TemplateContext context, string name, Func<FluidValue> factory)
    {
        return context.SetValue(name, FactoryValue.Create(factory));
    }
}
