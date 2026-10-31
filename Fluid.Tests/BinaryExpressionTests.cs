using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class BinaryExpressionTests
{
    private static FluidParser _parser = new FluidParser();

    private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
    {
        _parser.TryParse("{% if " + source + " %}true{% else %}false{% endif %}", out var template, out var messages);

        var context = new TemplateContext();
        init?.Invoke(context);

        var result = await template.RenderAsync(context);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1 == 1", "true")]
    [InlineData("1 == 2", "false")]
    public Task EqualBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("1 != 1", "false")]
    [InlineData("1 != 2", "true")]
    [InlineData("1 <> 1", "false")]
    [InlineData("1 <> 2", "true")]
    public Task NotEqualBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("true and true", "true")]
    [InlineData("true and false", "false")]
    [InlineData("false and false", "false")]
    public Task AndBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("true or true", "true")]
    [InlineData("true or false", "true")]
    [InlineData("false or false", "false")]
    public Task OrBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("'abc' contains 'a'", "true")]
    [InlineData("'abc' contains 'd'", "false")]
    [InlineData("x contains 'a'", "true")]
    [InlineData("x contains 'x'", "false")]
    [InlineData("y contains 'a'", "true")]
    [InlineData("y contains 'x'", "false")]
    public Task ContainsBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected, context =>
        {
            context.SetValue("x", new[] { "a", "b", "c" });
            context.SetValue("y", new List<string> { "a", "b", "c" });
        });
    }

    [Theory]
    [InlineData("'Strasse' contains 'ß'", "false")]
    [InlineData("'ss' == 'ß'", "false")]
    public Task StringComparisonsShouldBeOrdinal(string source, string expected)
    {
        return CheckAsync(source, expected, context =>
        {
            // This is the default, but forcing it for clarity of the test
            // With invariant culture, Strasse and Straße are equal, and Liquid 
            // should not assume they are 
            context.CultureInfo = CultureInfo.InvariantCulture;
        });
    }

    [Theory]
    [InlineData("10 < 10", "false")]
    [InlineData("10 <= 10", "true")]
    public Task LowerThanBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("10 > 10", "false")]
    [InlineData("10 >= 10", "true")]
    public Task GreaterThanBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("'abc' startswith 'bc'", "false")]
    [InlineData("'abc' startswith 'ab'", "true")]
    [InlineData("x startswith 'b'", "false")]
    [InlineData("x startswith 'a'", "true")]
    [InlineData("y startswith 2", "false")]
    [InlineData("y startswith 1", "true")]
    [InlineData("z startswith 'a'", "false")]
    public Task StartsWithBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected, context =>
        {
            context.SetValue("x", new[] { "a", "b", "c" });
            context.SetValue("y", new[] { 1, 2, 3 });
            context.SetValue("z", new string[0]);
        });
    }

    [Theory]
    [InlineData("'abc' endswith 'ab'", "false")]
    [InlineData("'abc' endswith 'bc'", "true")]
    [InlineData("x endswith 'b'", "false")]
    [InlineData("x endswith 'c'", "true")]
    [InlineData("y endswith 2", "false")]
    [InlineData("y endswith 3", "true")]
    [InlineData("z endswith 'a'", "false")]
    public Task EndsWithBinaryExpressionIsEvaluated(string source, string expected)
    {
        return CheckAsync(source, expected, context =>
        {
            context.SetValue("x", new[] { "a", "b", "c" });
            context.SetValue("y", new[] { 1, 2, 3 });
            context.SetValue("z", new string[0]);
        });
    }

    [Theory]
    [InlineData("'' == empty", "true")]
    [InlineData("'a' == empty", "false")]
    [InlineData("x == empty", "true")]
    [InlineData("y == empty", "false")]
    public Task EmptyValue(string source, string expected)
    {
        return CheckAsync(source, expected, context =>
        {
            context.SetValue("x", new string[0]);
            context.SetValue("y", new List<string> { "foo" });
        });
    }

    [Theory]
    [InlineData("''", "true")]
    [InlineData("'a'", "true")]
    [InlineData("blank", "true")]
    [InlineData("empty", "true")]
    [InlineData("0", "true")]
    [InlineData("1", "true")]
    [InlineData("abc", "false")]
    [InlineData("false", "false")]
    public Task TruthyFalsy(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("true == true", "true", null)]
    [InlineData("true != true", "false", null)]
    [InlineData("0 > 0", "false", null)]
    [InlineData("1 > 0", "true", null)]
    [InlineData("0 < 1", "true", null)]
    [InlineData("0 <= 0", "true", null)]
    [InlineData("null <= 0", "false", null)]
    [InlineData("0 <= null", "false", null)]
    [InlineData("0 >= 0", "true", null)]
    [InlineData("'test' == 'test'", "true", null)]
    [InlineData("'test' != 'test'", "false", null)]
    [InlineData("var == 'hello there!'", "true", "hello there!")]
    [InlineData("'hello there!' == var", "true", "hello there!")]
    [InlineData("'hello there!' == true", "false", "hello there!")]
    [InlineData("'hello there!' == false", "false", "hello there!")]
    [InlineData("null <= null", "false", null)]
    [InlineData("null >= null", "false", null)]
    [InlineData("null < null", "false", null)]
    [InlineData("null > null", "false", null)]
    [InlineData("null == null", "true", null)]
    [InlineData("null != null", "false", null)]
    public Task StringLiteralTrue(string source, string expected, object value)
    {
        // https://github.com/Shopify/liquid/blob/master/test/integration/tags/statements_test.rb
        return CheckAsync(source, expected, t => t.SetValue("var", value));
    }

    [Theory]
    // Like Liquid, nil and values that aren't numbers or strings are never ordered.
    [InlineData("{% if missing >= missing2 %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if n <= missing %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 1 > (2..3) %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 1 <= (2..3) %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 'x' > a %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if '' < a %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if a > 1 %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 1 < h %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 1 < true %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if empty >= nil %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if 2 >= 1 and 'b' > 'a' %}y{% else %}n{% endif %}", "y")]
    // An empty collection is equal to empty and blank, but not to nil.
    [InlineData("{% if e == nil %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if eh == nil %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if e == missing %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if e != nil %}y{% else %}n{% endif %}", "y")]
    [InlineData("{% if (5..3) == nil %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% case e %}{% when nil %}y{% else %}n{% endcase %}", "n")]
    [InlineData("{% if e == empty and e == blank and eh == empty and eh == blank %}y{% else %}n{% endif %}", "y")]
    // Like Liquid, a range never responds to empty? or blank?.
    [InlineData("{% if (5..3) == empty %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if (5..3) == blank or empty == (5..3) %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% case (5..3) %}{% when empty %}y{% else %}n{% endcase %}{{ (5..3) | default: 'D' }}", "n5..3")]
    [InlineData("{% if nil == nil and missing == nil %}y{% else %}n{% endif %}", "y")]
    // Like Ruby's Range#==, a range is only equal to a range with the same bounds.
    [InlineData("{% assign s = (1..3) | sort %}{% if s == (1..3) %}eq{% else %}ne{% endif %}|{% if (3..1) == e %}eq{% else %}ne{% endif %}|{% case (3..1) %}{% when e %}w{% endcase %}|{% if e != (3..1) %}NE{% endif %}", "ne|ne||NE")]
    [InlineData("{% if (1..-3) == (5..3) %}eq{% else %}ne{% endif %}|{% if (1..2) == (1..2) %}eq{% else %}ne{% endif %}|{% case (1..-3) %}{% when (5..3) %}w{% endcase %}", "ne|eq|")]
    // Like Liquid, contains blank or empty looks for '' in a string, blank and empty are never equal to
    // each other or themselves, and case blank is case ''.
    [InlineData("{% if '' contains blank %}a{% endif %}{% if 'x' contains empty %}b{% endif %}{% if a contains blank %}c{% endif %}{% if n contains blank %}d{% endif %}{% assign x = blank %}{% if x contains blank %}e{% endif %}", "abe")]
    [InlineData("{% if blank == blank %}a{% endif %}{% if empty == empty %}b{% endif %}{% if empty != empty %}c{% endif %}{% if blank != empty %}d{% endif %}{% assign x = blank %}{% if x == blank %}e{% endif %}", "cde")]
    [InlineData("{% case blank %}{% when n %}a{% endcase %}{% case blank %}{% when '' %}b{% endcase %}{% case empty %}{% when nil %}c{% endcase %}{% case n %}{% when blank %}d{% endcase %}{% if blank == n %}e{% endif %}", "bde")]
    public async Task ComparesLikeLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("a", new[] { 1, 2, 3 });
        context.SetValue("h", new Dictionary<string, object> { ["k"] = 1 });
        context.SetValue("e", Array.Empty<int>());
        context.SetValue("eh", new Dictionary<string, object>());
        context.SetValue("n", NilValue.Instance);

        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(context));
    }

    [Theory]
    [InlineData("{% if 1 < 'a' %}{% endif %}", "comparison of Integer with String failed")]
    [InlineData("{% if 1 >= 'a' %}{% endif %}", "comparison of Integer with String failed")]
    [InlineData("{% if 'a' > 1 %}{% endif %}", "comparison of String with Integer failed")]
    [InlineData("{% if 'a' <= 1 %}{% endif %}", "comparison of String with Integer failed")]
    public async Task ComparingNumbersWithStringsFails(string source, string message)
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => _parser.Parse(source).RenderAsync(new TemplateContext()).AsTask());
        Assert.Equal(message, exception.Message);
    }

    [Theory]
    // Like Liquid, the right operand isn't evaluated when the left one decides the result.
    [InlineData("{% if true or 1 < 'a' %}y{% else %}n{% endif %}", "y")]
    [InlineData("{% if false and 1 < 'a' %}y{% else %}n{% endif %}", "n")]
    [InlineData("{% if true or false and 1 < 'a' %}y{% else %}n{% endif %}", "y")]
    [InlineData("{% if false or true or 1 < 'a' %}y{% else %}n{% endif %}", "y")]
    [InlineData("{% unless true or 1 < 'a' %}y{% else %}n{% endunless %}", "n")]
    [InlineData("{% if false or true and true %}y{% else %}n{% endif %}", "y")]
    [InlineData("{% if true and false or true %}y{% else %}n{% endif %}", "y")]
    public async Task AndOrShortCircuit(string source, string expected)
    {
        Assert.Equal(expected, await _parser.Parse(source).RenderAsync(new TemplateContext()));
    }

    [Fact]
    public async Task AndOrEvaluateTheLeftOperandFirst()
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => _parser.Parse("{% if 1 < 'a' or true %}{% endif %}").RenderAsync(new TemplateContext()).AsTask());
        Assert.Equal("comparison of Integer with String failed", exception.Message);
    }

    [Theory]
    [InlineData("true or false and false", "true")]
    [InlineData("true and false and false or true", "false")]
    [InlineData("true and true and false == false", "true")]
    [InlineData("true and true and true == false", "false")]
    [InlineData("false or false or true == true", "true")]
    public Task OperatorsShouldBeEvaluatedFromRightToLeft(string source, string expected)
    {
        // https://shopify.github.io/liquid/basics/operators/
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("1 == 1 or 1 == 2 and 1 == 2", "true")]
    [InlineData("1 == 1 and 1 == 2 and 1 == 2 or 1 == 1", "false")]
    public Task OperatorsHavePriority(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Fact]
    public void DictionaryValuesShouldBeEqual()
    {
        var actual = DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(new Dictionary<string, FluidValue>
        {
            { "stringProperty", StringValue.Create("testValue") }
        }));

        var expected = DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(new Dictionary<string, FluidValue>
        {
            { "stringProperty", StringValue.Create("testValue") }
        }));

        Assert.True(actual.Equals(expected));
    }

    [Fact]
    public void ArrayValuesShouldBeEqual()
    {
        var actual = ArrayValue.Create(new FluidValue[]
        {
            StringValue.Create("testValue")
        });

        var expected = ArrayValue.Create(new FluidValue[]
        {
            StringValue.Create("testValue")
        });


        Assert.True(actual.Equals(expected));
    }

    [Theory]
    [InlineData("{{ 2 == 3 }}", "2")]
    [InlineData("{{ 5 == 5 }}", "5")]
    [InlineData("{{ 10 != 5 }}", "10")]
    [InlineData("{{ 10 > 5 }}", "10")]
    [InlineData("{{ 3 < 5 }}", "3")]
    [InlineData("{{ 10 >= 5 }}", "10")]
    [InlineData("{{ 3 <= 5 }}", "3")]
    [InlineData("{{ true and false }}", "true")]
    [InlineData("{{ true or false }}", "true")]
    [InlineData("{{ 'abc' contains 'a' }}", "abc")]
    [InlineData("{{ 'abc' startswith 'a' }}", "abc")]
    [InlineData("{{ 'abc' endswith 'c' }}", "abc")]
    public async Task BinaryExpressionsReturnLeftOperand(string source, string expected)
    {
        // Binary expressions should return the left operand according to Liquid standard
        _parser.TryParse(source, out var template, out var messages);
        var result = await template.RenderAsync();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{{ 2 == 3 | plus: 10 | minus: 3 }}", "9")]
    [InlineData("{{ 5 == 5 | plus: 3 }}", "8")]
    [InlineData("{{ 10 > 5 | minus: 2 }}", "8")]
    public async Task BinaryExpressionsWithFilters(string source, string expected)
    {
        // Binary expressions return left operand which can then be filtered
        _parser.TryParse(source, out var template, out var messages);
        var result = await template.RenderAsync();
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ObjectValuesShouldCompareByValueNotReference()
    {
        // Create a simple class that overrides Equals
        var obj1 = new TestObject { Value = "test" };
        var obj2 = new TestObject { Value = "test" };

        _parser.TryParse("{% if obj1 == obj2 %}equal{% else %}not equal{% endif %}", out var template, out var messages);

        var context = new TemplateContext();
        context.SetValue("obj1", obj1);
        context.SetValue("obj2", obj2);

        var result = await template.RenderAsync(context);

        // obj1.Equals(obj2) returns true, so the comparison should return "equal"
        Assert.Equal("equal", result);
    }

    [Fact]
    public async Task ArrayContainsShouldCompareByValueNotReference()
    {
        var obj1 = new TestObject { Value = "test" };
        var obj2 = new TestObject { Value = "test" };
        var array = new[] { obj1 };

        _parser.TryParse("{% if array contains obj2 %}found{% else %}not found{% endif %}", out var template, out var messages);

        var context = new TemplateContext();
        context.SetValue("array", array);
        context.SetValue("obj2", obj2);

        var result = await template.RenderAsync(context);

        // obj1.Equals(obj2) returns true, so contains should return "found"
        Assert.Equal("found", result);
    }

    private class TestObject
    {
        public string Value { get; set; }

        public override bool Equals(object obj)
        {
            return obj is TestObject other && Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }
    }

    [Theory]
    [InlineData("'1' <= '1'", "true")]
    [InlineData("'1' <= '2'", "true")]
    [InlineData("'2' <= '1'", "false")]
    [InlineData("'a' <= 'b'", "true")]
    [InlineData("'b' <= 'a'", "false")]
    [InlineData("'ab' <= 'a'", "false")]
    [InlineData("'abc' <= 'ab'", "false")]
    [InlineData("'abc' <= 'abd'", "true")]
    [InlineData("'ab' <= 'abd'", "true")]
    public Task CompareString(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("'1' == '1'", "true")]
    [InlineData("'1' == '01'", "false")]
    [InlineData("'1' != '1'", "false")]
    [InlineData("'1' != '01'", "true")]
    [InlineData("'1' == 1", "false")]
    [InlineData("'1' != 1", "true")]
    [InlineData("'1' == true", "false")]
    [InlineData("'1' != false", "true")]
    [InlineData("'1' == false", "false")]
    [InlineData("'0' == true", "false")]
    [InlineData("'0' == false", "false")]
    public Task StringEquality(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("1 == 1", "true")]
    [InlineData("1 != 1", "false")]
    [InlineData("1 == 01", "true")]
    [InlineData("1 == '1'", "false")]
    [InlineData("1 != '1'", "true")]
    [InlineData("1 == true", "false")]
    [InlineData("1 != true", "true")]
    [InlineData("1 == false", "false")]
    [InlineData("0 == true", "false")]
    [InlineData("0 == false", "false")]
    public Task NumberEquality(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Theory]
    [InlineData("true == true", "true")]
    [InlineData("false == false", "true")]
    [InlineData("true == false", "false")]
    [InlineData("true != true", "false")]
    [InlineData("false != false", "false")]
    [InlineData("true != false", "true")]
    [InlineData("true == '1'", "false")]
    [InlineData("true == 1", "false")]
    [InlineData("false == '1'", "false")]
    [InlineData("false == 1", "false")]
    [InlineData("true == null", "false")]
    [InlineData("true != null", "true")]
    [InlineData("false == null", "false")]
    [InlineData("false != null", "true")]
    [InlineData("null == true", "false")]
    [InlineData("null != true", "true")]
    [InlineData("null == false", "false")]
    [InlineData("null != false", "true")]
    [InlineData("true == nil", "false")]
    [InlineData("true != nil", "true")]
    [InlineData("false == nil", "false")]
    [InlineData("false != nil", "true")]
    [InlineData("nil == true", "false")]
    [InlineData("nil != true", "true")]
    [InlineData("nil == false", "false")]
    [InlineData("nil != false", "true")]
    public Task BooleanEquality(string source, string expected)
    {
        return CheckAsync(source, expected);
    }

    [Fact]
    public async Task ContainsShouldSupportAsyncWithContext()
    {
        // Create a custom FluidValue that uses ContainsAsync with TemplateContext
        var customValue = new CustomAsyncContainsValue(new[] { "a", "b", "c" });

        _parser.TryParse("{% if custom contains 'b' %}found{% else %}not found{% endif %}", out var template, out var messages);

        var context = new TemplateContext();
        context.SetValue("custom", customValue);

        var result = await template.RenderAsync(context);

        // The custom value should use ContainsAsync and find 'b'
        Assert.Equal("found", result);
    }

    private class CustomAsyncContainsValue : FluidValue
    {
        private readonly string[] _values;

        public CustomAsyncContainsValue(string[] values)
        {
            _values = values;
        }

        public override FluidValues Type => FluidValues.Array;

        public override bool Equals(FluidValue other) => false;

        public override bool ToBooleanValue(TemplateContext context) => true;

        public override decimal ToNumberValue(TemplateContext context) => _values.Length;

        public override string ToStringValue(TemplateContext context) => string.Join(",", _values);

        public override object ToObjectValue(TemplateContext context) => _values;

        public override async ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
        {
            // Simulate async operation
            await Task.Delay(1);
            var searchValue = value.ToStringValue(context);
            return Array.Exists(_values, v => v == searchValue);
        }
    }

}
