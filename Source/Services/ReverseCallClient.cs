// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Dolittle.SDK.Execution;
using Dolittle.SDK.Protobuf;
using Dolittle.SDK.Tenancy;
using Dolittle.Services.Contracts;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using ExecutionContext = Dolittle.SDK.Execution.ExecutionContext;

namespace Dolittle.SDK.Services
{
    /// <summary>
    /// An implementation of <see cref="IReverseCallClient{TConnectArguments, TConnectResponse, TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TClientMessage">Type of the <see cref="IMessage">messages</see> that is sent from the client to the server.</typeparam>
    /// <typeparam name="TServerMessage">Type of the <see cref="IMessage">messages</see> that is sent from the server to the client.</typeparam>
    /// <typeparam name="TConnectArguments">Type of the arguments that are sent along with the initial Connect call.</typeparam>
    /// <typeparam name="TConnectResponse">Type of the response that is received after the initial Connect call.</typeparam>
    /// <typeparam name="TRequest">Type of the requests sent from the server to the client.</typeparam>
    /// <typeparam name="TResponse">Type of the responses received from the client.</typeparam>
    public class ReverseCallClient<TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse>
        : IReverseCallClient<TConnectArguments, TConnectResponse, TRequest, TResponse>
        where TClientMessage : class, IMessage
        where TServerMessage : class, IMessage
        where TConnectArguments : class
        where TConnectResponse : class
        where TRequest : class
        where TResponse : class
    {
        readonly IAmAReverseCallProtocol<TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse> _protocol;
        readonly TimeSpan _pingInterval;
        readonly IPerformMethodCalls _caller;
        readonly ExecutionContext _executionContext;
        readonly IScheduler _scheduler;
        readonly ILogger _logger;
        readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseCallClient{TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="arguments">The <typeparamref name="TConnectArguments"/> to send to the server to start the reverse call protocol.</param>
        /// <param name="handler">The handler that will handle requests from the server.</param>
        /// <param name="protocol">The the reverse call protocol that will be used to connect to the server.</param>
        /// <param name="pingInterval">The interval at which to request pings from the server to keep the reverse call alive.</param>
        /// <param name="caller">The caller that will be used to perform the method call.</param>
        /// <param name="executionContext">The execution context to use while initiating the reverse call.</param>
        /// <param name="scheduler">The scheduler to use for executing reactive subscriptions.</param>
        /// <param name="logger">The logger that will be used to log messages while performing the reverse call.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
        public ReverseCallClient(
            TConnectArguments arguments,
            IReverseCallHandler<TRequest, TResponse> handler,
            IAmAReverseCallProtocol<TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse> protocol,
            TimeSpan pingInterval,
            IPerformMethodCalls caller,
            ExecutionContext executionContext,
            IScheduler scheduler,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Arguments = arguments;
            Handler = handler;
            _protocol = protocol;
            _pingInterval = pingInterval;
            _caller = caller;
            _executionContext = executionContext;
            _scheduler = scheduler;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        /// <inheritdoc/>
        public TConnectArguments Arguments { get; }

        /// <inheritdoc/>
        public IReverseCallHandler<TRequest, TResponse> Handler { get; }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<TConnectResponse> observer)
        {
            #pragma warning disable CA2000
            var toClientMessages = new Subject<TServerMessage>();
            #pragma warning restore CA2000

            var connectResponse = GetConnectResponseFromFirstMessageOrError(toClientMessages);
            var timeout = TimeoutAfterPingIntervalAfterFirstMessageIsReceived(toClientMessages);

            var pongs = RespondToPingWithPong(toClientMessages);
            var responses = RespondToRequestWithResponse(toClientMessages);

            var toServerMessages = MergeArgumentsWithPongsResponsesAndTimeout(pongs, responses, timeout);

            var connectResponseAndErrors = MergeConnectResponseWithErrorsFromServer(connectResponse, toClientMessages);
            connectResponseAndErrors.Subscribe(observer);

            toClientMessages
                .Where(MessageIsPing)
                .Subscribe(_ => Thread.Sleep(20));

            var subscription = _caller.Call(_protocol, toServerMessages, _cancellationToken).Subscribe(toClientMessages);

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                toClientMessages.Dispose();
            });
        }

