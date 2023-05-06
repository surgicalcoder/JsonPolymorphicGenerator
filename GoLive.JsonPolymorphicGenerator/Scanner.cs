using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace GoLive.JsonPolymorphicGenerator;

public static class Scanner
{
    public static bool InheritsFrom(INamedTypeSymbol symbol, ITypeSymbol type)
    {
        var baseType = symbol.BaseType;
        while (baseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType))
                return true;

            baseType = baseType.BaseType;
        }

        return false;
    }
    
    public static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax c
               && c.Modifiers.Any(m => ((SyntaxToken)m).IsKind(SyntaxKind.AbstractKeyword))
               && c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
               && c.AttributeLists.Count > 0
            ;
    }
}