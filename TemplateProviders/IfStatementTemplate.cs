﻿using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.ControlFlow.PostfixCompletion.LookupItems;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.TemplateProviders
{
  [PostfixTemplateProvider(
    templateName: "if",
    description: "Checks boolean expression to be 'true'",
    example: "if (expr)", WorksOnTypes = true)]
  public sealed class IfStatementTemplate : BooleanExpressionProviderBase, IPostfixTemplate
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

    private sealed class LookupItem : KeywordStatementPostfixLookupItem<IIfStatement>
    {
      public LookupItem([NotNull] PrefixExpressionContext context) : base("if", context) { }

      protected override string Template { get { return "if(expr)"; } }

      protected override void PlaceExpression(
        IIfStatement statement, ICSharpExpression expression, CSharpElementFactory factory)
      {
        statement.Condition.ReplaceBy(expression);
      }
    }
  }
}