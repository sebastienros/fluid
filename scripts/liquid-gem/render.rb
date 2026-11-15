# Renders a Liquid template with the Liquid gem. The template and its data come from environment variables, so
# that no quoting is needed on the command line of any shell:
#
#   LIQUID_TEMPLATE    the template source (required)
#   LIQUID_DATA        a JSON object, the variables of the template (optional)
#   LIQUID_ERROR_MODE  lax, warn or strict (default: strict)
#   LIQUID_FORMAT      raw (default) or json
#   TZ                 the time zone of the process, like America/New_York (default: UTC)
#
# In the data, {"$time": "2026-03-04T13:00:00"} is a Time in the time zone of the process, or with the offset it
# has ("...-05:00", or "...Z" for Time.utc), and {"$date": "2026-03-04"} is a Date.
#
# Raw output is written as is to stdout. Exit codes: 0 rendered, 2 syntax error, 4 render error, 3 usage error.

require "json"
require "date"
require "time"
require "liquid"

def to_liquid_data(value)
  case value
  when Hash
    if value.size == 1 && value.key?("$time")
      Time.iso8601(value["$time"])
    elsif value.size == 1 && value.key?("$date")
      Date.iso8601(value["$date"])
    else
      value.transform_values { |v| to_liquid_data(v) }
    end
  when Array
    value.map { |v| to_liquid_data(v) }
  else
    value
  end
end

def finish(format, output: nil, error: nil, error_type: nil, code: 0)
  if format == "json"
    # ASCII, so that no shell or console encoding can change it.
    puts JSON.generate({
      "output" => output,
      "error" => error,
      "errorType" => error_type,
      "liquid" => Liquid::VERSION,
      "ruby" => RUBY_VERSION,
    }, ascii_only: true)
  elsif error
    warn error
  else
    $stdout.write(output)
  end

  exit code
end

format = ENV.fetch("LIQUID_FORMAT", "raw")
source = ENV["LIQUID_TEMPLATE"]
finish(format, error: "LIQUID_TEMPLATE is not set", error_type: "usage", code: 3) if source.nil?

begin
  data = ENV["LIQUID_DATA"].to_s.strip.empty? ? {} : to_liquid_data(JSON.parse(ENV["LIQUID_DATA"]))
rescue JSON::ParserError, ArgumentError, Date::Error => e
  finish(format, error: "Invalid data: #{e.message}", error_type: "usage", code: 3)
end

finish(format, error: "The data must be a JSON object", error_type: "usage", code: 3) unless data.is_a?(Hash)

error_mode = ENV.fetch("LIQUID_ERROR_MODE", "strict").to_sym

begin
  template = Liquid::Template.parse(source, error_mode: error_mode)
rescue Liquid::SyntaxError => e
  finish(format, error: e.message, error_type: "syntax", code: 2)
end

begin
  output = template.render!(data)
rescue Liquid::Error => e
  # Like Fluid's exceptions, without the "Liquid error: " prefix of the rendered message.
  finish(format, error: e.message.sub(/\ALiquid (?:error|syntax error): /, ""), error_type: "render", code: 4)
rescue StandardError => e
  finish(format, error: "#{e.class}: #{e.message}", error_type: "render", code: 4)
end

finish(format, output: output)
