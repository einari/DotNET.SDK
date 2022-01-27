// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MongoDB.Bson;
using PbProjectionCopyToMongoDB = Dolittle.Runtime.Events.Processing.Contracts.ProjectionCopyToMongoDB;

namespace Dolittle.SDK.Projections.Copies.MongoDB;

public record ProjectionCopyToMongoDB(ProjectionMongoDBCopyCollectionName CollectionName, IDictionary<string, BsonType> Conversions)
{
    /// <summary>
    /// Creates a Protobuf representation of this <see cref="ProjectionCopyToMongoDB"/>.
    /// </summary>
    /// <returns><see cref="PbProjectionCopyToMongoDB"/>.</returns>
    public PbProjectionCopyToMongoDB ToProtobuf()
    {
        var result = new PbProjectionCopyToMongoDB
        {
            Collection = CollectionName    
        };
        
        foreach (var (fieldName, type) in Conversions)
        {
            result.Conversions.Add(fieldName, ToProtobuf(type));
        }
        return result;
    }

    static PbProjectionCopyToMongoDB.Types.BSONType ToProtobuf(BsonType type)
        => type switch
        {
            BsonType.Binary => PbProjectionCopyToMongoDB.Types.BSONType.Binary,
            BsonType.DateTime => PbProjectionCopyToMongoDB.Types.BSONType.Date,
            BsonType.Timestamp => PbProjectionCopyToMongoDB.Types.BSONType.Timestamp,
            _ => throw new UnsupportedBSONType(type)
        };
}
