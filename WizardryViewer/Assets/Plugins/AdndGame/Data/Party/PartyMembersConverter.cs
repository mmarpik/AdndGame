using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Adnd.Data.Party;

public class PartyMembersConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var result = new List<string>();

        if (reader.TokenType != JsonToken.StartArray)
            return result;

        var array = JArray.Load(reader);

        foreach (var token in array)
        {
            if (token.Type == JTokenType.String)
            {
                var name = (string?)token;
                if (name != null)
                    result.Add(name);
                continue;
            }

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var nameProp = obj["Name"] ?? obj["name"];
                if (nameProp != null && nameProp.Type == JTokenType.String)
                {
                    var nm = (string?)nameProp;
                    if (nm != null)
                        result.Add(nm);
                    continue;
                }

                // If object doesn't have a Name property, try to extract a reasonable string
                // fallback: use the raw JSON text of the object
                result.Add(token.ToString(Formatting.None));
                continue;
            }

            // For any other token, skip or attempt to get string
            try
            {
                var s = token.ToString();
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

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        if (value is List<string> list)
        {
            foreach (var s in list)
            {
                writer.WriteValue(s);
            }
        }
        writer.WriteEndArray();
    }
}
