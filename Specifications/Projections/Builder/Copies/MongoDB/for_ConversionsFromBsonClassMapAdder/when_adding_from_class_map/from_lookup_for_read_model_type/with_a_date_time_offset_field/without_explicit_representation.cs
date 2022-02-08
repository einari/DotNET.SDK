using System;
using Dolittle.SDK.Projections.Copies.MongoDB;
using Machine.Specifications;
using MongoDB.Bson.Serialization;

namespace Dolittle.SDK.Projections.Builder.Copies.MongoDB.for_ConversionsFromBsonClassMapAdder.when_adding_from_class_map.from_lookup_for_read_model_type.with_a_date_time_offset_field;

public class without_explicit_representation : given.all_dependencies
{
    public class read_model
    {
        public DateTimeOffset DateTimeOffsetField;
        public string Property { get; }
    }

    Because of = () => adder.AddFrom(BsonClassMap.LookupClassMap(typeof(read_model)), build_results, conversions.Object);
    
    It should_add_conversion_to_date_time_for_date_time_offset_field = () => conversions.Verify(_ => _.AddConversion("DateTimeOffsetField", Conversion.DateAsArray));
    It should_not_add_anything_else = () => conversions.VerifyNoOtherCalls();
}