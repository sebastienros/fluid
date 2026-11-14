---
title: Security
description: "How to render templates written by users safely, and the limits Fluid applies while rendering."
---

## Model security

Fluid templates can read public properties and fields from the model and from objects reachable through it. This is by design; a model passed to `TemplateContext` should be treated as the template's readable data boundary.

When rendering an untrusted template, pass a dedicated model that contains only the data the template is allowed to read. Do not pass domain entities, service objects, configuration objects, or other object graphs that may expose sensitive data through public members.

## Execution limits

### Limiting template recursion

When invoking `{% include 'sub-template' %}` statements, it is possible that some templates create an infinite recursion that could block the server.
To prevent this, the `TemplateOptions` class defines a default `MaxRecursion = 100` that prevents templates from having a depth greater than `100`.

### Limiting template execution

A template can inadvertently perform enough work to block the server. `MaxSteps` limits statements, loop iterations, range construction, and built-in collection enumeration. String filters are not counted per character; their cost follows the length of their input, and `MaxOutputSize` bounds what they produce. `MaxOutputSize` limits cumulative rendered output, captured blocks, macro results, and amplified string operations. `MaxCollectionSize` limits ranges and collections materialized by built-in operations.

`MaxSteps`, `MaxOutputSize`, and `MaxCollectionSize` are unlimited by default to preserve compatibility. Set all of them when rendering untrusted templates. `TemplateContext.CancellationToken` complements these limits and should also be set.

### Rendering user-provided templates safely

Fluid is a template engine, not a security sandbox. An application that accepts templates from users should apply all of the following controls:

1. Limit the template source length before parsing. Render limits do not limit parsing.
2. Pass a dedicated model containing only data the user is allowed to read. Templates can read public properties and fields from every object reachable through the model. Do not pass domain entities, services, dependency-injection containers, configuration, or secrets.
3. Configure recursion, work, output, and collection limits. The appropriate values depend on the templates and capacity of the application; start conservatively and adjust using representative load tests.
4. Set a per-render cancellation deadline in addition to the request cancellation token.
5. Keep the default null file provider unless user templates need `include`, `render`, or `from`. If they do, use a tenant-scoped or allow-listed provider that cannot access application files or another tenant's templates.
6. Expose only trusted custom filters, tags, values, and file providers. Extension code executes with the permissions of the application and must honor cancellation and equivalent resource limits.
7. Apply normal service protections such as request-size limits, authentication, rate limits, bounded concurrency, and memory or process isolation where the threat model requires it.

The following is an example starting point. The numerical limits are illustrative and should be tuned for the application:

```csharp
private const int MaxTemplateLength = 100_000;

private static readonly FluidParser Parser = new FluidParser();

// The file provider retains its default NullFileProvider.
private static readonly TemplateOptions UserTemplateOptions = new TemplateOptionsBuilder()
    .WithMaxRecursion(20)
    .WithMaxSteps(10_000)
    .WithMaxOutputSize(1_000_000)
    .WithMaxCollectionSize(10_000)
    .Build();

public static async ValueTask<string> RenderUserTemplateAsync(
    string source,
    SafeTemplateModel model,
    CancellationToken requestAborted)
{
    if (source.Length > MaxTemplateLength)
    {
        throw new ArgumentException("The template is too large.", nameof(source));
    }

    if (!Parser.TryParse(source, out var template, out var error))
    {
        throw new ArgumentException(error, nameof(source));
    }

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    timeout.CancelAfter(TimeSpan.FromSeconds(2));

    var context = new TemplateContext(model, UserTemplateOptions)
    {
        CancellationToken = timeout.Token
    };

    return await template.RenderAsync(context);
}
```

`TemplateOptions`, `FluidParser`, and parsed templates can be shared. Create a new `TemplateContext` for every render because it is not thread-safe and owns the execution counters and cancellation token.

When a file provider can return different content for the same path in different tenants or security scopes, set `TemplateSourceInfo.CacheKey` to a stable value that includes that scope. This prevents a cached parsed template from crossing the same boundary enforced by the provider.

`MaxOutputSize` counts UTF-16 characters written through Fluid's output abstraction. If the rendered result is encoded to a response with a separate byte limit, enforce that transport limit as well. Custom filters, values, tags, and output implementations are responsible for enforcing equivalent limits for work they perform outside Fluid's built-in operations.
