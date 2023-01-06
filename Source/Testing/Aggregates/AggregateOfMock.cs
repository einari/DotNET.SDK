﻿// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Dolittle.SDK.Aggregates;
using Dolittle.SDK.Events;

namespace Dolittle.SDK.Testing.Aggregates;

/// <summary>
/// Represents a mock implementation of <see cref="IAggregateOf{TAggregateRoot}"/>.
/// </summary>
/// <typeparam name="TAggregate">The <see cref="Type"/> of the <see cref="AggregateRoot"/>.</typeparam>
public class AggregateOfMock<TAggregate> : IAggregateOf<TAggregate>
    where TAggregate : AggregateRoot
{
    readonly Func<EventSourceId, TAggregate> _createAggregateRoot;
    readonly ConcurrentDictionary<EventSourceId, object> _aggregateLocks = new();
    readonly ConcurrentDictionary<EventSourceId, TAggregate> _aggregates = new();
    readonly ConcurrentDictionary<EventSourceId, int> _numEventsBeforeLastOperation = new();

    /// <summary>
    /// Initializes an instance of the <see cref="AggregateOfMock{T}"/> class.
    /// </summary>
    /// <param name="createAggregateRoot"></param>
    public AggregateOfMock(Func<EventSourceId, TAggregate> createAggregateRoot)
    {
        _createAggregateRoot = createAggregateRoot;
    }

    /// <summary>
    /// Gets all the aggregates that have had operations performed on them.
    /// </summary>
    public IEnumerable<TAggregate> Aggregates => _aggregates.Values;

    /// <inheritdoc />
    public IAggregateRootOperations<TAggregate> Get(EventSourceId eventSourceId)
    {
        var aggregate = GetOrAddAggregate(eventSourceId);
        return new AggregateRootOperationsMock<TAggregate>(
            _aggregateLocks[eventSourceId],
            aggregate,
            () => _createAggregateRoot(eventSourceId),
            oldAggregate => _aggregates[eventSourceId] = oldAggregate,
            numEventsBeforeLastOperation => _numEventsBeforeLastOperation[eventSourceId] = numEventsBeforeLastOperation);
    }

    /// <summary>
    /// Gets the <see cref="AggregateRootOperationsMock{TAggregate}"/> for the aggregate.
    /// </summary>
    /// <param name="eventSourceId">The event source id.</param>
    /// <returns>The <see cref="AggregateRootOperationsMock{TAggregate}"/>.</returns>
    public AggregateRootOperationsMock<TAggregate> GetMock(EventSourceId eventSourceId)
        => (AggregateRootOperationsMock<TAggregate>) Get(eventSourceId);

    /// <summary>
    /// Tries to get the <typeparamref name="TAggregate"/> with the given <see cref="EventSourceId"/>.
    /// </summary>
    /// <param name="eventSource">The <see cref="EventSourceId"/> of the aggregate.</param>
    /// <param name="aggregate">The aggregate.</param>
    /// <returns>True if operations has been performed on aggregate, false if not.</returns>
    public bool TryGetAggregate(EventSourceId eventSource, out TAggregate aggregate)
        => _aggregates.TryGetValue(eventSource, out aggregate);

    /// <summary>
    /// Gets the aggregate with the given <see cref="EventSourceId"/>.
    /// </summary>
    /// <param name="eventSource">The <see cref="EventSourceId"/> of the aggregate</param>
    /// <returns>The <typeparamref name="TAggregate"/>.</returns>
    public TAggregate GetAggregate(EventSourceId eventSource)
        => GetOrAddAggregate(eventSource);

    /// <summary>
    /// Gets <see cref="AggregateRootAssertion"/> for the stored aggregate with an event sequence to assert on as if it only performed the last operation.
    /// </summary>
    /// <param name="eventSource">The event source of the aggregate.</param>
    /// <returns>The <see cref="AggregateRootAssertion"/>.</returns>
    public AggregateRootAssertion AfterLastOperationOn(EventSourceId eventSource)
        => new(GetOrAddAggregate(eventSource), _numEventsBeforeLastOperation.GetOrAdd(eventSource, 0));

    /// <summary>
    /// Gets <see cref="AggregateRootAssertion"/> for the stored aggregate.
    /// </summary>
    /// <param name="eventSource">The event source of the aggregate.</param>
    /// <returns>The <see cref="AggregateRootAssertion"/>.</returns>
    public AggregateRootAssertion AssertThat(EventSourceId eventSource)
        => new(GetOrAddAggregate(eventSource));
    
    TAggregate GetOrAddAggregate(EventSourceId eventSource)
        => _aggregates.GetOrAdd(eventSource, eventSourceId =>
        {
            _aggregateLocks.TryAdd(eventSourceId, new object());
            return _createAggregateRoot(eventSourceId);
        });
}

