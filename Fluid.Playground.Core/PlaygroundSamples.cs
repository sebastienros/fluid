namespace Fluid.Playground;

/// <summary>A sample of the playground: a name and the state it loads.</summary>
public sealed record PlaygroundSample(string Id, string Name, PlaygroundState State);

/// <summary>The samples the playground offers. The first one is loaded when the URL has no state.</summary>
public static class PlaygroundSamples
{
    private const string Products = """
        {
          "store": "Fluid Fruits",
          "products": [
            { "name": "Apple", "price": 3.29, "tags": ["red", "crunchy"], "available": true },
            { "name": "Orange", "price": 2.5, "tags": ["citrus"], "available": true },
            { "name": "Banana", "price": 0.99, "tags": ["yellow", "sweet"], "available": false }
          ]
        }
        """;

    /// <summary>All the samples.</summary>
    public static IReadOnlyList<PlaygroundSample> All { get; } =
    [
        new("products", "Products", new PlaygroundState
        {
            Template = """
                <h1>{{ store | upcase }}</h1>
                <ul>
                {%- for product in products %}
                  <li class="{% cycle 'odd', 'even' %}">
                    {{ forloop.index }}. {{ product.name }}: {{ product.price | times: 1.2 | round: 2 }}
                    {%- unless product.available %} (sold out){% endunless %}
                    <small>{{ product.tags | join: ', ' }}</small>
                  </li>
                {%- endfor %}
                </ul>
                {% assign total = products | map: 'price' | sum %}
                <p>{{ products.size }} products, {{ total | round: 2 }} in total.</p>
                """,
            Model = Products,
        }),

        new("filters", "Filters", new PlaygroundState
        {
            Template = """
                {{ "  Hello, World!  " | strip | downcase | replace: "world", "Fluid" }}
                {{ "liquid templates for .NET" | capitalize | truncatewords: 2 }}
                {{ "a,b,c" | split: "," | reverse | join: " < " }}
                {{ 10 | divided_by: 3.0 | round: 3 }} {{ -12.5 | abs | ceil }}
                {{ "2024-03-15T10:30:00Z" | date: "%A, %B %-d %Y at %H:%M" }}
                {{ products | where: "available" | map: "name" | sort | join: ", " }}
                {{ products | sort: "price" | first | map: "name" }}
                {{ "Fluid & <Liquid>" | escape }} {{ "a b&c" | url_encode }}
                {{ products[0] | json }}
                """,
            Model = Products,
        }),

        new("whitespace", "Whitespace control", new PlaygroundState
        {
            Template = """
                Without hyphens:
                {% for i in (1..3) %}
                  {{ i }}
                {% endfor %}
                With hyphens:
                {%- for i in (1..3) -%}
                  {{ i }}
                {%- endfor %}
                [{% if true %} {% assign a = 1 %} {% endif %}]
                """,
        }),

        new("json", "JSON output", new PlaygroundState
        {
            Template = """
                {%- assign available = products | where: "available" -%}
                {
                  "store": {{ store | json }},
                  "available": [
                  {%- for product in available %}
                    { "name": {{ product.name | json }}, "price": {{ product.price }}, "tags": {{ product.tags | json }} }
                    {%- unless forloop.last %},{% endunless %}
                  {%- endfor %}
                  ]
                }
                """,
            Model = Products,
        }),

        new("xml", "XML feed", new PlaygroundState
        {
            Template = """
                <?xml version="1.0" encoding="utf-8"?>
                <rss version="2.0">
                  <channel>
                    <title>{{ store | xml_escape }}</title>
                    {%- for product in products %}
                    <item>
                      <title>{{ product.name | xml_escape }}</title>
                      <category>{{ product.tags | first }}</category>
                    </item>
                    {%- endfor %}
                  </channel>
                </rss>
                """,
            Model = Products,
        }),

        new("markdown", "Markdown email", new PlaygroundState
        {
            Template = """
                # Your order at {{ store }}

                Thanks for your order! Here is what we will ship:

                | Product | Price |
                | --- | ---: |
                {%- for product in products %}{% if product.available %}
                | {{ product.name }} | {{ product.price | times: 1.2 | round: 2 }} |
                {%- endif %}{% endfor %}

                {% for product in products %}{% unless product.available -%}
                > **{{ product.name }}** is sold out, we will tell you when it is back.
                {% endunless %}{% endfor %}
                Questions? [Contact us](https://example.com/contact).
                """,
            Model = Products,
        }),

        new("csharp", "C# code generation", new PlaygroundState
        {
            Template = """
                // Generated from the model.
                namespace {{ namespace }};

                /// <summary>{{ description }}</summary>
                public sealed record {{ name }}(
                {%- for property in properties %}
                    {{ property.type }} {{ property.name | capitalize }}{% unless forloop.last %},{% endunless %}
                {%- endfor %});
                """,
            Model = """
                {
                  "namespace": "Contoso.Store",
                  "name": "Product",
                  "description": "A product of the store.",
                  "properties": [
                    { "name": "name", "type": "string" },
                    { "name": "price", "type": "decimal" },
                    { "name": "available", "type": "bool" }
                  ]
                }
                """,
        }),

        new("partials", "Partials", new PlaygroundState
        {
            Template = """
                {% for product in products %}
                {%- render 'card', product: product, currency: '$' %}
                {% endfor %}
                {% render 'card' for products as product %}
                """,
            Partials =
            [
                new("card", "<div class=\"card\">{{ product.name }}: {{ currency | default: '€' }}{{ product.price }}</div>"),
            ],
            Model = Products,
        }),

        new("macros", "Macros and functions", new PlaygroundState
        {
            Template = """
                {% macro field(name, value='', type='text') %}
                  <label>{{ name | capitalize }} <input type="{{ type }}" name="{{ name }}" value="{{ value }}"></label>
                {%- endmacro %}

                {{ field('user', 'ada') }}
                {{ field('password', type='password') }}
                {{ 1 | plus: (2 | times: 3) }}
                """,
            Options = new PlaygroundOptions { AllowFunctions = true, AllowParentheses = true },
        }),

        new("liquid-tag", "The liquid tag", new PlaygroundState
        {
            Template = """
                {% liquid
                  assign names = products | map: 'name'
                  for name in names
                    if forloop.first
                      echo name | upcase
                    else
                      echo ', ' | append: name
                    endif
                  endfor
                %}
                """,
            Model = Products,
            Options = new PlaygroundOptions { AllowLiquidTag = true },
        }),

        new("money", "Money and cultures", new PlaygroundState
        {
            Template = """
                {{ 1134.65 | money }}
                {{ 1134.65 | money_with_currency }}
                {{ 10 | money: 'JPY' }}
                {{ 1234.5 | format_number: "N2" }}
                {{ "2024-03-15" | date: "%A %-d %B %Y" }}
                {{ 'rgb(10%, 10%, 20%)' | color_to_hex }} {{ '#1693e6' | color_lighten: 20 }}
                """,
            Options = new PlaygroundOptions { MoneyFilters = true, ColorFilters = true, Culture = "fr-FR" },
        }),

        new("time-zones", "Dates and time zones", new PlaygroundState
        {
            Template = """
                Parsed in the time zone: {{ "2024-03-15 12:00" | date: "%c %z" }}
                Converted: {{ "2024-03-15T12:00:00Z" | time_zone: "Asia/Tokyo" | date: "%c %z" }}
                Seconds since 1970: {{ 86400 | date: "%Y-%m-%d %H:%M %z" }}
                """,
            Options = new PlaygroundOptions { TimeZone = "America/New_York" },
        }),

        new("strict", "Strict variables", new PlaygroundState
        {
            Template = """
                Hello {{ user.name }}!
                """,
            Model = """{ "user": { "first_name": "Ada" } }""",
            Options = new PlaygroundOptions { StrictVariables = true },
        }),
    ];

    /// <summary>The sample loaded when the URL has no state.</summary>
    public static PlaygroundSample Default => All[0];
}
