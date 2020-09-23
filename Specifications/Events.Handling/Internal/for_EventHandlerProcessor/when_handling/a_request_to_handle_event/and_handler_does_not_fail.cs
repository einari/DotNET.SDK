// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Dolittle.Runtime.Events.Processing.Contracts;
using Dolittle.SDK.Protobuf;
using Machine.Specifications;

namespace Dolittle.SDK.Events.Handling.Internal.for_EventHandlerProcessor.when_handling.a_request_to_handle_event
{
    public class and_handler_does_not_fail : given.all_dependencies
    {
        static EventContext event_context;
        static EventHandlerResponse response;

        Establish context = () =>
        {
            var stream_event = new Processing.StreamEvent(
                new CommittedEvent(
                    committed_event.EventLogSequenceNumber,
                    committed_event.Occurred.ToDateTimeOffset(),
                    committed_event.EventSourceId.To<EventSourceId>(),
                    execution_context,
                    event_type_to_handle,
                    event_to_handle,
                    committed_event.Public),
                partitioned,
                request.Event.PartitionId.To<PartitionId>(),
                request.Event.ScopeId.To<ScopeId>());
            event_processing_converter
                .Setup(_ => _.ToSDK(Moq.It.IsAny<StreamEvent>()))
                .Returns(stream_event);
            event_context = new EventContext(
                committed_event.EventLogSequenceNumber,
                committed_event.EventSourceId.To<EventSourceId>(),
                committed_event.Occurred.ToDateTimeOffset(),
                execution_context,
                execution_context);
            event_types.Setup(_ => _.HasTypeFor(event_type_to_handle)).Returns(true);
            event_types.Setup(_ => _.GetTypeFor(event_type_to_handle)).Returns(typeof(given.some_event));
            event_handler
                .Setup(_ => _.Handle(Moq.It.IsAny<object>(), Moq.It.IsAny<EventType>(), Moq.It.IsAny<EventContext>(), Moq.It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        };

        Because of = async () =>
        {
            response = await event_handler_processor
                .Handle(request, execution_context, CancellationToken.None).ConfigureAwait(false);
        };

        It should_get_a_response = () => response.ShouldNotBeNull();
        It should_not_have_a_failure = () => response.Failure.ShouldBeNull();
        It should_have_called_the_handler = () => event_handler.Verify(_ => _.Handle(event_to_handle, event_type_to_handle, event_context, CancellationToken.None), Moq.Times.Once);
    }
}