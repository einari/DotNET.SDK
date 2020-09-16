// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Contracts = Dolittle.Runtime.Events.Contracts;

namespace Dolittle.SDK.Events
{
    /// <summary>
    /// Defines a system that is capable of converting events to and from protobuf.
    /// </summary>
    public interface IEventConverter
    {
        /// <summary>
        /// Convert from <see cref="Contracts.CommittedEvent"/> to <see cref="CommittedEvent"/>.
        /// </summary>
        /// <param name="source"><see cref="Contracts.CommittedEvent"/>.</param>
        /// <returns>Converted <see cref="CommittedEvent"/>.</returns>
        CommittedEvent ToSDK(Contracts.CommittedEvent source);

        /// <summary>
        /// Convert from <see cref="IEnumerable{T}"/> of type <see cref="Contracts.CommittedEvent"/> to <see cref="CommittedEvents"/>.
        /// </summary>
        /// <param name="source"><see cref="IEnumerable{T}"/> of type <see cref="Contracts.CommittedEvent"/>.</param>
        /// <returns>Converted <see cref="CommittedEvents"/>.</returns>
        CommittedEvents ToSDK(IEnumerable<Contracts.CommittedEvent> source);

        /// <summary>
        /// Convert from <see cref="UncommittedEvent" /> to <see cref="Contracts.UncommittedEvent" />.
        /// </summary>
        /// <param name="event"><see cref="UncommittedEvent" />.</param>
        /// <returns>Converted <see cref="Contracts.UncommittedEvent" />.</returns>
        Contracts.UncommittedEvent ToProtobuf(UncommittedEvent @event);

        /// <summary>
        /// Convert from <see cref="UncommittedEvent" /> to <see cref="IEnumerable{T}"/> of type <see cref="Contracts.CommittedEvent"/>.
        /// </summary>
        /// <param name="events"><see cref="UncommittedEvent" />.</param>
        /// <returns>Converted see <see cref="IEnumerable{T}"/> of type <see cref="Contracts.CommittedEvent"/>.</returns>
        IEnumerable<Contracts.UncommittedEvent> ToProtobuf(UncommittedEvents events);

        /// <summary>
        /// Convert from <see cref="Contracts.CommitEventsResponse"/> to <see cref="CommitEventsResult"/>.
        /// </summary>
        /// <param name="source"><see cref="Contracts.CommitEventsResponse"/>.</param>
        /// <returns>Converted <see cref="CommitEventsResult"/>.</returns>
        CommitEventsResult ToSDK(Contracts.CommitEventsResponse source);
    }
}
