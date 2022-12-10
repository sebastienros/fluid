using Fluid.Ast;

namespace Fluid.Compilation;

public static class CompilationHelpers
{
    /// <summary>
    /// Compiles a statement even if it doesn't support compilation natively.
    /// </summary>
    /// <remarks>
    /// This method is called from ICompilable.Compile implementations that need to invoke other statements.
    /// The result is a CompilationResult that is either generated code or a wrapper to the interpreted logic
    /// of the statement.
    /// </remarks>
    public static CompilationResult CompileStatement(Statement statement, string caller, CompilationContext context)
    {
        CompilationResult result;

        if (statement is ICompilable compilableStatement)
        {

            // If the result of call ICompilable.Compile on a Statement is null
            // we assume that this statements can't be compiled, so we wrap
            // the interpreted method instead, like it was not implementing ICompilable.
            // That can be useful when some statements are too hard to compiled in some
            // cases, and the compile path only want to handle simple cases.

            var previousCaller = context.Caller;
            context.Caller = caller;
            
            result = compilableStatement.Compile(context);
            
            context.Caller = previousCaller;

            if (result != null)
            {
                return result;
            }
        }

        result = new CompilationResult();
        result.StringBuilder.Append($@"await {caller}.WriteToAsync({context.TextWriter}, {context.TextEncoder}, {context.TemplateContext});");

        return result;
    }

    /// <summary>
    /// Compiles an expression even if it doesn't support compilation natively.
    /// </summary>
    public static CompilationResult CompileExpression(Expression expression, string caller, CompilationContext context)
    {
        CompilationResult result;

        if (expression is ICompilable compilableExpression)
        {
            var previousCaller = context.Caller;
            context.Caller = caller;

            result = compilableExpression.Compile(context);

            context.Caller = previousCaller;

            if (result != null)
            {
                return result;
            }
        }

        result = new CompilationResult();
        context.DeclareExpressionResult(result);
        result.StringBuilder.Append($@"{result.Result} = await {caller}.EvaluateAsync({context.TemplateContext});");

        return result;
    }

    public static string DeclareCompletionVariable(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"completion_{context.NextNumber}";
        result.StringBuilder.AppendLine($"ValueTask<Completion> {result.Result};");
        return result.Result;
    }

    public static string DeclareExpressionResult(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"eval_{context.NextNumber}";
        result.StringBuilder.AppendLine($"FluidValue {result.Result};");
        return result.Result;
    }

    public static string DeclareFluidValueResult(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"value_{context.NextNumber}";
        result.StringBuilder.AppendLine($"FluidValue {result.Result};");
        return result.Result;
    }

    public static string DeclareTaskVariable(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"completion_{context.NextNumber}";
        result.StringBuilder.AppendLine($"Task {result.Result};");
        return result.Result;
    }

    public static string DeclareCaller(this CompilationContext context, CompilationResult result, string accessor)
    {
        result.Caller = $"caller_{context.NextNumber}";
        result.StringBuilder.AppendLine($"var {result.Result} = accessor;");
        return result.Caller;
    }

    //public static ParameterExpression DeclareTemplateResult(this CompilationContext context, CompilationResult result)
    //{
    //    result.Result = Expression.Variable(typeof(ValueTask), $"result{context.NextNumber}");
    //    result.Variables.Add(result.Result);
    //    // Structs should not be initialized to their default value
    //    //result.Body.Add(Expression.Assign(result.Result, CompilationHelpers.ValueTask_Completed()));
    //    return result.Result;
    //}

    public static void IncrementSteps(this CompilationContext context)
    {
        if (context.Options.MaxSteps > 0)
        {
            // TODO: Generate this code

            //if (context.TemplateContext._steps++ > maxSteps)
            //{
            //    ExceptionHelper.ThrowMaximumRecursionException();
            //}
        }
    }
}