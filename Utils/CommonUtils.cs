﻿using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Parsing;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion
{
  public static class CommonUtils
  {
    public static DocumentRange ToDocumentRange(
      [CanBeNull] this ReparsedCodeCompletionContext context, [NotNull] ITreeNode treeNode)
    {
      var documentRange = treeNode.GetDocumentRange();
      if (context == null) return documentRange;

      var originalRange = context.ToDocumentRange(treeNode.GetTreeTextRange());
      return new DocumentRange(documentRange.Document, originalRange);
    }

    [CanBeNull] public static ITokenNode FindSemicolonAfter(
      [NotNull] ICSharpExpression expression, [NotNull] ITreeNode reference)
    {
      var statement = ExpressionStatementNavigator.GetByExpression(expression);
      if (statement == null)
      {
        var referenceExpression = ReferenceExpressionNavigator.GetByQualifierExpression(expression);
        if (referenceExpression != null && referenceExpression == reference)
          statement = ExpressionStatementNavigator.GetByExpression(referenceExpression);

        if (statement == null) return null;
      }

      return statement.Semicolon;
    }

    public static bool IsInstantiable(
      [NotNull] ITypeElement typeElement, [NotNull] ITreeNode expression)
    {
      if (typeElement is IStruct || typeElement is IEnum || typeElement is IClass)
      {
        // filter out abstract classes
        var classType = typeElement as IClass;
        if (classType != null && classType.IsAbstract) return false;

        // check type has any constructor accessable
        var accessContext = new ElementAccessContext(expression);

        foreach (var constructor in typeElement.Constructors)
        {
          if (constructor.IsStatic) continue;
          if (AccessUtil.IsSymbolAccessible(constructor, accessContext))
          {
            return true;
          }
        }
      }

      return false;
    }

    public static bool CanTypeBecameExpression([CanBeNull] ICSharpExpression expression)
    {
      var referenceExpression = expression as IReferenceExpression;
      if (referenceExpression != null)
      {
        var parent = referenceExpression.Parent;

        if (IsLessThanRelationalExpression(parent)) return false;

        var expressionStatement = parent as IExpressionStatement;
        if (expressionStatement != null)
        {
          var previous = expressionStatement.GetPreviousStatementInBlock();
          if (previous == null) return true;

          // two children: relational and error element
          return !(
            IsLessThanRelationalExpression(previous.FirstChild) &&
            previous.FirstChild.NextSibling == previous.LastChild &&
            previous.LastChild is IErrorElement);
        }

        return CanTypeBecameExpression(parent as IReferenceExpression);
      }

      var predefinedType = expression as IPredefinedTypeExpression;
      if (predefinedType != null)
      {
        var parent = predefinedType.Parent;

        return CanTypeBecameExpression(parent as IReferenceExpression);
      }

      return true;
    }

    [ContractAnnotation("notnull => true")]
    private static bool IsLessThanRelationalExpression([CanBeNull] ITreeNode node)
    {
      var relationalExpression = node as IRelationalExpression;
      if (relationalExpression == null) return false;

      var sign = relationalExpression.OperatorSign;
      return sign != null && sign.GetTokenType() == CSharpTokenType.LT;
    }
  }
}