// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Dolittle.SDK.Builders;
using Dolittle.SDK.DependencyInversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dolittle.SDK;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> for adding setting up the <see cref="IDolittleClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IDolittleClient"/> and <see cref="DolittleClientConfiguration"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="setupClient">The optional <see cref="SetupDolittleClient"/> callback.</param>
    /// <param name="configureClient">The optional <see cref="ConfigureDolittleClient"/> callback.</param>
    /// <returns>The <see cref="IServiceCollection"/> for continuation.</returns>
    public static IServiceCollection AddDolittle(this IServiceCollection services, SetupDolittleClient setupClient = default, ConfigureDolittleClient configureClient = default)
    {
        return services
            .AddDolittleOptions()
            .AddDolittleClient(setupClient, configureClient);
    }

    static IServiceCollection AddDolittleOptions(this IServiceCollection services)
    {
        services
            .AddOptions<Configurations.Dolittle>()
            .BindConfiguration(nameof(Configurations.Dolittle));
        return services;
    }

    static IServiceCollection AddDolittleClient(this IServiceCollection services, SetupDolittleClient setupClient = default, ConfigureDolittleClient configureClient = default)
    {
        var dolittleClient = DolittleClient.Setup(setupClient);
        return services
            .AddSingleton(dolittleClient)
            .AddHostedService(provider => new DolittleClientService(dolittleClient, ConfigureWithDefaultsFromServiceProvider(provider, configureClient)));
    }

    static DolittleClientConfiguration ConfigureWithDefaultsFromServiceProvider(IServiceProvider provider, ConfigureDolittleClient configureClient = default)
    {
        var config = provider.GetService<IOptions<Configurations.Dolittle>>()?.Value;
        
        var clientConfig = config != default ? DolittleClientConfiguration.FromConfiguration(config) : new DolittleClientConfiguration();
        var loggerFactory = provider.GetService<ILoggerFactory>();
        if (loggerFactory != default)
        {
            clientConfig.WithLogging(loggerFactory);
        }


        configureClient?.Invoke(clientConfig);
        if (clientConfig.ServiceProvider is null)
        {
            clientConfig.WithServiceProvider(provider);
        }

        if (clientConfig.CreateTenantContainer is null)
        {
            clientConfig.WithTenantContainerCreator(clientConfig.CreateTenantContainerFactory is not null
                ? clientConfig.CreateTenantContainerFactory(clientConfig.ServiceProvider)
                : services => new DefaultTenantContainersCreator().Create(clientConfig.ServiceProvider, services));
        }
        
        return clientConfig;
    }
}
