// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reactive.Threading.Tasks;
using System.Threading;
using Dolittle.SDK.Protobuf;
using Dolittle.SDK.Security;
using Dolittle.SDK.Services.given.ReverseCall;
using Dolittle.Services.Contracts;
using Machine.Specifications;
using Microsoft.Reactive.Testing;
using Moq;
using ExecutionContext = Dolittle.SDK.Execution.ExecutionContext;
using It = Machine.Specifications.It;
using Version = Dolittle.SDK.Microservices.Version;

namespace Dolittle.SDK.Services.for_ReverseCallClient.when_handling
{
    [Ignore("because thread needs to sleep")]
    public class a_request_that_takes_a_long_time_to_complete : given.a_reverse_call_client
    {
        static ConnectArguments connectArguments;
        static ConnectResponse connectResponse;
        static ExecutionContext requestExecutionContext;
        static Request request;
        static Response response;
        static Mock<IReverseCallHandler<Request, Response>> handler;

        static ExecutionContext executionContextPassedToHandler;
        static ITestableObservable<ServerMessage> serverToClientMessages;
        static ITestableObserver<ConnectResponse> observer;

        Establish context = () =>
        {
            connectArguments = new ConnectArguments();
            connectResponse = new ConnectResponse(connectArguments);

            requestExecutionContext = new ExecutionContext(
                "8b8d14a9-dfa0-49cf-b716-d23121ce7006",
                "fa33c31b-1d6a-48b5-9d99-544525fb261a",
                new Version(0, 0, 0, 0, ""),
                "belbonwogi",
                "960f76fd-9deb-4e58-8c82-8b79c5e10bb9",
                new Claims(new[]
                    {
                        new Claim("zumcutuwji", "vokidawtet", "pewvimapus"),
                    }),
                CultureInfo.InvariantCulture);

            request = new Request
            {
                Context = new ReverseCallRequestContext
                {
                    CallId = Guid.NewGuid().ToProtobuf(),
                    ExecutionContext = requestExecutionContext.ToProtobuf(),
                }
            };
            response = new Response(request);

            var slowResponse = scheduler.CreateHotObservable(
                OnNext(200, response),
                OnCompleted<Response>(201)).ToTask();

            handler = new Mock<IReverseCallHandler<Request, Response>>();
            handler
                .Setup(_ => _.Handle(request, Moq.It.IsAny<ExecutionContext>(), Moq.It.IsAny<CancellationToken>()))
                .Callback<Request, ExecutionContext, CancellationToken>((request, executionContext, token) => executionContextPassedToHandler = executionContext)
                .Returns(slowResponse);

            serverToClientMessages = scheduler.CreateHotObservable(
                OnNext(100, new ServerMessage { Response = connectResponse }),
                OnNext(110, new ServerMessage { Request = request }),
                OnNext(140, new ServerMessage { Ping = new Ping() }),
                OnNext(180, new ServerMessage { Ping = new Ping() }),
                OnCompleted<ServerMessage>(250));
        };

        Because of = () => observer = scheduler.Start(
            () => reverse_call_client_with(connectArguments, handler.Object, serverToClientMessages),
            created: 0,
            subscribed: 0,
            disposed: 1000);

        It should_call_the_handler_to_handle_the_request = () => handler.Verify(_ => _.Handle(request, Moq.It.IsAny<ExecutionContext>(), Moq.It.IsAny<CancellationToken>()));
        It should_not_call_the_handler_any_more = () => handler.VerifyNoOtherCalls();
        It should_pass_the_correct_microservice_to_the_handler = () => executionContextPassedToHandler.Microservice.ShouldEqual(executionContext.Microservice);
        It should_pass_the_correct_tenant_to_the_handler = () => executionContextPassedToHandler.Tenant.ShouldEqual(requestExecutionContext.Tenant);
        It should_pass_the_correct_version_to_the_handler = () => executionContextPassedToHandler.Version.ShouldEqual(executionContext.Version);
        It should_pass_the_correct_environment_to_the_handler = () => executionContextPassedToHandler.Environment.ShouldEqual(executionContext.Environment);
        It should_pass_the_correct_correlation_to_the_handler = () => executionContextPassedToHandler.CorrelationId.ShouldEqual(requestExecutionContext.CorrelationId);
        It should_pass_the_correct_claims_to_the_handler = () => executionContextPassedToHandler.Claims.ShouldEqual(executionContext.Claims);
        It should_pass_the_correct_culture_to_the_handler = () => executionContextPassedToHandler.Culture.ShouldEqual(executionContext.Culture);

        It should_send_the_arguments_and_the_response_to_the_server_with_pongs_in_between = () => ReactiveAssert.AreElementsEqual(
            new[]
            {
                OnNext<ClientMessage>(2, _ => _.Arguments == connectArguments),
                OnNext<ClientMessage>(140, _ => _.Pong != null),
                OnNext<ClientMessage>(180, _ => _.Pong != null),
                OnNext<ClientMessage>(202, _ => _.Response == response && _.Response.Context.CallId == request.Context.CallId),
                OnCompleted<ClientMessage>(250),
            },
            messagesSentToServer.Messages);

        It should_receive_the_response = () => ReactiveAssert.AreElementsEqual(
            new[]
            {
                OnNext(100, connectResponse),
                OnCompleted<ConnectResponse>(250),
            },
            observer.Messages);
    }
}