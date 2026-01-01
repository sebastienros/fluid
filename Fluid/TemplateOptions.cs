using System.Globalization;
using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

namespace Fluid
{
    public class TemplateOptions
    {
        /// <summary>
        /// Gets or sets the size (in chars) of the internal output buffer used when rendering to a <see cref="TextWriter"/>.
        /// When the buffer is full, content is written to the underlying writer and the buffer is reused.
        /// Set to 0 to disable buffering.
        /// </summary>
        /// <remarks>
        /// Default is 16KB.
        /// </remarks>
        public int OutputBufferSize { get; set; } = 16 * 1024;

        /// <summary>
        /// When set to <c>true</c>, any access to an undefined variable during template rendering will
        /// immediately throw an <see cref="InvalidOperationException"/>. The default is <c>false</c>, which
        /// renders undefined variables as empty strings (unless an <see cref="Undefined"/> delegate is provided).
        /// This property can be set at any time and is checked when undefined variables are accessed.
        /// </summary>
        public bool StrictVariables { get; set; }

        /// <summary>
        /// When set to <c>true</c>, using an unknown filter name in a template will
        /// immediately throw an <see cref="InvalidOperationException"/> instead of
        /// silently ignoring the filter or returning the input value. The default is <c>false</c>.
        /// This can be toggled after construction to enforce stricter authoring rules.
        /// </summary>
        public bool StrictFilters { get; set; }

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
        /// <returns>The value to use for the undefined value.</returns>
        public delegate ValueTask<FluidValue> UndefinedDelegate(string name);

        /// <summary>
        /// Represents the method that will handle the template parsed event.
        /// </summary>
        /// <param name="path">The path of the template that was parsed.</param>
        /// <param name="template">The template that was parsed.</param>
        /// <returns>The template to use, which may be modified by applying AST visitors or rewriters.</returns>
        public delegate IFluidTemplate TemplateParsedDelegate(string path, IFluidTemplate template);

        public static readonly TemplateOptions Default = new();

        /// <summary>
        /// Gets ot sets the members than can be accessed in a template.
        /// </summary>
        public MemberAccessStrategy MemberAccessStrategy { get; set; } = new DefaultMemberAccessStrategy();

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider"/> used to access files for include and render statements.
        /// </summary>
        public IFileProvider FileProvider { get; set; } = new NullFileProvider();

        /// <summary>
        /// Gets or sets the <see cref="ITemplateCache"/> used to cache templates loaded from <see cref="FileProvider"/>.
        /// </summary>
        /// <remarks>
        /// The instance needs to be thread-safe for insertion and retrieval of cached entries.
        /// </remarks>
        public ITemplateCache TemplateCache { get; set; } = new TemplateCache();

        /// <summary>
        /// Gets or sets the default file extension to use when loading templates with include and render statements.
        /// If set, the file provider will first check if the file exists with the specified name, then try appending this extension.
        /// If null or empty, the filename is used as-is without any extension appending.
        /// </summary>
        /// <value>
        /// Default value is ".liquid"
        /// </value>
        public string DefaultFileExtension { get; set; } = ".liquid";

        /// <summary>
        /// Gets or sets the maximum number of steps a script can execute. Leave to 0 for unlimited.
        /// </summary>
        public int MaxSteps { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="StringComparer"/> to use when comparing model names.
        /// </summary>
        /// <value>
        /// Default value is <see cref="StringComparer.OrdinalIgnoreCase"/>
        /// </value>
        public StringComparer ModelNamesComparer { get; set; } = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Gets or sets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
        /// </summary>
        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Gets or sets the value returned by the "now" keyword.
        /// </summary>
        public Func<DateTimeOffset> Now { get; set; } = static () => DateTimeOffset.Now;

        /// <summary>
        /// Gets or sets the local time zone used when parsing or creating dates without specific ones.
        /// </summary>
        public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

        /// <summary>
        /// Gets or sets the maximum depth of recursions a script can execute. 100 by default.
        /// </summary>
        public int MaxRecursion { get; set; } = 100;

        /// <summary>
        /// Gets the collection of filters available in the templates.
        /// </summary>
        public FilterCollection Filters { get; } = new FilterCollection();

        /// <summary>
        /// Gets a scope that is available in all the templates.
        /// </summary>
        public Scope Scope { get; } = new Scope();

        /// <summary>
        /// Gets the list of value converters.
        /// </summary>
        public List<Func<object, object>> ValueConverters { get; } = new List<Func<object, object>>();

        /// <summary>
        /// Gets or sets the delegate to execute when a Capture tag has been evaluated.
        /// </summary>
        public CapturedDelegate Captured { get; set; }

        /// <summary>
        /// Gets or sets the delegate to execute when an Assign tag has been evaluated.
        /// </summary>
        public AssignedDelegate Assigned { get; set; }

        /// <summary>
        /// Gets or sets the delegate to execute when an undefined value is encountered during rendering.
        /// </summary>
        public UndefinedDelegate Undefined { get; set; }

        /// <summary>
        /// Gets or sets the delegate to execute when a template is parsed during include or render statements.
        /// This can be used to apply AST visitors or rewriters to modify templates before they are rendered or cached.
        /// </summary>
        public TemplateParsedDelegate TemplateParsed { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JsonSerializerOptions"/> used by the <c>json</c> filter.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonSerializerOptions.Default;

        /// <summary>
        /// Gets or sets the default trimming rules.
        /// </summary>
        public TrimmingFlags Trimming { get; set; } = TrimmingFlags.None;

        /// <summary>
        /// Gets or sets whether trimming is greedy. Default is true. When set to true, all successive blank chars are trimmed.
        /// </summary>
        public bool Greedy { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateOptions"/> class.
        /// </summary>
        public TemplateOptions()
        {
            Filters.WithArrayFilters()
                .WithStringFilters()
                .WithNumberFilters()
                .WithMiscFilters();
        }
    }
}
