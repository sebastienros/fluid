using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid
{
    public class TemplateOptions
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

        public static readonly TemplateOptions Default = new();

        private static readonly JavaScriptEncoder DefaultJavaScriptEncoder = JavaScriptEncoder.Default;

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
        /// Gets or sets the <see cref="JavaScriptEncoder" /> instance used by the <c>json</c> filter.
        /// </summary>
        public JavaScriptEncoder JavaScriptEncoder { get; set; } = DefaultJavaScriptEncoder;

        /// <summary>
        /// Gets or sets the default trimming rules.
        /// </summary>
        public TrimmingFlags Trimming { get; set; } = TrimmingFlags.None;

        /// <summary>
        /// Gets or sets whether trimming is greedy. Default is true. When set to true, all successive blank chars are trimmed.
        /// </summary>
        public bool Greedy { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to throw an exception when a template variable is not found.
        /// When true, accessing undefined variables will collect all missing variables and throw a StrictVariableException.
        /// Default is false.
        /// </summary>
        public bool StrictVariables { get; set; } = false;

        public TemplateOptions()
        {
            Filters.WithArrayFilters()
                .WithStringFilters()
                .WithNumberFilters()
                .WithMiscFilters();
        }
    }
}
