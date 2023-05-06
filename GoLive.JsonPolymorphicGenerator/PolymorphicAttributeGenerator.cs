using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        
        context.RegisterSourceOutput(items, (spc, source) => Execute(source.Left, source.Right, spc) );
    }


    private void Execute(ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> polyClasses, ImmutableArray<INamedTypeSymbol> allClasses, SourceProductionContext spc)
    {
        foreach (var namedTypeSymbol in polyClasses)
        {
            var ssb = new SourceStringBuilder();
            ssb.AppendLine("using System.Text.Json.Serialization;");
            ssb.AppendLine();
            var res = allClasses.Where(e => Scanner.InheritsFrom(e, namedTypeSymbol.nts));

            if (res.Any())
            {
                ssb.AppendLine($"namespace {namedTypeSymbol.nts.ContainingNamespace.ToDisplayString()}");
                ssb.AppendOpenCurlyBracketLine();
                
                foreach (var symbol in res)
                {
                    ssb.AppendLine($"[JsonDerivedType(typeof({symbol.ToDisplayString(fullDisplayFormat)}), \"{symbol.ToDisplayString(shortDisplayFormat)}\")]");
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