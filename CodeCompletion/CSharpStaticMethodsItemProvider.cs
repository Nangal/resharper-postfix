﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application.Settings;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Feature.Services.Tips;
using JetBrains.ReSharper.PostfixTemplates.Settings;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExpectedTypes;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve.Filters;
using JetBrains.ReSharper.Psi.Pointers;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.TextControl;
using JetBrains.Util;

namespace JetBrains.ReSharper.PostfixTemplates.CodeCompletion
{
  [Language(typeof(CSharpLanguage))]
  public class CSharpStaticMethodsItemProvider : CSharpItemsProviderBase<CSharpCodeCompletionContext>
  {
    protected override bool IsAvailable(CSharpCodeCompletionContext context)
    {
      var completionType = context.BasicContext.CodeCompletionType;
      return completionType == CodeCompletionType.AutomaticCompletion ||
             completionType == CodeCompletionType.BasicCompletion;
    }

    protected override bool AddLookupItems(CSharpCodeCompletionContext context,
                                           GroupedItemsCollector collector)
    {
      var referenceExpression = CommonUtils.FindReferenceExpression(context.UnterminatedContext) ??
                                CommonUtils.FindReferenceExpression(context.TerminatedContext);
      if (referenceExpression == null) return false;

      var qualifier = referenceExpression.QualifierExpression;
      if (qualifier == null) return false;

      var qualifierType = qualifier.Type();
      if (!qualifierType.IsResolved) return false;

      var settingsStore = qualifier.GetSettingsStore();
      if (!settingsStore.GetValue(PostfixSettingsAccessor.ShowStaticMethods))
        return false;

      // prepare symbol table of suitable static methods
      var rule = referenceExpression.GetTypeConversionRule();
      var accessContext = new ElementAccessContext(qualifier);

      var typesCollector = new DeclaredTypesCollector();
      qualifierType.Accept(typesCollector);

      // collect all declared types
      var psiModule = context.PsiModule;
      var allTypesTable = typesCollector.Types
        .Aggregate(EmptySymbolTable.INSTANCE, (table, type) => table.Merge(type.GetSymbolTable(psiModule)))
        .Distinct();

      var symbolTable = allTypesTable.Filter(
        new CapatibleStaticMethodFilter(qualifierType, rule),
        OverriddenFilter.INSTANCE, new AccessRightsFilter(accessContext));

      var innerCollector = new GroupedItemsCollector();
      GetLookupItemsFromSymbolTable(symbolTable, innerCollector, context, false);

      // decorate static lookup elements
      var itemsOwner = context.BasicContext.LookupItemsOwner;
      foreach (var lookupItem in innerCollector.Items)
      {
        var elementLookupItem = lookupItem as DeclaredElementLookupItem;
        if (elementLookupItem == null) continue;

        elementLookupItem.TextColor = SystemColors.GrayText;
        SubscribeAfterComplete(elementLookupItem, itemsOwner);
        collector.AddToBottom(elementLookupItem);
      }

      return true;
    }

    private static void SubscribeAfterComplete([NotNull] DeclaredElementLookupItem lookupItem,
                                               [NotNull] ILookupItemsOwner itemsOwner)
    {
      // ugly as fuck :(
      lookupItem.AfterComplete += (
        ITextControl textControl, ref TextRange range, ref TextRange decoration,
        TailType tailType, ref Suffix suffix, ref IRangeMarker marker) =>
      {
        var solution = lookupItem.Solution;
        var psiServices = solution.GetPsiServices();
        psiServices.CommitAllDocuments();

        var preferredDeclaredElement = lookupItem.PreferredDeclaredElement;
        if (preferredDeclaredElement == null) return;

        var method = (IMethod) preferredDeclaredElement.Element;
        var ownerType = method.GetContainingType().NotNull();
        var hasMultipleParams = HasMultipleParameters(lookupItem, method);

        if (!hasMultipleParams) // put caret 'foo(arg){here};'
        {
          var documentRange = new DocumentRange(textControl.Document, decoration);
          marker = documentRange.EndOffsetRange().CreateRangeMarker();
        }

        foreach (var referenceExpression in TextControlToPsi
          .GetElements<IReferenceExpression>(solution, textControl.Document, range.StartOffset))
        {
          // 'remember' qualifier textually
          var qualifierExpression = referenceExpression.QualifierExpression;
          var qualifierText = qualifierExpression.NotNull().GetText();
          var referencePointer = referenceExpression.CreateTreeElementPointer();
          var parenthesisRange = decoration.SetStartTo(range.EndOffset);
          var parenthesisMarker = parenthesisRange.CreateRangeMarker(textControl.Document);

          // append ', ' if all overloads with >1 arguments
          if (HasOnlyMultipleParameters(lookupItem, method)) qualifierText += ", ";

          // insert qualifier as first argument
          var argumentPosition = TextRange.FromLength(
            decoration.EndOffset - (parenthesisRange.Length/2), 0);
          textControl.Document.ReplaceText(argumentPosition, qualifierText);

          // replace qualifier with type (predefined/user type)
          var keyword = CSharpTypeFactory.GetTypeKeyword(ownerType.GetClrName());
          var qualifierRange = qualifierExpression.GetDocumentRange().TextRange;
          textControl.Document.ReplaceText(qualifierRange, keyword ?? "T");

          psiServices.CommitAllDocuments();

          var newReference = referencePointer.GetTreeNode();
          if (newReference == null) break;

          if (keyword == null) // bind user type
          {
            var qualifier = (IReferenceExpression) newReference.QualifierExpression.NotNull();
            qualifier.Reference.BindTo(ownerType, preferredDeclaredElement.Substitution);

            range = newReference.NameIdentifier.GetDocumentRange().TextRange;
            decoration = TextRange.InvalidRange;
          }

          // show parameter info when needed
          if (hasMultipleParams && parenthesisMarker.IsValid)
          {
            LookupUtil.ShowParameterInfo(
              solution, textControl, parenthesisMarker.Range, null, itemsOwner);
          }

          break;
        }

        TipsManager.Instance.FeatureIsUsed(
          "Plugin.ControlFlow.PostfixTemplates.<static>", textControl.Document, solution);
      };
    }

