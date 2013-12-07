﻿using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.ControlFlow.PostfixCompletion.LookupItems;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.TemplateProviders
{
  [PostfixTemplateProvider(
    templateName: "while",
    description: "Iterating while boolean statement is 'true'",
    example: "while (expr)", WorksOnTypes = true)]
  public sealed class WhileLoopTemplate : BooleanExpressionProviderBase, IPostfixTemplate
  {
    protected override bool CreateBooleanItems(
      PrefixExpressionContext expression, ICollection<ILookupItem> consumer)
    {
      if (expression.CanBeStatement)
      {
        consumer.Add(new LookupItem(expression));
        return true;
      }

      return false;
    }

    private sealed class LookupItem : KeywordStatementPostfixLookupItem<IWhileStatement>
    {
      public LookupItem([NotNull] PrefixExpressionContext context) : base("while", context) { }

      protected override string Template { get { return "while(expr)"; } }
      protected override void PlaceExpression(
        IWhileStatement statement, ICSharpExpression expression, CSharpElementFactory factory)
      {
        statement.Condition.ReplaceBy(expression);
      }
    }
  }
}