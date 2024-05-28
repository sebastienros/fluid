﻿using System.Text.Encodings.Web;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class IfStatement : TagStatement
    {
        public IfStatement(
            Expression condition,
            IReadOnlyList<Statement> statements,
            ElseStatement elseStatement = null,
            IReadOnlyList<ElseIfStatement> elseIfStatements = null
        ) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
            ElseIfs = elseIfStatements ?? [];
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }
        public IReadOnlyList<ElseIfStatement> ElseIfs { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var conditionTask = Condition.EvaluateAsync(context);
            if (conditionTask.IsCompletedSuccessfully)
            {
                var result = conditionTask.Result.ToBooleanValue();

                if (result)
                {
                    for (var i = 0; i < Statements.Count; i++)
                    {
                        var statement = Statements[i];
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
                    for (var i = 0; i < ElseIfs.Count; i++)
                    {
                        var elseIf = ElseIfs[i];
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
                var completion = await incompleteStatementTask;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }

                for (var i = statementStartIndex; i < Statements.Count; i++)
                {
                    var statement = Statements[i];
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
            var condition = (await conditionTask).ToBooleanValue();
            if (condition)
            {
                return await (elseIfTask ?? elseIf.WriteToAsync(writer, encoder, context));
            }

            for (var i = startIndex; i < ElseIfs.Count; i++)
            {
                elseIf = ElseIfs[i];
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

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIfStatement(this);
    }
}