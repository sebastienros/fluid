using System.Globalization;
using System.Text.Json;
using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;

namespace Fluid;

/// <summary>
/// Configures and creates immutable <see cref="TemplateOptions"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// A builder is a short-lived, single-threaded configuration object. Every call to <see cref="Build"/> returns
/// an independent <see cref="TemplateOptions"/>: filters, global values and value converters are copied, and a new
/// <see cref="MemberAccessStrategy"/> is created, so changing the builder afterwards never affects options that
/// were already built and shared.
/// </para>
/// <para>
/// A new builder registers the array, string, number and misc filters. Use <see cref="WithMoneyFilters"/> or
/// <see cref="WithColorFilters"/> for the opt-in ones.
/// </para>
/// </remarks>
public class TemplateOptionsBuilder
{
    private static readonly Func<MemberAccessStrategy> DefaultMemberAccessStrategyFactory = static () => new DefaultMemberAccessStrategy();

    private readonly FilterCollection _filters = new();
    private readonly Dictionary<string, FluidValue> _globalValues = new(StringComparer.Ordinal);
    private readonly List<Func<object, object>> _valueConverters = [];
    private readonly List<Action<MemberAccessStrategy>> _memberAccessConfigurations = [];
    private Func<MemberAccessStrategy> _memberAccessStrategyFactory = DefaultMemberAccessStrategyFactory;

    private int _outputBufferSize = 16 * 1024;
    private bool _strictVariables;
    private bool _strictFilters;
    private ITemplateFileProvider _fileProvider = new FileProviderTemplateFileProvider(new NullFileProvider());
    private ITemplateCache _templateCache = new TemplateCache();
    private string _defaultFileExtension = ".liquid";
    private int _maxSteps;
    private int _maxOutputSize;
    private int _maxCollectionSize;
    private StringComparer _modelNamesComparer = StringComparer.OrdinalIgnoreCase;
    private CultureInfo _cultureInfo = CultureInfo.InvariantCulture;
    private MoneyOptions _moneyOptions = new();
    private Func<DateTimeOffset> _now = static () => DateTimeOffset.Now;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Local;
    private Func<string, TimeZoneInfo> _timeZoneResolver = TemplateOptions.ResolveTimeZone;
    private int _maxRecursion = 100;
    private TemplateOptions.CapturedDelegate _captured;
    private TemplateOptions.AssignedDelegate _assigned;
    private TemplateOptions.UndefinedDelegate _undefined;
    private TemplateOptions.TemplateParsedDelegate _templateParsed;
    private JsonSerializerOptions _jsonSerializerOptions = JsonSerializerOptions.Default;
    private TrimmingFlags _trimming = TrimmingFlags.None;
    private bool _greedy = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateOptionsBuilder"/> class.
    /// </summary>
    public TemplateOptionsBuilder()
    {
        _filters
            .WithArrayFilters()
            .WithStringFilters()
            .WithNumberFilters()
            .WithMiscFilters();

        if (this is ITemplateOptionsMemberAccessorRegistrar registrar)
        {
            registrar.RegisterMemberAccessors(this);
        }
    }

    internal TemplateOptionsBuilder(TemplateOptions options)
    {
        _filters = options.Filters.ToMutableCopy();
        _globalValues = new Dictionary<string, FluidValue>(options.GlobalValues.Count, StringComparer.Ordinal);

        foreach (var globalValue in options.GlobalValues)
        {
            _globalValues[globalValue.Key] = globalValue.Value;
        }

        _valueConverters.AddRange(options.ValueConverterArray);
        _memberAccessConfigurations.AddRange(options.MemberAccessConfigurations);
        _memberAccessStrategyFactory = options.MemberAccessStrategyFactory;

        _outputBufferSize = options.OutputBufferSize;
        _strictVariables = options.StrictVariables;
        _strictFilters = options.StrictFilters;
        _fileProvider = options.FileProvider;
        _templateCache = options.TemplateCache;
        _defaultFileExtension = options.DefaultFileExtension;
        _maxSteps = options.MaxSteps;
        _maxOutputSize = options.MaxOutputSize;
        _maxCollectionSize = options.MaxCollectionSize;
        _modelNamesComparer = options.ModelNamesComparer;
        _cultureInfo = options.CultureInfo;
        _moneyOptions = options.MoneyOptions;
        _now = options.Now;
        _timeZone = options.TimeZone;
        _timeZoneResolver = options.TimeZoneResolver;
        _maxRecursion = options.MaxRecursion;
        _captured = options.Captured;
        _assigned = options.Assigned;
        _undefined = options.Undefined;
        _templateParsed = options.TemplateParsed;
        _jsonSerializerOptions = options.JsonSerializerOptions;
        _trimming = options.Trimming;
        _greedy = options.Greedy;
    }

