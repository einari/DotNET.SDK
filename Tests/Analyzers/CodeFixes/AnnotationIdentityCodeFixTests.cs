﻿// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Dolittle.SDK.Analyzers.CodeFixes;

public class AttributeIdentityCodeFixTests : CodeFixProviderTests<AttributeIdentityAnalyzer, AttributeIdentityCodeFixProvider>
{
    [Fact]
    public async Task FixAttributeWithInvalidIdentity()
    {
        var test = @"
using Dolittle.SDK.Events;

[EventType("""")]
class SomeEvent
{
    public string Name {get; set;}
}";

        var expected = @"
using Dolittle.SDK.Events;

[EventType(""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeEvent
{
    public string Name {get; set;}
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";
        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 15)
            .WithArguments("EventType", "eventTypeId", @"""""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }

    [Fact]
    public async Task FixesAttributeWithInvalidIdentityWithNamedArguments()
    {
        var test = @"
using Dolittle.SDK.Events;

[EventType(eventTypeId: """")]
class SomeEvent
{
    public string Name {get; set;}
}";

        var expected = @"
using Dolittle.SDK.Events;

[EventType(eventTypeId: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeEvent
{
    public string Name {get; set;}
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 28)
            .WithArguments("EventType", "eventTypeId", @"""""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }
    
    [Fact]
    public async Task FixesAttributeWithInvalidIdentityWithNamedArgumentsInNonDefaultPosition()
    {
        var test = @"
using Dolittle.SDK.Events;

[EventType(alias: ""Foo"", eventTypeId: """")]
class SomeEvent
{
    public string Name {get; set;}
}";

        var expected = @"
using Dolittle.SDK.Events;

[EventType(alias: ""Foo"", eventTypeId: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeEvent
{
    public string Name {get; set;}
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 42)
            .WithArguments("EventType", "eventTypeId", @"""""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }
    
    [Fact]
    public async Task FixesEventHandlers()
    {
        var test = @"
using Dolittle.SDK.Events.Handling;

[EventHandler(alias: ""Foo"", eventHandlerId: ""invalid-id"")]
class SomeHandler
{
}";

        var expected = @"
using Dolittle.SDK.Events.Handling;

[EventHandler(alias: ""Foo"", eventHandlerId: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeHandler
{
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 58)
            .WithArguments("EventHandler", "eventHandlerId", @"""invalid-id""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }
    
    [Fact]
    public async Task FixesProjections()
    {
        var test = @"
using Dolittle.SDK.Projections;

[Projection(alias: ""Foo"", projectionId: ""invalid-id"")]
class SomeProjection
{
}";

        var expected = @"
using Dolittle.SDK.Projections;

[Projection(alias: ""Foo"", projectionId: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeProjection
{
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 54)
            .WithArguments("Projection", "projectionId", @"""invalid-id""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }
    
    [Fact]
    public async Task FixesAggregates()
    {
        var test = @"
using Dolittle.SDK.Aggregates;

[AggregateRoot(alias: ""Foo"", id: ""invalid-id"")]
class SomeAggregate
{
}";

        var expected = @"
using Dolittle.SDK.Aggregates;

[AggregateRoot(alias: ""Foo"", id: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeAggregate
{
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 47)
            .WithArguments("AggregateRoot", "id", @"""invalid-id""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }
    
    [Fact]
    public async Task FixesEmbeddings()
    {
        var test = @"
using Dolittle.SDK.Embeddings;

[Embedding(embeddingId: ""invalid-id"")]
class SomeAggregate
{
}";

        var expected = @"
using Dolittle.SDK.Embeddings;

[Embedding(embeddingId: ""61359cf4-3ae7-4a26-8a81-6816d3877f81"")]
class SomeAggregate
{
}";
        IdentityGenerator.Override = "61359cf4-3ae7-4a26-8a81-6816d3877f81";

        var diagnosticResult = Diagnostic(DescriptorRules.InvalidIdentity)
            .WithSpan(4, 2, 4, 38)
            .WithArguments("Embedding", "embeddingId", @"""invalid-id""");
        await VerifyCodeFixAsync(test, expected, diagnosticResult);
    }

}
