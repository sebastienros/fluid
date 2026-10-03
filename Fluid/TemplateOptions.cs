using System.Globalization;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Fluid;

/// <summary>
/// Represents the immutable, shareable configuration used to render templates.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created with <see cref="TemplateOptionsBuilder"/> and can't be modified afterwards, which makes them
/// safe to create once and share between threads, tenants and renderings. Use <see cref="ToBuilder"/> to derive a
/// variation. Per-rendering overrides belong on <see cref="TemplateContext"/>, which is not thread-safe.
/// </para>
/// <para>
/// Objects supplied by the application, like the <see cref="FileProvider"/>, the <see cref="TemplateCache"/>, the
/// delegates or the <see cref="JsonSerializerOptions"/>, are used as-is and remain responsible for their own thread-safety.
/// </para>
/// </remarks>
public sealed class TemplateOptions
{
    /// <param name="identifier">The name of the property that is assigned.</param>
    /// <param name="value">The value that is assigned.</param>
    /// <param name="context">The <see cref="TemplateContext" /> instance used for rendering the template.</param>
    /// <returns>The value which should be assigned to the property.</returns>
    public delegate ValueTask<FluidValue> AssignedDelegate(string identifier, FluidValue value, TemplateContext context);

    /// <param name="identifier">The name of the property that is assigned.</param>
    /// <param name="value">The value that is assigned.</param>
    /// <param name="context">The <see cref="TemplateContext" /> instance used for rendering the template.</param>
    /// <returns>The value which should be captured.</returns>
    public delegate ValueTask<FluidValue> CapturedDelegate(string identifier, FluidValue value, TemplateContext context);

    /// <param name="name">The name of the value that is undefined.</param>
    /// <param name="type">The type of the parent object whose member is undefined, if any.</param>
    /// <returns>The value to use for the undefined value.</returns>
    public delegate ValueTask<FluidValue> UndefinedDelegate(string name, Type type);

    /// <summary>
    /// Represents the method that will handle the template parsed event.
    /// </summary>
    /// <param name="path">The path of the template that was parsed.</param>
    /// <param name="template">The template that was parsed.</param>
    /// <returns>The template to use, which may be modified by applying AST visitors or rewriters.</returns>
    public delegate IFluidTemplate TemplateParsedDelegate(string path, IFluidTemplate template);

    /// <summary>
    /// Gets the options used when none are specified. They have the default configuration of a new <see cref="TemplateOptionsBuilder"/>.
    /// </summary>
    public static readonly TemplateOptions Default = new TemplateOptionsBuilder().Build();

    private readonly Func<object, object>[] _valueConverters;
    private readonly Func<MemberAccessStrategy> _memberAccessStrategyFactory;
    private readonly Action<MemberAccessStrategy>[] _memberAccessConfigurations;
    private readonly FilterCollection _filters;
    private readonly ReadOnlyDictionary<string, FluidValue> _globalValues;
    private readonly ReadOnlyCollection<Func<object, object>> _valueConverterList;

