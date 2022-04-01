using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IfStatement : TagStatement
    {
        private readonly List<ElseIfStatement> _elseIfStatements;

        public IfStatement(
            Expression condition,
            List<Statement> statements,
            ElseStatement elseStatement = null,
            List<ElseIfStatement> elseIfStatements = null
        ) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
            _elseIfStatements = elseIfStatements ?? new List<ElseIfStatement>();
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public IReadOnlyList<ElseIfStatement> ElseIfs => _elseIfStatements;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var conditionTask = Condition.EvaluateAsync(context);
            if (conditionTask.IsCompletedSuccessfully)
            {
                var result = conditionTask.Result.ToBooleanValue();

                if (result)
                {
                    for (var i = 0; i < _statements.Count; i++)
                    {
                        var statement = _statements[i];
                        var task = statement.WriteToAsync(writer, encoder, context);
                        if (!task.IsCompletedSuccessfully)
                        {
                            return Awaited(conditionTask, task, writer, encoder, context, i + 1);
                        }

                        var completion = task.Result;

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            // We return the completion to flow it to the outer loop
                            return new ValueTask<Completion>(completion);
                        }
                    }

                    return new ValueTask<Completion>(Completion.Normal);
                }
                else
                {
                    for (var i = 0; i < _elseIfStatements.Count; i++)
                    {
                        var elseIf = _elseIfStatements[i];
                        var elseIfConditionTask = elseIf.Condition.EvaluateAsync(context);
                        if (!elseIfConditionTask.IsCompletedSuccessfully)
                        {
                            return AwaitedElseBranch(elseIf, elseIfConditionTask, elseIfTask: null, writer, encoder, context, i + 1);
                        }

                        if (elseIfConditionTask.Result.ToBooleanValue())
                        {
                            var writeTask = elseIf.WriteToAsync(writer, encoder, context);
                            if (!writeTask.IsCompletedSuccessfully)
                            {
                                return AwaitedElseBranch(elseIf, elseIfConditionTask, writeTask, writer, encoder, context, i + 1);
                            }

                            return new ValueTask<Completion>(writeTask.Result);
                        }
                    }

                    if (Else != null)
                    {
                        return Else.WriteToAsync(writer, encoder, context);
                    }
                }

                return new ValueTask<Completion>(Completion.Normal);
            }
            else
            {
                return Awaited(
                    conditionTask,
                    incompleteStatementTask: new ValueTask<Completion>(Completion.Normal), // normal won't change processing
                    writer,
                    encoder,
                    context,
                    statementStartIndex: 0);
            }
        }


        private async ValueTask<Completion> Awaited(
            ValueTask<FluidValue> conditionTask,
            ValueTask<Completion> incompleteStatementTask,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext context,
            int statementStartIndex)
        {
            var result = (await conditionTask).ToBooleanValue();

            if (result)
            {
                var completion =  await incompleteStatementTask;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }

                for (var i = statementStartIndex; i < _statements.Count; i++)
                {
                    var statement = _statements[i];
                    completion = await statement.WriteToAsync(writer, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
                    }
                }

                return Completion.Normal;
            }
            else
            {
                await AwaitedElseBranch(null, new ValueTask<FluidValue>(BooleanValue.False), new ValueTask<Completion>(), writer, encoder, context, startIndex: 0);
            }

            return Completion.Normal;
        }

        private async ValueTask<Completion> AwaitedElseBranch(
            ElseIfStatement elseIf,
            ValueTask<FluidValue> conditionTask,
            ValueTask<Completion>? elseIfTask,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext context,
            int startIndex)
        {
            bool condition = (await conditionTask).ToBooleanValue();
            if (condition)
            {
                return await (elseIfTask ?? elseIf.WriteToAsync(writer, encoder, context));
            }

            for (var i = startIndex; i < _elseIfStatements.Count; i++)
            {
                elseIf = _elseIfStatements[i];
                if ((await elseIf.Condition.EvaluateAsync(context)).ToBooleanValue())
                {
                    return await elseIf.WriteToAsync(writer, encoder, context);
                }
            }

            if (Else != null)
            {
                return await Else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }
    }
}