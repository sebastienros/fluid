using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace Fluid.ViewEngine;

public class FluidViewEngineOptions
{
    /// <summary>
    /// Gets or set the <see cref="FluidViewParser"/> instance to use with the view engine.
    /// </summary>
    /// <remarks>
    /// To add custom tags which require special primitive elements, create a sub-class of <see cref="FluidViewParser"/>.
    /// </remarks>
    public FluidViewParser Parser { get; set; } = new FluidViewParser();

    /// <summary>
    /// Gets or sets the text encoder to use during rendering.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="DefaultTextEncoder"/>, which only encodes HTML-sensitive characters and leaves
    /// non-ASCII text such as <c>é</c> or <c>漢</c> as is. The ASP.NET Core integrations use the application's
    /// <see cref="HtmlEncoder"/> service instead when one is registered.
    /// </remarks>
    public TextEncoder TextEncoder = DefaultTextEncoder;

    /// <summary>
    /// Gets the default HTML encoder, <c>HtmlEncoder.Create(UnicodeRanges.All)</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="HtmlEncoder.Default"/>, which only allows Basic Latin, it doesn't turn non-ASCII
    /// characters into numeric entities. It still encodes <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, quotes,
    /// <c>+</c> and characters outside the Basic Multilingual Plane, such as emoji.
    /// </remarks>
    public static HtmlEncoder DefaultTextEncoder { get; } = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// Gets the builder used to configure the <see cref="TemplateOptions"/>.
    /// </summary>
    /// <remarks>
    /// The builder is read once, when <see cref="TemplateOptions"/> is first accessed, which is when the view renderer is created.
    /// Changes made afterwards are ignored.
    /// </remarks>
    public TemplateOptionsBuilder TemplateOptionsBuilder { get; } = new TemplateOptionsBuilder();

    /// <summary>
    /// Gets the immutable template options built from <see cref="TemplateOptionsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The options are built the first time they are accessed. Their file provider is
    /// <see cref="PartialsFileProvider"/>, then <see cref="ViewsFileProvider"/>, when one is set.
    /// </remarks>
    public TemplateOptions TemplateOptions
    {
        get
        {
            if (_templateOptions is null)
            {
                lock (_templateOptionsLock)
                {
                    if (_templateOptions is null)
                    {
                        var fileProvider = PartialsFileProvider ?? ViewsFileProvider;

                        if (fileProvider is not null)
                        {
                            TemplateOptionsBuilder.WithFileProvider(fileProvider);
                        }

                        _templateOptions = TemplateOptionsBuilder.Build();
                    }
                }
            }

            return _templateOptions;
        }
    }

    private readonly object _templateOptionsLock = new();
    private volatile TemplateOptions _templateOptions;

    /// <summary>
    /// Gets or sets the <see cref="ITemplateFileProvider"/> used to access views.
    /// </summary>
    public ITemplateFileProvider ViewsFileProvider { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ITemplateFileProvider"/> used to access includes.
    /// </summary>
    public ITemplateFileProvider PartialsFileProvider { get; set; }

    /// <summary>
    /// Gets the list of view location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
    /// </summary>
    /// <example>
    /// /Views/{0}.liquid
    /// </example>
    public List<string> ViewsLocationFormats { get; } = new();

    /// <summary>
    /// Gets the list of partial views location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
    /// </summary>
    /// <example>
    /// /Views/Includes/{0}.liquid
    /// </example>
    public List<string> PartialsLocationFormats { get; } = new();

    /// <summary>
    /// Gets the list of layout location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
    /// </summary>
    /// <example>
    /// /Views/Shared/{0}.liquid
    /// </example>
    public List<string> LayoutsLocationFormats { get; } = new();

    /// <summary>
    /// Gets or sets whether files should be reloaded automatically when changed. Default is <code>true</code>;
    /// </summary>
    public bool TrackFileChanges { get; set; } = true;

    /// <summary>
    /// <para>Represents the method that will handle the view rendering event.</para>
    /// </summary>
    public delegate ValueTask RenderingViewDelegate(string path, TemplateContext context);

    /// <summary>
    /// Gets or sets the delegate to execute when a view is rendered.
    /// </summary>
    public RenderingViewDelegate RenderingViewAsync { get; set; }
}
