using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GoLive.JsonPolymorphicGenerator;


[Generator]
public class PolymorphicAttributeGenerator : IIncrementalGenerator
{
    SymbolDisplayFormat fullDisplayFormat = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
    SymbolDisplayFormat shortDisplayFormat = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly);
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var allParentPolyClasses = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (s, _) => Scanner.IsCandidate(s),
            transform: static (ctx, _) => GetDeclarationsThatHasAttr(ctx)
        ).Where(static c => c is { cl: not null, nts: not null } );
        
        var allClasses = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (s, _) => s is ClassDeclarationSyntax c && !c.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
            transform: static (ctx, _) => GetDeclarations(ctx)
        ).Where(static c => c is not null);

        var items = allParentPolyClasses.Collect().Combine(allClasses.Collect());

        var items2 = context.AnalyzerConfigOptionsProvider.Combine(items);
        
        context.RegisterSourceOutput(items2, (spc, source) => Execute2(source.Left, source.Right, spc) );
    }

    private void Execute2(AnalyzerConfigOptionsProvider config, (ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> polyClasses, ImmutableArray<INamedTypeSymbol> Right) allClasses, SourceProductionContext spc)
    {
        foreach (var namedTypeSymbol in allClasses.polyClasses)
        {
            _ = config.GetOptions(namedTypeSymbol.cl.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_preappend", out string? preAppend);
            _ = config.GetOptions(namedTypeSymbol.cl.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_postappend", out string? postAppend);
            
            var ssb = new SourceStringBuilder();
            ssb.AppendLine("using System.Text.Json.Serialization;");
            ssb.AppendLine();
            var res = allClasses.Right.Where(e => Scanner.InheritsFrom(e, namedTypeSymbol.nts));

            if (res.Any())
            {
                ssb.AppendLine($"namespace {namedTypeSymbol.nts.ContainingNamespace.ToDisplayString()}");
                ssb.AppendOpenCurlyBracketLine();
                
                foreach (var symbol in res)
                {
                    ssb.AppendLine($"[JsonDerivedType(typeof({symbol.ToDisplayString(fullDisplayFormat)}), \"{preAppend}{symbol.ToDisplayString(shortDisplayFormat)}{postAppend}\")]");
                }
                
                ssb.AppendLine($"public partial class {namedTypeSymbol.nts.Name}");
                ssb.AppendOpenCurlyBracketLine();
                ssb.AppendCloseCurlyBracketLine();
                ssb.AppendCloseCurlyBracketLine();
                ssb.AppendLine();
                ssb.AppendLine();
                
                spc.AddSource($"{namedTypeSymbol.nts.Name}.g.cs", ssb.ToString());
            }
        }
    }

    private static INamedTypeSymbol GetDeclarations(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        return symbol;
    }
    
    private static (ClassDeclarationSyntax cl, INamedTypeSymbol nts) GetDeclarationsThatHasAttr(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        //var methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;
        foreach (var attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (var attributeSyntax in attributeListSyntax.Attributes)
            {
                if (ModelExtensions.GetSymbolInfo(context.SemanticModel, attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [LoggerMessage] attribute?
                if (fullName == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
                {
                    // return the parent class of the method
                    return (classDeclarationSyntax, context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax));
                }
            }
        }

        return (null, null);
    }
}
