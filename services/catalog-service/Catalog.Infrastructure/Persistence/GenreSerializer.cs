using Catalog.Domain.ValueObjects;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Serializes <see cref="Genre"/> to/from a BSON string.
/// </summary>
/// <remarks>
/// <see cref="Title.Genres"/> is stored as a BSON array of strings — flat,
/// directly queryable with a <c>$in</c> or equality filter on the <c>genres</c>
/// field, and compatible with the text index defined in <c>TitleIndexes</c>.
/// </remarks>
internal sealed class GenreSerializer : SerializerBase<Genre>
{
    public override Genre Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();
        return new Genre(value);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Genre value)
    {
        context.Writer.WriteString(value.Value);
    }
}
