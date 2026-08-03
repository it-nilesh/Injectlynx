using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Injectlynx.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Injectlynx.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectlynxCodeFixProvider))]
[Shared]
public sealed class InjectlynxCodeFixProvider : CodeFixProvider
{
    private const string MissingMatchingInterfaceId = "INJ001";
    private const string MissingExtensionMethodId = "CS1061";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            InjectlynxConfigurationAnalyzer.UnsupportedDslArgumentId,
            MissingMatchingInterfaceId,
            MissingExtensionMethodId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == InjectlynxConfigurationAnalyzer.UnsupportedDslArgumentId)
            {
                RegisterInlineStringConstantFix(context, root, diagnostic);
                continue;
            }

            if (diagnostic.Id == MissingMatchingInterfaceId)
            {
                RegisterUseSelfRegistrationFix(context, root, diagnostic);
                continue;
            }

            if (diagnostic.Id == MissingExtensionMethodId &&
                diagnostic.GetMessage().Contains("AddInjectlynxServices", StringComparison.Ordinal))
            {
                RegisterAddDefaultGeneratedNamespaceFix(context, root, diagnostic);
            }
        }
    }

    private static void RegisterInlineStringConstantFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ExpressionSyntax expression)
        {
            expression = node.AncestorsAndSelf().OfType<ExpressionSyntax>().FirstOrDefault()!;
        }

        if (expression is not IdentifierNameSyntax identifier)
        {
            return;
        }

        var variable = root
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier.ValueText, identifier.Identifier.ValueText, StringComparison.Ordinal) &&
                candidate.Initializer?.Value is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression));
        if (variable?.Initializer?.Value is not LiteralExpressionSyntax literalExpression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Inline string literal for Injectlynx DSL",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(expression, literalExpression.WithTriviaFrom(expression)))),
                equivalenceKey: "InlineInjectlynxStringLiteral"),
            diagnostic);
    }

    private static void RegisterUseSelfRegistrationFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var name = root
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .FirstOrDefault(identifier => string.Equals(identifier.Identifier.ValueText, "AsMatchingInterface", StringComparison.Ordinal));
        if (name is null)
        {
            return;
        }

        var replacement = SyntaxFactory.IdentifierName("AsSelf").WithTriviaFrom(name);
        context.RegisterCodeFix(
            CodeAction.Create(
                "Use AsSelf() registration",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(name, replacement))),
                equivalenceKey: "UseInjectlynxAsSelf"),
            diagnostic);
    }

    private static void RegisterAddDefaultGeneratedNamespaceFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        if (root is not CompilationUnitSyntax compilationUnit ||
            compilationUnit.Usings.Any(static item => item.Name?.ToString() == "Microsoft.Extensions.DependencyInjection"))
        {
            return;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName("Microsoft.Extensions.DependencyInjection"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        var newRoot = compilationUnit.AddUsings(usingDirective);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add Microsoft.Extensions.DependencyInjection using",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)),
                equivalenceKey: "AddInjectlynxDefaultNamespaceUsing"),
            diagnostic);
    }
}
