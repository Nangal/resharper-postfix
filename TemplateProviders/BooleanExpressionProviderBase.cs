﻿using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;

// todo: disable in cases like typeReference.Name == NotNullAttribute.if

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.TemplateProviders
{
  public abstract class BooleanExpressionProviderBase
  {
    public void CreateItems(
      PostfixTemplateAcceptanceContext context, ICollection<ILookupItem> consumer)
    {
      foreach (var expressionContext in context.Expressions)
      {
        if (expressionContext.Type.IsBool() ||
          IsBooleanExpression(expressionContext.Expression))
        {
          if (CreateBooleanItems(expressionContext, consumer)) return;
        }
      }

      if (context.ForceMode)
      {
        foreach (var expressionContext in context.Expressions)
        {
          if (CreateBooleanItems(expressionContext, consumer)) return;
        }
      }
    }

    private static bool IsBooleanExpression([CanBeNull] ICSharpExpression expression)
    {
      return expression is IRelationalExpression
          || expression is IEqualityExpression
          || expression is IConditionalAndExpression
          || expression is IConditionalOrExpression
          || expression is IUnaryOperatorExpression // TODO: check with +expr and other non-boolean unary
          || expression is IIsExpression;
    }

    protected abstract bool CreateBooleanItems(
      [NotNull] PrefixExpressionContext expression,
      [NotNull] ICollection<ILookupItem> consumer);
  }
}
