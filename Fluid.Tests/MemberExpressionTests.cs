using System;
using System.Collections.Generic;
using Fluid.Ast;
using Xunit;

namespace Fluid.Tests;

public class MemberExpressionTests
{
    [Fact]
    public void EmptySegmentsReportsParameterNameAndMessage()
    {
        var segments = (IReadOnlyList<MemberSegment>)Array.Empty<MemberSegment>();

        var exception = Assert.Throws<ArgumentException>(() => new MemberExpression(segments));

        Assert.Equal("segments", exception.ParamName);
        Assert.StartsWith("At least one segment is required in a MemberExpression", exception.Message);
    }
}
