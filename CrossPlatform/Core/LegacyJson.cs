using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace System.Web.Script.Serialization;

// Deliberate compatibility adapter: the shared Windows engine uses public fields,
// ScriptIgnore, untyped dictionaries/arrays, and Microsoft JSON date strings.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ScriptIgnoreAttribute : Attribute { }

public sealed class JavaScriptSerializer
{
    public int MaxJsonLength { get; set; } = int.MaxValue;
    public int RecursionLimit { get; set; } = 100;
    private JsonSerializerSettings Settings => new()
    {
        ContractResolver = new LegacyContract(), MaxDepth = RecursionLimit,
        DateParseHandling = DateParseHandling.None, Culture = Globalization.CultureInfo.InvariantCulture,
        TypeNameHandling = TypeNameHandling.None,
        Converters = { new LegacyDateConverter() }
    };
    public string Serialize(object value)
    {
        string result = JsonConvert.SerializeObject(value, Settings);
        if (result.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds the configured limit.");
        return result;
    }
    public T Deserialize<T>(string text)
    {
        if (text.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds the configured limit.");
        if (typeof(T) == typeof(Dictionary<string, object>) || typeof(T) == typeof(object))
            return (T)Untyped(JToken.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }))!;
        return JsonConvert.DeserializeObject<T>(text, Settings)!;
    }
    private static object? Untyped(JToken token) => token switch
    {
        JObject o => o.Properties().ToDictionary(p => p.Name, p => Untyped(p.Value)),
        JArray a => a.Select(Untyped).ToArray(),
        JValue v => v.Value,
        _ => null
    };
    private sealed class LegacyContract : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
        {
            var property = base.CreateProperty(member, serialization);
            if (member.GetCustomAttribute<ScriptIgnoreAttribute>() != null) property.Ignored = true;
            return property;
        }
    }
    private sealed class LegacyDateConverter : JsonConverter<DateTime>
    {
        public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer) =>
            writer.WriteValue("/Date(" + new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeMilliseconds() + ")/");
        public override DateTime ReadJson(JsonReader reader, Type type, DateTime existing, bool hasExisting, JsonSerializer serializer)
        {
            string text = Convert.ToString(reader.Value, Globalization.CultureInfo.InvariantCulture) ?? "";
            var match = Regex.Match(text, @"^/Date\((-?\d+)(?:[+-]\d{4})?\)/$");
            return match.Success ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value, Globalization.CultureInfo.InvariantCulture)).UtcDateTime
                : DateTime.Parse(text, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind);
        }
    }
}
