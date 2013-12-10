﻿using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using System;

namespace JetBrains.ReSharper.ControlFlow.PostfixCompletion.LookupItems
{
  public abstract class StatementPostfixLookupItem<TStatement> : PostfixLookupItem
    where TStatement : class, ICSharpStatement
  {
    protected StatementPostfixLookupItem(
      [NotNull] string shortcut, [NotNull] PrefixExpressionContext context)
      : base(shortcut, context) { }

    protected override bool RemoveSemicolon { get { return true; } }

    protected override void ExpandPostfix(
      ITextControl textControl, Suffix suffix,
      ISolution solution, DocumentRange replaceRange,
      IPsiModule psiModule, ICSharpExpression expression)
    {
      textControl.Document.ReplaceText(
        replaceRange.TextRange, "using(null){" + PostfixMarker + "}");

      solution.GetPsiServices().CommitAllDocuments();

      int? caretPosition = null;
      TStatement newStatement = null;
      using (WriteLockCookie.Create())
      {
        var commandName = GetType().FullName + " expansion";
        solution.GetPsiServices().DoTransaction(commandName, () =>
        {
          var expressionStatements = TextControlToPsi.GetElements<IUsingStatement>(
            solution, textControl.Document, replaceRange.TextRange.StartOffset);

          foreach (var block in expressionStatements)
          {
            var body = block.Body as IBlock;
            if (body == null) continue;

            if (body.Statements.Count != 1) continue;
            var statement = body.Statements[0] as IExpressionStatement;
            if (statement == null) continue;

            if (!IsMarkerExpressionStatement(statement, PostfixMarker)) continue;

            var factory = CSharpElementFactory.GetInstance(psiModule);
            newStatement = CreateStatement(factory);

            // find caret marker in created statement
            var caretMarker = new TreeNodeMarker(Guid.NewGuid().ToString());
            var collector = new RecursiveElementCollector<IExpressionStatement>(
              expressionStatement => IsMarkerExpressionStatement(expressionStatement, CaretMarker));
            var caretNodes = collector.ProcessElement(newStatement).GetResults();
            if (caretNodes.Count == 1) caretMarker.Mark(caretNodes[0]);

            // replace marker statement with the new one
            newStatement = block.ReplaceBy(newStatement);
            PlaceExpression(newStatement, expression, factory);

            // find and remove caret marker node
            var caretNode = caretMarker.FindMarkedNode(newStatement);
            if (caretNode != null)
            {
              caretPosition = caretNode.GetDocumentRange().TextRange.StartOffset;
              LowLevelModificationUtil.DeleteChild(caretNode);
            }

            caretMarker.Unmark(newStatement);
            break;
          }
        });
      }

      if (newStatement != null)
        AfterComplete(textControl, suffix, newStatement, caretPosition);
    }

    protected virtual bool SuppressSemicolonSuffix { get { return false; } }

    protected virtual void AfterComplete(
      [NotNull] ITextControl textControl, [NotNull] Suffix suffix,
      [NotNull] TStatement statement, int? caretPosition)
    {
      if (SuppressSemicolonSuffix && suffix.HasPresentation && suffix.Presentation == ';')
      {
        suffix = Suffix.Empty;
      }

      AfterComplete(textControl, suffix, caretPosition);
    }

    [NotNull] protected abstract TStatement CreateStatement([NotNull] CSharpElementFactory factory);

    // todo: => PutExpression?
    protected abstract void PlaceExpression(
      [NotNull] TStatement statement, [NotNull] ICSharpExpression expression,
      [NotNull] CSharpElementFactory factory);

    private static bool IsMarkerExpressionStatement(
      [NotNull] IExpressionStatement expressionStatement, [NotNull] string markerName)
    {
      var reference = expressionStatement.Expression as IReferenceExpression;
      return reference != null
          && reference.QualifierExpression == null
          && reference.Delimiter == null
          && reference.NameIdentifier.Name == markerName;
    }
  }
}