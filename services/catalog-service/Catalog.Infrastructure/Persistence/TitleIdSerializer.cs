using Catalog.Domain.ValueObjects;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Serializes <see cref="TitleId"/> to/from a BSON string.
/// </summary>
/// <remarks>
/// Storing <see cref="TitleId"/> as a plain string (the _id field value) rather
/// than a subdocument keeps filters and the BsonClassMap <c>MapIdMember</c>
/// configuration simple — MongoDB's _id is always a scalar in this design.
/// </remarks>
internal sealed class TitleIdSerializer : SerializerBase<TitleId>
{
    public override TitleId Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();
        return new TitleId(value);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TitleId value)
    {
        context.Writer.WriteString(value.Value);
    }
}
