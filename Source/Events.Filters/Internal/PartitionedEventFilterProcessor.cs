// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Dolittle.Runtime.Events.Processing.Contracts;
using Dolittle.SDK.Events.Processing;
using Dolittle.SDK.Protobuf;
using Dolittle.SDK.Services;
using Dolittle.Services.Contracts;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using static Dolittle.Runtime.Events.Processing.Contracts.Filters;

namespace Dolittle.SDK.Events.Filters.Internal
{
    /// <summary>
    /// Represents a <see cref="FilterEventProcessor{TRegisterArguments, TResponse}" /> that can filter partitioned private events.
    /// </summary>
    public class PartitionedEventFilterProcessor : FilterEventProcessor<PartitionedFilterRegistrationRequest, PartitionedFilterResponse>
    {
        readonly ScopeId _scopeId;
        readonly PartitionedFilterEventCallback _filterEventCallback;
        readonly ICreateReverseCallClients _reverseCallClientsCreator;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionedEventFilterProcessor"/> class.
        /// </summary>
        /// <param name="filterId">The <see cref="FilterId" />.</param>
        /// <param name="scopeId">The <see cref="ScopeId" />.</param>
        /// <param name="filterEventCallback">The <see cref="PartitionedFilterEventCallback" />.</param>
        /// <param name="reverseCallClientsCreator">The <see cref="ICreateReverseCallClients" />.</param>
        /// <param name="processingRequestConverter">The <see cref="IEventProcessingRequestConverter" />.</param>
        /// <param name="logger">The <see cref="ILogger" />.</param>
        public PartitionedEventFilterProcessor(
            FilterId filterId,
            ScopeId scopeId,
            PartitionedFilterEventCallback filterEventCallback,
            ICreateReverseCallClients reverseCallClientsCreator,
            IEventProcessingRequestConverter processingRequestConverter,
            ILogger logger)
            : base("Partitioned Filter", filterId, processingRequestConverter, logger)
        {
            _scopeId = scopeId;
            _filterEventCallback = filterEventCallback;
            _reverseCallClientsCreator = reverseCallClientsCreator;
        }

        /// <inheritdoc/>
        protected override PartitionedFilterRegistrationRequest RegisterArguments
            => new PartitionedFilterRegistrationRequest
                {
                    FilterId = Identifier.ToProtobuf(),
                    ScopeId = _scopeId.ToProtobuf()
                };

        /// <inheritdoc/>
        protected override IReverseCallClient<PartitionedFilterRegistrationRequest, FilterRegistrationResponse, FilterEventRequest, PartitionedFilterResponse> CreateClient(
            PartitionedFilterRegistrationRequest registerArguments,
            Func<FilterEventRequest, CancellationToken, Task<PartitionedFilterResponse>> callback,
            uint pingTimeout,
            CancellationToken cancellation)
            => _reverseCallClientsCreator.Create(
                RegisterArguments,
                this,
                new DuplexStreamingMethodCaller(),
                new ReverseCallMessageConverter());

        /// <inheritdoc/>
        protected override PartitionedFilterResponse CreateResponseFromFailure(ProcessorFailure failure)
            => new PartitionedFilterResponse { Failure = failure };

        /// <inheritdoc/>
        protected override async Task<PartitionedFilterResponse> Filter(object @event, EventContext context)
        {
            var result = await _filterEventCallback(@event, context).ConfigureAwait(false);
            return new PartitionedFilterResponse { IsIncluded = result.ShouldInclude, PartitionId = result.PartitionId.ToProtobuf() };
        }

        class DuplexStreamingMethodCaller : ICanCallADuplexStreamingMethod<FiltersClient, PartitionedFilterClientToRuntimeMessage, FilterRuntimeToClientMessage>
        {
            public AsyncDuplexStreamingCall<PartitionedFilterClientToRuntimeMessage, FilterRuntimeToClientMessage> Call(Channel channel, CallOptions callOptions)
                => new FiltersClient(channel).ConnectPartitioned(callOptions);
        }

        class ReverseCallMessageConverter : IConvertReverseCallMessages<PartitionedFilterClientToRuntimeMessage, FilterRuntimeToClientMessage, PartitionedFilterRegistrationRequest, FilterRegistrationResponse, FilterEventRequest, PartitionedFilterResponse>
        {
            public PartitionedFilterClientToRuntimeMessage CreateMessageFrom(PartitionedFilterRegistrationRequest arguments)
                => new PartitionedFilterClientToRuntimeMessage { RegistrationRequest = arguments };

            public PartitionedFilterClientToRuntimeMessage CreateMessageFrom(Pong pong)
                => new PartitionedFilterClientToRuntimeMessage { Pong = pong };

            public PartitionedFilterClientToRuntimeMessage CreateMessageFrom(PartitionedFilterResponse response)
                => new PartitionedFilterClientToRuntimeMessage { FilterResult = response };

            public FilterRegistrationResponse GetConnectResponseFrom(FilterRuntimeToClientMessage message)
                => message.RegistrationResponse;

            public Ping GetPingFrom(FilterRuntimeToClientMessage message)
                => message.Ping;

            public ReverseCallRequestContext GetRequestContextFrom(FilterEventRequest message)
                => message.CallContext;

            public FilterEventRequest GetRequestFrom(FilterRuntimeToClientMessage message)
                => message.FilterRequest;

            public void SetConnectArgumentsContextIn(ReverseCallArgumentsContext context, PartitionedFilterRegistrationRequest arguments)
                => arguments.CallContext = context;

            public void SetResponseContextIn(ReverseCallResponseContext context, PartitionedFilterResponse response)
                => response.CallContext = context;
        }
    }
}