        IObservable<TConnectResponse> GetConnectResponseFromFirstMessageOrError(IObservable<TServerMessage> toClientMessages)
            => toClientMessages
                .Take(1)
                .Select(_ =>
                    {
                        var response = _protocol.GetConnectResponseFrom(_);
                        if (response == null)
                        {
                            return Notification.CreateOnError<TConnectResponse>(new DidNotReceiveConnectResponse());
                        }

                        var failure = _protocol.GetFailureFromConnectResponse(response);
                        if (failure != null)
                        {
                            return Notification.CreateOnError<TConnectResponse>(new ReverseCallConnectionFailed(failure));
                        }

                        return Notification.CreateOnNext(response);
                    })
                .DefaultIfEmpty(Notification.CreateOnError<TConnectResponse>(new DidNotReceiveConnectResponse()))
                .Dematerialize();

        IObservable<TConnectResponse> MergeConnectResponseWithErrorsFromServer(IObservable<TConnectResponse> connectResponse, IObservable<TServerMessage> toClientMessages)
            => connectResponse.Concat(
                toClientMessages
                    .Where(_ => false)
                    .Cast<TConnectResponse>());

        IObservable<TClientMessage> RespondToPingWithPong(IObservable<TServerMessage> toClientMessages)
            => toClientMessages
                .Where(MessageIsPing)
                .Select(_protocol.GetPingFrom)
                .Select(_ => new Pong())
                .Select(_protocol.CreateMessageFrom);

        IObservable<TClientMessage> RespondToRequestWithResponse(IObservable<TServerMessage> toClientMessages)
            => toClientMessages
                .Where(MessageIsRequest)
                .Select(_protocol.GetRequestFrom)
                .Where(RequestIsValid)
                .Select(request => Observable.FromAsync((token) => HandleRequest(request, token), _scheduler))
                .Merge()
                .Select(_protocol.CreateMessageFrom);

        IObservable<TClientMessage> TimeoutAfterPingIntervalAfterFirstMessageIsReceived(IObservable<TServerMessage> toClientMessages)
            => toClientMessages
                .Take(1)
                .Concat(
                    toClientMessages
                        .Timeout(_pingInterval * 3, _scheduler))
                        .Where(_ => false)
                        .Cast<TClientMessage>()
                        .Catch((TimeoutException _) => Observable.Throw<TClientMessage>(new PingTimedOut(_pingInterval), _scheduler));

        IObservable<TClientMessage> MergeArgumentsWithPongsResponsesAndTimeout(IObservable<TClientMessage> pongs, IObservable<TClientMessage> responses, IObservable<TClientMessage> timeout)
        {
            var pongsResponsesAndTimeout = pongs.Merge(responses, _scheduler).Merge(timeout, _scheduler);

            var connectArguments = Arguments;
            var connectContext = CreateReverseCallArgumentsContext();
            _protocol.SetConnectArgumentsContextIn(connectContext, connectArguments);
            var connectMessage = _protocol.CreateMessageFrom(connectArguments);

            return pongsResponsesAndTimeout.StartWith(_scheduler, connectMessage);
        }

        bool MessageIsPing(TServerMessage message)
            => _protocol.GetPingFrom(message) != null;

        bool MessageIsRequest(TServerMessage message)
            => _protocol.GetRequestFrom(message) != null;

        bool RequestIsValid(TRequest request)
        {
            var context = _protocol.GetRequestContextFrom(request);
            if (context == null)
            {
                _logger.LogWarning("Received request from Reverse Call Dispatcher, but it did not contain a Reverse Call Context");
                return false;
            }
            else if (context.ExecutionContext == null)
            {
                _logger.LogWarning("Received request from Reverse Call Dispatcher, but it did not contain an Execution Context");
                return false;
            }

            return true;
        }

        async Task<TResponse> HandleRequest(TRequest request, CancellationToken token)
        {
            var requestContext = _protocol.GetRequestContextFrom(request);

            var executionContext = _executionContext
                .ForTenant(requestContext.ExecutionContext.TenantId.To<TenantId>())
                .ForCorrelation(requestContext.ExecutionContext.CorrelationId.To<CorrelationId>());

            var response = await Handler.Handle(request, executionContext, token).ConfigureAwait(false);

            var responseContext = new ReverseCallResponseContext { CallId = requestContext.CallId };
            _protocol.SetResponseContextIn(responseContext, response);

            return response;
        }

        ReverseCallArgumentsContext CreateReverseCallArgumentsContext()
            => new ReverseCallArgumentsContext
                {
                    HeadId = Guid.NewGuid().ToProtobuf(),
                    ExecutionContext = _executionContext.ToProtobuf(),
                    PingInterval = Duration.FromTimeSpan(_pingInterval),
                };
    }
}
