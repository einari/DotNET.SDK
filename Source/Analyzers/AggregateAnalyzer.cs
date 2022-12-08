﻿// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dolittle.SDK.Analyzers;

#pragma warning disable CS1574, CS1584, CS1581, CS1580
/// <summary>
/// Analyzer for <see cref="Dolittle.SDK.Aggregates.AggregateRoot"/>.
/// </summary>
#pragma warning restore CS1574, CS1584, CS1581, CS1580
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AggregateAnalyzer : DiagnosticAnalyzer
{
    record Types(INamedTypeSymbol AggregateRoot, INamedTypeSymbol AggregateAttribute)
    {
        public INamedTypeSymbol AggregateRoot { get; } = AggregateRoot;
        public INamedTypeSymbol AggregateAttribute { get; } = AggregateAttribute;
    }

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DescriptorRules.Aggregate.MissingAttribute,
            DescriptorRules.Aggregate.MissingMutation,
            DescriptorRules.Aggregate.MutationShouldBePrivate,
            DescriptorRules.Aggregate.MutationHasIncorrectNumberOfParameters,
            DescriptorRules.Events.MissingAttribute
        );

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var types = GetRelevantTypes(compilationContext.Compilation);
            if (types is null) return;


            // Register an action that accesses the immutable state and reports diagnostics.
            compilationContext.RegisterSymbolAction(
                symbolContext => { AnalyzeAggregates(symbolContext, types); }, SymbolKind.NamedType);
        });
    }


    static void AnalyzeAggregates(SymbolAnalysisContext context, Types types)
    {
        // Check if the symbol has the aggregate root base class
        var aggregateType = (INamedTypeSymbol)context.Symbol;
        if (aggregateType.BaseType?.Equals(types.AggregateRoot, SymbolEqualityComparer.Default) != true) return;
        if (aggregateType.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not ClassDeclarationSyntax aggregateSyntax) return;

        CheckAggregateRootAttributePresent(context, aggregateType, types.AggregateAttribute);


        var handledEvents = CheckOnMethods(context, aggregateType);
        CheckApplyInvocations(context, aggregateSyntax, handledEvents);
    }


    static HashSet<ITypeSymbol> CheckOnMethods(SymbolAnalysisContext context, INamedTypeSymbol aggregateType)
    {
        var members = aggregateType.GetMembers();
        var onMethods = members.Where(_ => _.Name.Equals("On")).OfType<IMethodSymbol>().ToArray();
        var eventTypesHandled = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var onMethod in onMethods)
        {
            if (onMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax syntax) continue;

            if (syntax.Modifiers.Any(SyntaxKind.PublicKeyword)
                || syntax.Modifiers.Any(SyntaxKind.InternalKeyword)
                || syntax.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(DescriptorRules.Aggregate.MutationShouldBePrivate, syntax.GetLocation(),
                    onMethod.ToDisplayString()));
            }

            var parameters = onMethod.Parameters;
            if (parameters.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(DescriptorRules.Aggregate.MutationHasIncorrectNumberOfParameters, syntax.GetLocation(),
                    onMethod.ToDisplayString()));
            }

            if (parameters.Length > 0)
            {
                var eventType = parameters[0].Type;
                eventTypesHandled.Add(eventType);

                if (!eventType.HasEventTypeAttribute())
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DescriptorRules.Events.MissingAttribute,
                        parameters[0].DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation(),
                        eventType.ToTargetClassAndAttributeProps(DolittleTypes.EventTypeAttribute),
                        eventType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
                    );
                }
            }
        }

        return eventTypesHandled;
    }

    static void CheckAggregateRootAttributePresent(SymbolAnalysisContext context, INamedTypeSymbol aggregateClass, INamedTypeSymbol attributeType)
    {
        var hasAttribute = aggregateClass.GetAttributes()
            .Any(attribute => attribute.AttributeClass?.Equals(attributeType, SymbolEqualityComparer.Default) == true);

        if (!hasAttribute)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DescriptorRules.Aggregate.MissingAttribute,
                aggregateClass.Locations[0],
                aggregateClass.ToTargetClassAndAttributeProps(DolittleTypes.AggregateRootAttribute),
                aggregateClass.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            ));
        }
    }


    static void CheckApplyInvocations(SymbolAnalysisContext context, ClassDeclarationSyntax aggregateClassSyntax,
        ISet<ITypeSymbol> handledEventTypes)
    {
        // TODO: refactor analyzer to prevent creating a new SemanticModel for each class
        var semanticModel = context.Compilation.GetSemanticModel(aggregateClassSyntax.SyntaxTree);
        foreach (var invocation in aggregateClassSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not IdentifierNameSyntax { Identifier.Text: "Apply" }) continue;
            if (invocation.ArgumentList.Arguments.Count != 1) continue;
            var argument = invocation.ArgumentList.Arguments[0];
            var typeInfo = semanticModel.GetTypeInfo(argument.Expression);
            if (typeInfo.Type is not { } type) continue;
            if (!type.HasEventTypeAttribute())
            {
                context.ReportDiagnostic(Diagnostic.Create(DescriptorRules.Events.MissingAttribute, invocation.GetLocation(),
                    type.ToTargetClassAndAttributeProps(DolittleTypes.EventTypeAttribute), type.ToString()));
            }

            if (!handledEventTypes.Contains(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(DescriptorRules.Aggregate.MissingMutation, invocation.GetLocation(), type.ToMinimalTypeNameProps(),
                    type.ToString()));
            }
        }
    }


    static Types? GetRelevantTypes(Compilation compilation)
    {
        var aggregateBaseClass = compilation.GetTypeByMetadataName(DolittleTypes.AggregateRootBaseClass);
        if (aggregateBaseClass == null)
        {
            return default;
        }

        var aggregateRootAttribute = compilation.GetTypeByMetadataName(DolittleTypes.AggregateRootAttribute);
        if (aggregateRootAttribute == null)
        {
            return default;
        }


        return new Types(aggregateBaseClass, aggregateRootAttribute);
    }
}