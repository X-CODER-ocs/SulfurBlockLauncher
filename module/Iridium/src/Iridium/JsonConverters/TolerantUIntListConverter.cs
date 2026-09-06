using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iridium.JsonConverters;

public sealed class TolerantUIntListConverter : JsonConverter<IReadOnlyList<uint>> {
    public override IReadOnlyList<uint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.StartObject) {
            if (reader.TokenType == JsonTokenType.StartObject)
                using (JsonDocument.ParseValue(ref reader)) { }
            
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            return [];

        var list = new List<uint>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            if (reader.TokenType == JsonTokenType.Number)
                list.Add(reader.GetUInt32());

        return list;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<uint> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}