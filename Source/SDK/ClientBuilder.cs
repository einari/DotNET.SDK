// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading;
using Dolittle.SDK.EventHorizon;
using Dolittle.SDK.Events;
using Dolittle.SDK.Events.Builders;
using Dolittle.SDK.Events.Filters;
using Dolittle.SDK.Events.Handling.Builder;
using Dolittle.SDK.Events.Processing;
using Dolittle.SDK.Events.Store.Builders;
using Dolittle.SDK.Events.Store.Converters;
using Dolittle.SDK.Execution;
using Dolittle.SDK.Microservices;
using Dolittle.SDK.Projections.Builder;
using Dolittle.SDK.Projections.Store;
using Dolittle.SDK.Projections.Store.Builders;
using Dolittle.SDK.Projections.Store.Converters;
using Dolittle.SDK.Resilience;
using Dolittle.SDK.Security;
using Dolittle.SDK.Services;
using Dolittle.SDK.Tenancy;
using Microsoft.Extensions.Logging;
using Environment = Dolittle.SDK.Microservices.Environment;
using ExecutionContext = Dolittle.SDK.Execution.ExecutionContext;
using Version = Dolittle.SDK.Microservices.Version;

namespace Dolittle.SDK
{
    /// <summary>
    /// Represents a builder for building <see cref="Client" />.
    /// </summary>
    public class ClientBuilder
    {
        readonly EventTypesBuilder _eventTypesBuilder;
        readonly EventFiltersBuilder _eventFiltersBuilder;
        readonly EventHandlersBuilder _eventHandlersBuilder;
        readonly ProjectionsBuilder _projectionsBuilder;
        readonly SubscriptionsBuilder _eventHorizonsBuilder;
        readonly MicroserviceId _microserviceId;
        readonly ProjectionReadModelTypeAssociations _projectionAssociations;
        string _host = "localhost";
        TimeSpan _timeout = TimeSpan.FromSeconds(5);
        ushort _port = 50053;
        Version _version;
        Environment _environment;
        CancellationToken _cancellation;
        RetryPolicy _retryPolicy;

        ILoggerFactory _loggerFactory = LoggerFactory.Create(_ =>
            {
                _.SetMinimumLevel(LogLevel.Information);
                _.AddConsole();
            });

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientBuilder"/> class.
        /// </summary>
        /// <param name="microserviceId">The <see cref="MicroserviceId"/> of the microservice.</param>
        public ClientBuilder(MicroserviceId microserviceId)
        {
            _microserviceId = microserviceId;

            _version = Version.NotSet;
            _environment = Environment.Undetermined;
            _cancellation = CancellationToken.None;
            _retryPolicy = (IObservable<Exception> exceptions) => exceptions.Delay(TimeSpan.FromSeconds(1));

            _projectionAssociations = new ProjectionReadModelTypeAssociations();

            _eventTypesBuilder = new EventTypesBuilder();
            _eventFiltersBuilder = new EventFiltersBuilder();
            _eventHandlersBuilder = new EventHandlersBuilder();
            _projectionsBuilder = new ProjectionsBuilder(_projectionAssociations);
            _eventHorizonsBuilder = new SubscriptionsBuilder();
        }

        /// <summary>
        /// Sets the version of the microservice.
        /// </summary>
        /// <param name="version">The version of the microservice.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithVersion(Version version)
        {
            _version = version;
            return this;
        }

        /// <summary>
        /// Sets the ping timeout for communicating with the microservice.
        /// </summary>
        /// <param name="timeout">The ping timeout.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the version of the microservice.
        /// </summary>
        /// <param name="major">Major version of the microservice.</param>
        /// <param name="minor">Minor version of the microservice.</param>
        /// <param name="patch">Path level of the microservice.</param>
        /// <param name="build">Build number of the microservice.</param>
        /// <param name="preReleaseString">If prerelease - the prerelease string.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithVersion(int major, int minor, int patch, int build = 0, string preReleaseString = "")
        {
            _version = new Version(major, minor, patch, build, preReleaseString);
            return this;
        }

        /// <summary>
        /// Sets the environment in which the microservice is running.
        /// </summary>
        /// <param name="environment">The environment in which the microservice is running.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithEnvironment(Environment environment)
        {
            _environment = environment;
            return this;
        }

        /// <summary>
        /// Connect to a specific host and port for the Dolittle runtime.
        /// </summary>
        /// <param name="host">The host name to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>The client builder for continuation.</returns>
        /// <remarks>If not specified, host 'localhost' and port 50053 will be used.</remarks>
        public ClientBuilder WithRuntimeOn(string host, ushort port)
        {
            _host = host;
            _port = port;
            return this;
        }

        /// <summary>
        /// Sets the cancellation token for cancelling pending operations on the Runtime.
        /// </summary>
        /// <param name="cancellation">The cancellation token for cancelling pending operations on the Runtime.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithCancellation(CancellationToken cancellation)
        {
            _cancellation = cancellation;
            return this;
        }

