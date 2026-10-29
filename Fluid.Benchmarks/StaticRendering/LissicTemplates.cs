using Lissic;

namespace Fluid.Benchmarks.StaticRendering;

// Lissic templates only have values, sub-templates, loops of sub-templates and branches. A loop renders a list of
// templates the caller builds, so the list is a template and each product another one.
[TemplateText("""
<ul id='products'>
{{ loop.products }}
</ul>
""")]
public partial class LissicProductList
{
}

// Lissic doesn't encode values, htmlTextSafe does. Its truncate filter doesn't add an ellipsis, unlike Liquid's.
[TemplateText("""
    <li>
      <h2>{{ name <htmlTextSafe> }}</h2>
           Only {{ price }}
           {{ description <truncate('12') | append('...') | htmlTextSafe> }}
    </li>

""")]
public partial class LissicProduct
{
}
