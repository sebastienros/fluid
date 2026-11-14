# Fluid documentation site

The documentation published at https://sebastienros.github.io/fluid/, built by
[Douze](https://sebastienros.github.io/douze/) from this folder. The playground, at `/fluid/playground/`, is the
`Fluid.Playground` project.

## Build and preview

Install Douze (it is a preview, published on every push to its `main` branch):

```shell
dotnet tool install -g douze --prerelease --add-source https://f.feedz.io/sebastienros/douze/nuget/index.json
```

From the root of the repository:

```shell
douze serve -s docs                          # http://localhost:8080/fluid/, rebuilt on every change
douze -s docs                                # writes docs/_site; the site is docs/_site/fluid
dotnet run --project Fluid.Playground        # the playground, at the root of its own server
```

The deployment (`.github/workflows/docs.yml`) builds the site, publishes the playground into
`docs/_site/fluid/playground/`, and uploads `docs/_site/fluid`.

## Layout

| Path | Content |
| --- | --- |
| `douze.yml` | The build workflow: front matter, Markdown, headings, highlighting, layouts, link checks and search |
| `_content/fluid/` | The pages, served under `/fluid/` like GitHub Pages does |
| `_content/fluid/guide/` | The guide, one Markdown file per page |
| `_content/fluid/assets/` | Styles, scripts, the logo and the benchmark charts (written by `Fluid.Benchmarks/Charts/generate_charts.py`) |
| `_content/fluid/playground/` | A placeholder replaced by the published playground |
| `_data/sidebar.yml` | The navigation |
| `_includes/` | The layouts |

A page is a Markdown file with a `title` and a `description` in its front matter. Its content is not Liquid, so that
Liquid samples, inline code included, are written as they are. Link to other pages with relative links to their `.md`
files: Douze turns them into URLs and fails the build when a link or an anchor is broken.

The theme is derived from the documentation site of Douze (MIT).