    internal TemplateOptions(TemplateOptionsBuilder builder)
    {
        _filters = builder.FilterCollection.ToReadOnly();
        _valueConverters = builder.ValueConverterList.ToArray();
        _memberAccessStrategyFactory = builder.MemberAccessStrategyFactory;
        _memberAccessConfigurations = builder.MemberAccessConfigurationList.ToArray();

        _valueConverterList = new ReadOnlyCollection<Func<object, object>>(_valueConverters);
        _globalValues = new ReadOnlyDictionary<string, FluidValue>(new Dictionary<string, FluidValue>(builder.GlobalValueMap, StringComparer.Ordinal));
        GlobalScope = new Scope();

        foreach (var globalValue in _globalValues)
        {
            GlobalScope.SetOwnValue(globalValue.Key, globalValue.Value);
        }

        var strategy = _memberAccessStrategyFactory() ?? throw new InvalidOperationException("The member access strategy factory returned null.");

        foreach (var configuration in _memberAccessConfigurations)
        {
            configuration(strategy);
        }

        strategy.MakeReadOnly();
        MemberAccessStrategy = strategy;

        OutputBufferSize = builder.OutputBufferSize;
        StrictVariables = builder.StrictVariables;
        StrictFilters = builder.StrictFilters;
        FileProvider = builder.FileProvider;
        TemplateCache = builder.TemplateCache;
        DefaultFileExtension = builder.DefaultFileExtension;
        MaxSteps = builder.MaxSteps;
        MaxOutputSize = builder.MaxOutputSize;
        MaxCollectionSize = builder.MaxCollectionSize;
        ModelNamesComparer = builder.ModelNamesComparer;
        CultureInfo = builder.CultureInfo;
        MoneyOptions = builder.MoneyOptions;
        Now = builder.Now;
        TimeZone = builder.TimeZone;
        TimeZoneResolver = builder.TimeZoneResolver;
        MaxRecursion = builder.MaxRecursion;
        Captured = builder.Captured;
        Assigned = builder.Assigned;
        Undefined = builder.Undefined;
        TemplateParsed = builder.TemplateParsed;
        JsonSerializerOptions = builder.JsonSerializerOptions;
        Trimming = builder.Trimming;
        Greedy = builder.Greedy;
    }

    /// <summary>
    /// Gets the size (in chars) of the internal output buffer used when rendering to a <see cref="TextWriter"/>.
    /// When the buffer is full, content is written to the underlying writer and the buffer is reused.
    /// A value of 0 disables buffering.
    /// </summary>
    /// <remarks>
    /// Default is 16KB.
    /// </remarks>
    public int OutputBufferSize { get; }

    /// <summary>
    /// Gets whether any access to an undefined variable during template rendering will
    /// immediately throw an <see cref="InvalidOperationException"/>. The default is <c>false</c>, which
    /// renders undefined variables as empty strings (unless an <see cref="Undefined"/> delegate is provided).
    /// </summary>
    public bool StrictVariables { get; }

    /// <summary>
    /// Gets whether using an unknown filter name in a template will
    /// immediately throw an <see cref="InvalidOperationException"/> instead of
    /// silently ignoring the filter or returning the input value. The default is <c>false</c>.
    /// </summary>
    public bool StrictFilters { get; }

    /// <summary>
    /// Gets the members than can be accessed in a template.
    /// </summary>
    /// <remarks>
    /// The strategy is owned by these options. Explicit registrations can only be made with
    /// <see cref="TemplateOptionsBuilder.ConfigureMemberAccess(Action{MemberAccessStrategy})"/>.
    /// </remarks>
    public MemberAccessStrategy MemberAccessStrategy { get; }

    /// <summary>
    /// Gets the <see cref="ITemplateFileProvider"/> used to access files for include, render, and from statements.
    /// </summary>
    public ITemplateFileProvider FileProvider { get; }

    /// <summary>
    /// Gets the <see cref="ITemplateCache"/> used to cache templates loaded from <see cref="FileProvider"/>.
    /// </summary>
    /// <remarks>
    /// The instance needs to be thread-safe for insertion and retrieval of cached entries.
    /// </remarks>
    public ITemplateCache TemplateCache { get; }

    /// <summary>
    /// Gets the default file extension to use when loading templates with include and render statements.
    /// If set, the file provider will first check if the file exists with the specified name, then try appending this extension.
    /// If null or empty, the filename is used as-is without any extension appending.
    /// </summary>
    /// <value>
    /// Default value is ".liquid"
    /// </value>
    public string DefaultFileExtension { get; }

    /// <summary>
    /// Gets the maximum number of steps a script can execute. 0 means unlimited.
    /// </summary>
    public int MaxSteps { get; }

    /// <summary>
    /// Gets the maximum number of characters a template or captured block can render.
    /// 0 means unlimited.
    /// </summary>
    public int MaxOutputSize { get; }

    /// <summary>
    /// Gets the maximum number of items a template operation can materialize.
    /// 0 means unlimited.
    /// </summary>
    public int MaxCollectionSize { get; }

