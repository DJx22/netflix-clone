using Catalog.Domain.ValueObjects;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Serializes <see cref="MaturityRating"/> to/from a BSON string.
/// </summary>
/// <remarks>
/// Stored as a plain string ("PG-13", "TV-MA", etc.) so the field is directly
/// readable in Atlas or mongo shell without driver-specific decoding.
/// </remarks>
internal sealed class MaturityRatingSerializer : SerializerBase<MaturityRating>
{
    public override MaturityRating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();
        return new MaturityRating(value);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, MaturityRating value)
    {
        context.Writer.WriteString(value.Value);
    }
}