    internal FilterCollection FilterCollection => _filters;
    internal Dictionary<string, FluidValue> GlobalValueMap => _globalValues;
    internal List<Func<object, object>> ValueConverterList => _valueConverters;
    internal List<Action<MemberAccessStrategy>> MemberAccessConfigurationList => _memberAccessConfigurations;
    internal Func<MemberAccessStrategy> MemberAccessStrategyFactory => _memberAccessStrategyFactory;

    internal int OutputBufferSize => _outputBufferSize;
    internal bool StrictVariables => _strictVariables;
    internal bool StrictFilters => _strictFilters;
    internal ITemplateFileProvider FileProvider => _fileProvider;
    internal ITemplateCache TemplateCache => _templateCache;
    internal string DefaultFileExtension => _defaultFileExtension;
    internal int MaxSteps => _maxSteps;
    internal int MaxOutputSize => _maxOutputSize;
    internal int MaxCollectionSize => _maxCollectionSize;
    internal StringComparer ModelNamesComparer => _modelNamesComparer;
    internal CultureInfo CultureInfo => _cultureInfo;
    internal MoneyOptions MoneyOptions => _moneyOptions;
    internal Func<DateTimeOffset> Now => _now;
    internal TimeZoneInfo TimeZone => _timeZone;
    internal Func<string, TimeZoneInfo> TimeZoneResolver => _timeZoneResolver;
    internal int MaxRecursion => _maxRecursion;
    internal TemplateOptions.CapturedDelegate Captured => _captured;
    internal TemplateOptions.AssignedDelegate Assigned => _assigned;
    internal TemplateOptions.UndefinedDelegate Undefined => _undefined;
    internal TemplateOptions.TemplateParsedDelegate TemplateParsed => _templateParsed;
    internal JsonSerializerOptions JsonSerializerOptions => _jsonSerializerOptions;
    internal TrimmingFlags Trimming => _trimming;
    internal bool Greedy => _greedy;

    /// <summary>
    /// Creates immutable <see cref="TemplateOptions"/> from the current configuration.
    /// </summary>
    public TemplateOptions Build() => new(this);

    /// <summary>
    /// Sets the size (in chars) of the internal output buffer used when rendering to a <see cref="TextWriter"/>.
    /// Use 0 to disable buffering. The default is 16KB.
    /// </summary>
    public TemplateOptionsBuilder WithOutputBufferSize(int size)
    {
        _outputBufferSize = size;
        return this;
    }

    /// <summary>
    /// Sets whether accessing an undefined variable throws an <see cref="InvalidOperationException"/>. The default is <c>false</c>.
    /// </summary>
    public TemplateOptionsBuilder WithStrictVariables(bool strict = true)
    {
        _strictVariables = strict;
        return this;
    }

    /// <summary>
    /// Sets whether using an unknown filter throws an <see cref="InvalidOperationException"/>. The default is <c>false</c>.
    /// </summary>
    public TemplateOptionsBuilder WithStrictFilters(bool strict = true)
    {
        _strictFilters = strict;
        return this;
    }

    /// <summary>
    /// Sets the factory used by <see cref="Build"/> to create the <see cref="MemberAccessStrategy"/> of the options.
    /// The default creates a <see cref="DefaultMemberAccessStrategy"/>.
    /// </summary>
    /// <remarks>
    /// The factory is invoked by every <see cref="Build"/> call, so options never share a strategy that can be modified.
    /// Registrations made with <see cref="ConfigureMemberAccess(Action{MemberAccessStrategy})"/> are applied to the created strategy.
    /// </remarks>
    public TemplateOptionsBuilder WithMemberAccessStrategy(Func<MemberAccessStrategy> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _memberAccessStrategyFactory = factory;
        return this;
    }