    private sealed class DeclaredTypesCollector : TypeVisitor
    {
      [NotNull] public readonly List<IDeclaredType> Types = new List<IDeclaredType>();

      public override void VisitDeclaredType(IDeclaredType declaredType)
      {
        Types.Add(declaredType);

        var typeElement = declaredType.GetTypeElement();
        if (typeElement == null) return;

        var substitution = declaredType.GetSubstitution();
        var typeParameters = typeElement.TypeParameters;
        if (typeParameters.Count == 0) return;

        foreach (var typeParameter in typeParameters)
        {
          substitution[typeParameter].Accept(this);
        }
      }

      public override void VisitArrayType(IArrayType arrayType)
      {
        arrayType.ElementType.Accept(this);
      }

      public override void VisitPointerType(IPointerType pointerType)
      {
        pointerType.ElementType.Accept(this);
      }

      public override void VisitType(IType type) {}
      public override void VisitMultitype(IMultitype multitype) {}
      public override void VisitDynamicType(IDynamicType dynamicType) {}
      public override void VisitAnonymousType(IAnonymousType anonymousType) {}
    }

    private static bool HasMultipleParameters([NotNull] IDeclaredElementLookupItem lookupItem,
                                              [NotNull] IMethod method)
    {
      var methodsItem = lookupItem as MethodsLookupItem;
      if (methodsItem == null)
      {
        return HasMultipleParameters(method);
      }

      foreach (var instance in methodsItem.Methods)
      {
        if (HasMultipleParameters(instance.Element)) return true;
      }

      return false;
    }

    private static bool HasMultipleParameters([NotNull] IParametersOwner method)
    {
      var parameters = method.Parameters;
      if (parameters.Count > 1) return true;

      return parameters.Count == 1 && parameters[0].IsParameterArray;
    }

    private static bool HasOnlyMultipleParameters(
      [NotNull] IDeclaredElementLookupItem item, [NotNull] IMethod method)
    {
      var methodsItem = item as MethodsLookupItem;
      if (methodsItem == null)
      {
        return method.Parameters.Count > 1;
      }

      foreach (var instance in methodsItem.Methods)
      {
        if (instance.Element.Parameters.Count <= 1) return false;
      }

      return true;
    }

    private sealed class CapatibleStaticMethodFilter : SimpleSymbolFilter
    {
      [NotNull] private readonly IExpressionType myExpressionType;
      [NotNull] private readonly ICSharpTypeConversionRule myConversionRule;

      public CapatibleStaticMethodFilter([NotNull] IExpressionType expressionType,
                                         [NotNull] ICSharpTypeConversionRule conversionRule)
      {
        myExpressionType = expressionType;
        myConversionRule = conversionRule;
      }

      public override ResolveErrorType ErrorType
      {
        get { return ResolveErrorType.NOT_RESOLVED; }
      }

      public override bool Accepts(IDeclaredElement declaredElement, ISubstitution substitution)
      {
        var method = declaredElement as IMethod;
        if (method == null || !method.IsStatic) return false;
        if (method.IsExtensionMethod) return false;
        if (method.Parameters.Count <= 0) return false;

        // filter out static methods from Object.*
        if (method.GetContainingType().IsObjectClass()) return false;

        var firstParameter = method.Parameters[0];
        if (firstParameter.Kind != ParameterKind.VALUE) return false;

        var parameterType = firstParameter.Type;

        if (firstParameter.IsParameterArray)
        {
          var arrayType = parameterType as IArrayType;
          if (arrayType != null)
          {
            var elementType = arrayType.ElementType;
            if (myExpressionType.IsImplicitlyConvertibleTo(elementType, myConversionRule))
              return true;
          }
        }

        return myExpressionType.IsImplicitlyConvertibleTo(parameterType, myConversionRule);
      }
    }
  }
}