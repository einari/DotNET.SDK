using System;
using Dolittle.SDK.Aggregates;

namespace Dolittle.SDK.Testing.Aggregates.for_AggregatesMock.given;

class spec_for<TAggregate>
    where TAggregate : AggregateRoot
{
    protected static IAggregateOf<TAggregate> aggregate_of;
    protected static AggregateOfMock<TAggregate> aggregate_of_mock;
    protected static Func<AggregatesMock> create_aggregates_mock;
    protected static AggregatesMock aggregates_mock;

    protected static void getting_the_aggregate_of()
    {
        aggregate_of = get_aggregate_of();
        aggregate_of_mock = aggregate_of as AggregateOfMock<TAggregate>;
    }

    protected static IAggregateOf<TAggregate> get_aggregate_of()
    {
        aggregates_mock ??= create_aggregates_mock();
        return aggregates_mock.Of<TAggregate>();
    }
}