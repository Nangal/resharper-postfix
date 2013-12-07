﻿using System.Collections.Generic;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.TemplateProviders
{
  [PostfixTemplateProvider(
    templateName: "notnull",
    description: "Checks expression to be not-null",
    example: "if (expr != null)")]
  public class CheckForNullTemplate : CheckForNullTemplateProviderBase, IPostfixTemplate
  {
    public void CreateItems(PostfixTemplateAcceptanceContext context, ICollection<ILookupItem> consumer)
    {
      var exprContext = context.OuterExpression;
      if (!exprContext.CanBeStatement) return;

      if (!context.ForceMode)
      {
        if (exprContext.Type.IsUnknown) return;
        if (!IsNullableType(exprContext.Type)) return;
      }

      var state = CSharpControlFlowNullReferenceState.UNKNOWN;

      if (!context.ForceMode) state = CheckNullabilityState(exprContext);

      switch (state)
      {
        case CSharpControlFlowNullReferenceState.MAY_BE_NULL:
        case CSharpControlFlowNullReferenceState.UNKNOWN:
          consumer.Add(new LookupItem("notNull", exprContext, "if(expr!=null)"));
          break;
      }
    }
  }
}