// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Dolittle.SDK.Concepts;
using Dolittle.SDK.Services;

namespace Dolittle.SDK.Events.Processing.Internal
{
    /// <summary>
    /// Defines a system that handles the behavior of event processors that registers with the Runtime and handles processing requests.
    /// </summary>
    /// <typeparam name="TIdentifier">The <see cref="Type" /> of the <see cref="ConceptAs{T}" /> <see cref="Guid" />.>.</typeparam>
    /// <typeparam name="TRegisterResponse">The <see cref="Type" /> of the registration response.</typeparam>
    /// <typeparam name="TRequest">The <see cref="Type" /> of the request.</typeparam>
    /// <typeparam name="TResponse">The <see cref="Type" /> of the response.</typeparam>
    public interface IEventProcessor<TIdentifier, TRegisterResponse, TRequest, TResponse> : IReverseCallHandler<TRequest, TResponse>
        where TIdentifier : ConceptAs<Guid>
        where TRegisterResponse : class
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Gets the <typeparamref name="TIdentifier"/> identifier.
        /// </summary>
        TIdentifier Identifier { get; }
    }
}
