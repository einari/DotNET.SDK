// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Dolittle.SDK.Events;
using Machine.Specifications;

namespace Dolittle.SDK.Testing.Aggregates.for_AggregateMock.when_getting_aggregate;

class and_aggregate_has_yet_to_be_used : given.an_aggregate_of<StatelessAggregateRoot>
{
    static StatelessAggregateRoot aggregate;
    static EventSourceId event_source;
    Establish context = () =>
    {
        event_source = "an event source";
        use_aggregate_factory(_ => new StatelessAggregateRoot(_));
    };
    
    Because of = () => aggregate = aggregate_of.GetAggregate(event_source);
    
    It should_get_the_correct_aggregate = () => aggregate.EventSourceId.ShouldEqual(event_source);
    It should_invoke_factory_only_once = () => aggregate_factory.Verify(_ => _.Invoke(Moq.It.IsAny<EventSourceId>()), Moq.Times.Once);
    It should_invoke_factory_with_the_correct_event_source = () => aggregate_factory.Verify(_ => _.Invoke(event_source), Moq.Times.Once);
}