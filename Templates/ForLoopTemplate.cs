﻿using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.Templates
{
  [PostfixTemplate(
    templateName: "for",
    description: "Iterates over collection with index",
    example: "for (var i = 0; i < expr.Length; i++)")]
  public class ForLoopTemplate : ForLoopTemplateBase, IPostfixTemplate
  {
    [NotNull] private readonly LiveTemplatesManager myTemplatesManager;

    public ForLoopTemplate([NotNull] LiveTemplatesManager templatesManager)
    {
      myTemplatesManager = templatesManager;
    }

    public ILookupItem CreateItem(PostfixTemplateContext context)
    {
      string lengthPropertyName;
      if (CreateItems(context, out lengthPropertyName))
      {
        return new ForLookupItem(
          context.InnerExpression, myTemplatesManager, lengthPropertyName);
      }

      return null;
    }

    private sealed class ForLookupItem : ForLookupItemBase
    {
      public ForLookupItem(
        [NotNull] PrefixExpressionContext context,
        [NotNull] LiveTemplatesManager templatesManager,
        [CanBeNull] string lengthPropertyName)
        : base("for", context, templatesManager, lengthPropertyName) { }

      protected override IForStatement CreateStatement(
        CSharpElementFactory factory, ICSharpExpression expression)
      {
        var template = "for(var x=0;x<$0;x++)" + EmbeddedStatementBracesTemplate;
        var forStatement = (IForStatement) factory.CreateStatement(template, expression);

        var condition = (IRelationalExpression) forStatement.Condition;
        if (LengthPropertyName == null)
        {
          condition.RightOperand.ReplaceBy(expression);
        }
        else
        {
          var lengthAccess = factory.CreateReferenceExpression("expr.$0", LengthPropertyName);
          lengthAccess = condition.RightOperand.ReplaceBy(lengthAccess);
          lengthAccess.QualifierExpression.NotNull().ReplaceBy(expression);
        }

        return forStatement;
      }
    }
  }
}