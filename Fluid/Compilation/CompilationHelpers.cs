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

        result = context.CreateCompilationResult();
        result.Append($@"await {caller}.WriteToAsync({context.TextWriter}, {context.TextEncoder}, {context.TemplateContext});");

        return result;
    }

    /// <summary>
    /// Compiles an expression even if it doesn't support compilation natively.
    /// </summary>
    public static CompilationResult CompileExpression(Expression expression, string caller, CompilationContext context)
    {
        CompilationResult result = null;

        if (expression is ICompilable compilableExpression)
        {
            var previousCaller = context.Caller;
            context.Caller = caller;

            result = compilableExpression.Compile(context);

            context.Caller = previousCaller;
        }
        
        // The result can be null if the expression doesn't implement ICompilable
        // or of the result of the compilation is null;

        // Constant expression don't need to implement ICompilable since the expression
        // can be cached and the interpreted evaluation will only be executed once

        if (result == null)
        {
            result = context.CreateCompilationResult();
            context.DeclareExpressionResult(result);
            result.Append($@"{result.Result} = await {caller}.EvaluateAsync({context.TemplateContext});");
        }

        // If the expression is constant, we can execute the code only once
        // The expressions might still be executed from than once (thrundering herd)
        // since there is no locking on the _initialized member but this is totally acceptable.

        if (expression.IsConstantExpression())
        {
            var member = $"_value{context.NextNumber}";

            context.GlobalMembers.Add($"private FluidValue {member} = NilValue.Instance;");
            var newResult = context.CreateCompilationResult();
            newResult
                .AppendLine("if (!_initialized)")
                .AppendLine("{")
                .Indent().AppendLine(result.ToString())
                .AppendLine($"{member} = {result.Result};")
                .AppendLine("}");

            newResult.Result = member;
            result = newResult;
        }

        return result;
    }

    public static string DeclareCompletionVariable(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"completion_{context.NextNumber}";
        result.AppendLine($"ValueTask<Completion> {result.Result};");
        return result.Result;
    }

    public static string DeclareExpressionResult(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"eval_{context.NextNumber}";
        result.AppendLine($"FluidValue {result.Result};");
        return result.Result;
    }

    public static string DeclareFluidValueResult(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"value_{context.NextNumber}";
        result.AppendLine($"FluidValue {result.Result};");
        return result.Result;
    }

    public static string DeclareTaskVariable(this CompilationContext context, CompilationResult result)
    {
        result.Result = $"completion_{context.NextNumber}";
        result.AppendLine($"Task {result.Result};");
        return result.Result;
    }

    public static string DeclareCaller(this CompilationContext context, CompilationResult result, string accessor)
    {
        result.Caller = $"caller_{context.NextNumber}";
        result.AppendLine($"var {result.Result} = accessor;");
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