        /// <summary>
        /// Sets the event types through the <see cref="EventTypesBuilder" />.
        /// </summary>
        /// <param name="callback">The builder callback.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithEventTypes(Action<EventTypesBuilder> callback)
        {
            callback(_eventTypesBuilder);
            return this;
        }

        /// <summary>
        /// Sets the filters through the <see cref="EventFiltersBuilder" />.
        /// </summary>
        /// <param name="callback">The builder callback.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithFilters(Action<EventFiltersBuilder> callback)
        {
            callback(_eventFiltersBuilder);
            return this;
        }

        /// <summary>
        /// Sets the event handlers through the <see cref="EventHandlersBuilder" />.
        /// </summary>
        /// <param name="callback">The builder callback.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithEventHandlers(Action<EventHandlersBuilder> callback)
        {
            callback(_eventHandlersBuilder);
            return this;
        }

        /// <summary>
        /// Sets the event handlers through the <see cref="ProjectionsBuilder" />.
        /// </summary>
        /// <param name="callback">The builder callback.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithProjections(Action<ProjectionsBuilder> callback)
        {
            callback(_projectionsBuilder);
            return this;
        }

        /// <summary>
        /// Sets the event handlers through the <see cref="SubscriptionsBuilder" />.
        /// </summary>
        /// <param name="callback">The builder callback.</param>
        /// <returns>The client builder for continuation.</returns>
        public ClientBuilder WithEventHorizons(Action<SubscriptionsBuilder> callback)
        {
            callback(_eventHorizonsBuilder);
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ILoggerFactory"/> to use for creating instances of <see cref="ILogger"/> for the client.
        /// </summary>
        /// <param name="factory">The given <see cref="ILoggerFactory"/>.</param>
        /// <returns>The client builder for continuation.</returns>
        /// <remarks>If not used, a factory with 'Trace' level logging will be used.</remarks>
        public ClientBuilder WithLogging(ILoggerFactory factory)
        {
            _loggerFactory = factory;
            return this;
        }

        /// <summary>
        /// Build the Client.
        /// </summary>
        /// <returns>The <see cref="Client"/>.</returns>
        public Client Build()
        {
            var executionContext = new ExecutionContext(
                _microserviceId,
                TenantId.System,
                _version,
                _environment,
                CorrelationId.System,
                Claims.Empty,
                CultureInfo.InvariantCulture);
            var eventTypes = new EventTypes(_loggerFactory.CreateLogger<EventTypes>());
            _eventTypesBuilder.AddAssociationsInto(eventTypes);

            var methodCaller = new MethodCaller(_host, _port);
            var reverseCallClientsCreator = new ReverseCallClientCreator(
                _timeout,
                methodCaller,
                executionContext,
                _loggerFactory);

            var serializer = new EventContentSerializer(eventTypes);
            var eventToProtobufConverter = new EventToProtobufConverter(serializer);
            var eventToSDKConverter = new EventToSDKConverter(serializer);
            var aggregateEventToProtobufConverter = new AggregateEventToProtobufConverter(serializer);
            var aggregateEventToSDKConverter = new AggregateEventToSDKConverter(serializer);
            var projectionsToSDKConverter = new ProjectionsToSDKConverter();

            var eventProcessingConverter = new EventProcessingConverter(eventToSDKConverter);
            var processingCoordinator = new ProcessingCoordinator();

            var eventProcessors = new EventProcessors(reverseCallClientsCreator, processingCoordinator, _loggerFactory.CreateLogger<EventProcessors>());

            var callContextResolver = new CallContextResolver();

            var eventStoreBuilder = new EventStoreBuilder(
                methodCaller,
                eventToProtobufConverter,
                eventToSDKConverter,
                aggregateEventToProtobufConverter,
                aggregateEventToSDKConverter,
                executionContext,
                callContextResolver,
                eventTypes,
                _loggerFactory);

            var eventHorizons = new EventHorizons(methodCaller, executionContext, _loggerFactory.CreateLogger<EventHorizons>());
            _eventHorizonsBuilder.BuildAndSubscribe(eventHorizons, _cancellation);

            var projectionStoreBuilder = new ProjectionStoreBuilder(
                methodCaller,
                executionContext,
                callContextResolver,
                _projectionAssociations,
                projectionsToSDKConverter,
                _loggerFactory);

            return new Client(
                eventTypes,
                eventStoreBuilder,
                eventHorizons,
                processingCoordinator,
                eventProcessors,
                eventProcessingConverter,
                _eventHandlersBuilder,
                _eventFiltersBuilder,
                projectionsToSDKConverter,
                _projectionsBuilder,
                projectionStoreBuilder,
                _loggerFactory,
                _cancellation);
        }
    }
}
