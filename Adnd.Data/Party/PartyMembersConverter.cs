using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adnd.Data.Party;

public class PartyMembersConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return new List<string>();

        var result = new List<string>();

        // Read start of array
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                if (name != null)
                    result.Add(name);
                continue;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    var nm = nameProp.GetString();
                    if (nm != null)
                        result.Add(nm);
                    continue;
                }
                if (doc.RootElement.TryGetProperty("name", out var nameProp2) && nameProp2.ValueKind == JsonValueKind.String)
                {
                    var nm = nameProp2.GetString();
                    if (nm != null)
                        result.Add(nm);
                    continue;
                }

                // If object doesn't have a Name property, try to extract a reasonable string
                // fallback: use the raw JSON text of the object
                result.Add(doc.RootElement.GetRawText());
                continue;
            }

            // For any other token, skip or attempt to get string
            try
            {
                var s = reader.GetString();
                if (s != null)
                    result.Add(s);
            }
            catch
            {
                // ignore
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var s in value)
        {
            writer.WriteStringValue(s);
        }
        writer.WriteEndArray();
    }
}