    /// <summary>
    /// Sets the type of <see cref="MemberAccessStrategy"/> created by every <see cref="Build"/> call.
    /// </summary>
    public TemplateOptionsBuilder WithMemberAccessStrategy<T>() where T : MemberAccessStrategy, new()
    {
        _memberAccessStrategyFactory = static () => new T();
        return this;
    }

    /// <summary>
    /// Registers members that can be accessed in templates. The configuration is applied, in order, to the
    /// <see cref="MemberAccessStrategy"/> created by every <see cref="Build"/> call.
    /// </summary>
    /// <example>
    /// <code>builder.ConfigureMemberAccess(strategy => strategy.Register&lt;Person&gt;("FirstName", x => x.FirstName));</code>
    /// </example>
    public TemplateOptionsBuilder ConfigureMemberAccess(Action<MemberAccessStrategy> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _memberAccessConfigurations.Add(configure);
        return this;
    }

    /// <summary>
    /// Sets the <see cref="ITemplateFileProvider"/> used to access files for include, render, and from statements.
    /// </summary>
    public TemplateOptionsBuilder WithFileProvider(ITemplateFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="ITemplateCache"/> used to cache templates loaded from the file provider.
    /// The instance needs to be thread-safe for insertion and retrieval of cached entries.
    /// </summary>
    public TemplateOptionsBuilder WithTemplateCache(ITemplateCache templateCache)
    {
        _templateCache = templateCache;
        return this;
    }

    /// <summary>
    /// Sets the default file extension used when loading templates with include and render statements.
    /// If <c>null</c> or empty, the filename is used as-is. The default is ".liquid".
    /// </summary>
    public TemplateOptionsBuilder WithDefaultFileExtension(string extension)
    {
        _defaultFileExtension = extension;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of steps a script can execute. Use 0 for unlimited.
    /// </summary>
    public TemplateOptionsBuilder WithMaxSteps(int maxSteps)
    {
        _maxSteps = maxSteps;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of characters a template or captured block can render. Use 0 for unlimited.
    /// </summary>
    public TemplateOptionsBuilder WithMaxOutputSize(int maxOutputSize)
    {
        _maxOutputSize = maxOutputSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of items a template operation can materialize. Use 0 for unlimited.
    /// </summary>
    public TemplateOptionsBuilder WithMaxCollectionSize(int maxCollectionSize)
    {
        _maxCollectionSize = maxCollectionSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum depth of recursions a script can execute. The default is 100.
    /// </summary>
    public TemplateOptionsBuilder WithMaxRecursion(int maxRecursion)
    {
        _maxRecursion = maxRecursion;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="StringComparer"/> used when comparing model names.
    /// The default is <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public TemplateOptionsBuilder WithModelNamesComparer(StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        _modelNamesComparer = comparer;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="CultureInfo"/> used to render locale values like dates and numbers.
    /// </summary>
    public TemplateOptionsBuilder WithCultureInfo(CultureInfo cultureInfo)
    {
        _cultureInfo = cultureInfo;
        return this;
    }

    /// <summary>
    /// Sets the options used by the money filters.
    /// </summary>
    public TemplateOptionsBuilder WithMoneyOptions(MoneyOptions moneyOptions)
    {
        _moneyOptions = moneyOptions;
        return this;
    }

    /// <summary>
    /// Sets the function returning the value of the "now" keyword.
    /// </summary>
    public TemplateOptionsBuilder WithNow(Func<DateTimeOffset> now)
    {
        _now = now;
        return this;
    }

    /// <summary>
    /// Sets the local time zone used when parsing or creating dates without specific ones.
    /// </summary>
    public TemplateOptionsBuilder WithTimeZone(TimeZoneInfo timeZone)
    {
        _timeZone = timeZone;
        return this;
    }

    /// <summary>
    /// Sets the function used by the <c>time_zone</c> filter to resolve a time zone identifier.
    /// </summary>
    public TemplateOptionsBuilder WithTimeZoneResolver(Func<string, TimeZoneInfo> resolver)
    {
        _timeZoneResolver = resolver;
        return this;
    }

    /// <summary>
    /// Registers filters. The collection is mutable until <see cref="Build"/> copies it.
    /// </summary>
    /// <example>
    /// <code>builder.ConfigureFilters(filters => filters.AddFilter("shout", (input, args, ctx) => ...));</code>
    /// </example>
    public TemplateOptionsBuilder ConfigureFilters(Action<FilterCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(_filters);
        return this;
    }

    /// <summary>
    /// Registers a filter.
    /// </summary>
    public TemplateOptionsBuilder AddFilter(string name, FilterDelegate filter)
    {
        _filters.AddFilter(name, filter);
        return this;
    }

    /// <summary>
    /// Registers the color filters.
    /// </summary>
    public TemplateOptionsBuilder WithColorFilters()
    {
        _filters.WithColorFilters();
        return this;
    }

    /// <summary>
    /// Registers the money filters, which are configured with <see cref="WithMoneyOptions(MoneyOptions)"/>.
    /// </summary>
    public TemplateOptionsBuilder WithMoneyFilters()
    {
        _filters.WithMoneyFilters();
        return this;
    }

    /// <summary>
    /// Removes all the registered filters, including the default ones.
    /// </summary>
    public TemplateOptionsBuilder ClearFilters()
    {
        _filters.Clear();
        return this;
    }

    /// <summary>
    /// Sets a value that is available in all templates rendered with the options.
    /// </summary>
    public TemplateOptionsBuilder WithGlobalValue(string name, FluidValue value)
    {
        ArgumentNullException.ThrowIfNull(name);

        _globalValues[name] = value ?? NilValue.Instance;
        return this;
    }

    /// <summary>
    /// Adds a value converter, called in registration order when a CLR object is converted to a <see cref="FluidValue"/>.
    /// </summary>
    public TemplateOptionsBuilder AddValueConverter(Func<object, object> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        _valueConverters.Add(converter);
        return this;
    }

    /// <summary>
    /// Sets the delegate to execute when a Capture tag has been evaluated.
    /// </summary>
    public TemplateOptionsBuilder WithCaptured(TemplateOptions.CapturedDelegate captured)
    {
        _captured = captured;
        return this;
    }

    /// <summary>
    /// Sets the delegate to execute when an Assign tag has been evaluated.
    /// </summary>
    public TemplateOptionsBuilder WithAssigned(TemplateOptions.AssignedDelegate assigned)
    {
        _assigned = assigned;
        return this;
    }

    /// <summary>
    /// Sets the delegate to execute when an undefined value is encountered during rendering.
    /// </summary>
    public TemplateOptionsBuilder WithUndefined(TemplateOptions.UndefinedDelegate undefined)
    {
        _undefined = undefined;
        return this;
    }

    /// <summary>
    /// Sets the delegate to execute when a template is parsed during include or render statements.
    /// This can be used to apply AST visitors or rewriters to modify templates before they are rendered or cached.
    /// </summary>
    public TemplateOptionsBuilder WithTemplateParsed(TemplateOptions.TemplateParsedDelegate templateParsed)
    {
        _templateParsed = templateParsed;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="JsonSerializerOptions"/> used by the <c>json</c> filter.
    /// </summary>
    public TemplateOptionsBuilder WithJsonSerializerOptions(JsonSerializerOptions jsonSerializerOptions)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        return this;
    }

    /// <summary>
    /// Sets the default trimming rules.
    /// </summary>
    public TemplateOptionsBuilder WithTrimming(TrimmingFlags trimming)
    {
        _trimming = trimming;
        return this;
    }

    /// <summary>
    /// Sets whether trimming is greedy. The default is <c>true</c>: all successive blank chars are trimmed.
    /// </summary>
    public TemplateOptionsBuilder WithGreedy(bool greedy = true)
    {
        _greedy = greedy;
        return this;
    }
}
