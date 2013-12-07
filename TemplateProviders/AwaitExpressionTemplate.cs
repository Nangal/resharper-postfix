﻿using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.ControlFlow.PostfixCompletion.LookupItems;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.TemplateProviders
{
  [PostfixTemplateProvider(
    templateName: "await",
    description: "Awaits expressions of 'Task' type",
    example: "await expr")]
  public class AwaitExpressionTemplate : IPostfixTemplate
  {
    public void CreateItems(PostfixTemplateAcceptanceContext context, ICollection<ILookupItem> consumer)
    {
      var exprContext = context.InnerExpression;
      var function = context.ContainingFunction;
      if (function == null) return;

      if (!context.ForceMode)
      {
        if (!function.IsAsync) return;

        var expressionType = exprContext.Type;
        if (!expressionType.IsUnknown)
        {
          if (!(expressionType.IsTask() ||
                expressionType.IsGenericTask())) return;
        }
      }

      // check expression is not already awaited
      var awaitExpression = AwaitExpressionNavigator.GetByTask(
        (context.PostfixReferenceNode as IReferenceExpression)
        .GetContainingParenthesizedExpression() as IUnaryExpression);

      if (awaitExpression == null)
        consumer.Add(new LookupItem(exprContext));
    }

    private sealed class LookupItem : ExpressionPostfixLookupItem<IAwaitExpression>
    {
      public LookupItem([NotNull] PrefixExpressionContext context)
        : base("await", context) { }

      protected override IAwaitExpression CreateExpression(
        CSharpElementFactory factory, ICSharpExpression expression)
      {
        return (IAwaitExpression) factory.CreateExpression("await $0", expression);
      }
    }
  }
}