﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.Templates
{
  [PostfixTemplate(
    templateName: "prop",
    description: "Introduces property for expression",
    example: "Property = expr;")]
  public class IntroducePropertyTemplate : IntroduceMemberTemplateBase
  {
    [NotNull] private readonly LiveTemplatesManager myTemplatesManager;

    public IntroducePropertyTemplate([NotNull] LiveTemplatesManager templatesManager)
    {
      myTemplatesManager = templatesManager;
    }

    protected override IntroduceMemberLookupItem CreateLookupItem(
      PrefixExpressionContext expression, IType expressionType, bool isStatic)
    {
      return new IntroducePropertyLookupItem(expression, myTemplatesManager, isStatic);
    }

    private sealed class IntroducePropertyLookupItem : IntroduceMemberLookupItem
    {
      public IntroducePropertyLookupItem(
        [NotNull] PrefixExpressionContext context,
        [NotNull] LiveTemplatesManager templatesManager, bool isStatic)
        : base("prop", context, templatesManager, context.Type, isStatic) { }

      protected override IClassMemberDeclaration CreateMemberDeclaration(CSharpElementFactory factory)
      {
        var declaration = factory.CreatePropertyDeclaration(ExpressionType, "__");
        declaration.SetAccessRights(AccessRights.PUBLIC);
        var getter = factory.CreateAccessorDeclaration(AccessorKind.GETTER, false);
        var setter = factory.CreateAccessorDeclaration(AccessorKind.SETTER, false);

        declaration.AddAccessorDeclarationAfter(getter, null);
        declaration.AddAccessorDeclarationBefore(setter, null);
        declaration.SetStatic(IsStatic);

        return declaration;
      }

      protected override ICSharpTypeMemberDeclaration GetAnchorMember(IList<ICSharpTypeMemberDeclaration> members)
      {
        var anchor = members.LastOrDefault(m => m.DeclaredElement is IProperty && m.IsStatic == IsStatic) ??
                     members.LastOrDefault(m => m.DeclaredElement is IField && m.IsStatic == IsStatic);
        if (anchor == null && IsStatic)
        {
          return members.LastOrDefault(m => m.DeclaredElement is IProperty) ??
                 members.LastOrDefault(m => m.DeclaredElement is IField);
        }

        return anchor;
      }
    }
  }
}