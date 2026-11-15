# Renders Liquid templates with the Liquid gem (https://github.com/Shopify/liquid), the reference implementation.
# Built and run by liquid-gem.cs: `dotnet run scripts/liquid-gem/liquid-gem.cs -- --help`.

FROM ruby:3.4-slim

ARG LIQUID_VERSION=5.14.0

RUN gem install liquid --version "${LIQUID_VERSION}" --no-document

COPY render.rb /liquid/render.rb

ENV TZ=UTC

ENTRYPOINT ["ruby", "/liquid/render.rb"]
