# Fluid.Core.Tool

A native AOT command line tool (`liquid`) to render [Liquid](https://shopify.github.io/liquid/) templates with [Fluid](https://github.com/sebastienros/fluid).

```shell
dotnet tool install -g Fluid.Core.Tool
echo '{"name":"World"}' | liquid -e "Hello {{ name }}!"
```

Run `liquid --help` for all options.