    /// <summary>
    /// Gets the <see cref="StringComparer"/> to use when comparing model names.
    /// </summary>
    /// <value>
    /// Default value is <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// </value>
    public StringComparer ModelNamesComparer { get; }

    /// <summary>
    /// Gets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
    /// </summary>
    public CultureInfo CultureInfo { get; }

    /// <summary>
    /// Gets the options used by the money filters. These filters are not registered by default,
    /// use <see cref="TemplateOptionsBuilder.WithMoneyFilters"/> to add them to <see cref="Filters"/>.
    /// </summary>
    public MoneyOptions MoneyOptions { get; }

    /// <summary>
    /// Gets the function returning the value of the "now" keyword.
    /// </summary>
    public Func<DateTimeOffset> Now { get; }

    /// <summary>
    /// Gets the local time zone used when parsing or creating dates without specific ones.
    /// </summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// Gets the function used by the <c>time_zone</c> filter to resolve a time zone identifier.
    /// </summary>
    /// <remarks>
    /// The default uses <see cref="TimeZoneInfo.FindSystemTimeZoneById(string)"/> on .NET 6 and later.
    /// </remarks>
    public Func<string, TimeZoneInfo> TimeZoneResolver { get; }

    /// <summary>
    /// Gets the maximum depth of recursions a script can execute. 100 by default.
    /// </summary>
    public int MaxRecursion { get; }

    /// <summary>
    /// Gets the read-only collection of filters available in the templates.
    /// </summary>
    public FilterCollection Filters => _filters;

    /// <summary>
    /// Gets values that are available in all templates rendered with these options.
    /// </summary>
    public IReadOnlyDictionary<string, FluidValue> GlobalValues => _globalValues;

    /// <summary>
    /// Gets the list of value converters.
    /// </summary>
    public IReadOnlyList<Func<object, object>> ValueConverters => _valueConverterList;

    /// <summary>
    /// Gets the delegate to execute when a Capture tag has been evaluated.
    /// </summary>
    public CapturedDelegate Captured { get; }

    /// <summary>
    /// Gets the delegate to execute when an Assign tag has been evaluated.
    /// </summary>
    public AssignedDelegate Assigned { get; }

    /// <summary>
    /// Gets the delegate to execute when an undefined value is encountered during rendering.
    /// </summary>
    public UndefinedDelegate Undefined { get; }

    /// <summary>
    /// Gets the delegate to execute when a template is parsed during include or render statements.
    /// This can be used to apply AST visitors or rewriters to modify templates before they are rendered or cached.
    /// </summary>
    public TemplateParsedDelegate TemplateParsed { get; }

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used by the <c>json</c> filter.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Gets the default trimming rules.
    /// </summary>
    public TrimmingFlags Trimming { get; }

    /// <summary>
    /// Gets whether trimming is greedy. Default is true. When true, all successive blank chars are trimmed.
    /// </summary>
    public bool Greedy { get; }

    internal Scope GlobalScope { get; }

    internal Func<object, object>[] ValueConverterArray => _valueConverters;

    internal bool HasValueConverters => _valueConverters.Length != 0;

    internal Func<MemberAccessStrategy> MemberAccessStrategyFactory => _memberAccessStrategyFactory;

    internal Action<MemberAccessStrategy>[] MemberAccessConfigurations => _memberAccessConfigurations;

    /// <summary>
    /// Creates a <see cref="TemplateOptionsBuilder"/> initialized with the configuration of these options.
    /// </summary>
    /// <remarks>
    /// The builder gets its own copies of the filters, global values and value converters, and builds its own
    /// <see cref="MemberAccessStrategy"/>, so changing it never affects these options.
    /// </remarks>
    public TemplateOptionsBuilder ToBuilder() => new(this);

    internal static TimeZoneInfo ResolveTimeZone(string id)
    {
#if NET6_0_OR_GREATER
        return TimeZoneInfo.FindSystemTimeZoneById(id);
#else
        return TimeZoneConverter.TZConvert.GetTimeZoneInfo(id);
#endif
    }
}
