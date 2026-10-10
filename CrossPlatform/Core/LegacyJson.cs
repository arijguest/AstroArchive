#nullable enable
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
    public object DeserializeObject(string text) => Deserialize<object>(text);
    public T Deserialize<T>(string text)
    {
        if (text.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds the configured limit.");
        if (typeof(T) == typeof(Dictionary<string, object>) || typeof(T) == typeof(object))
        {
            using var input = new IO.StringReader(text);
            using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = RecursionLimit };
            return (T)Untyped(JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }))!;
        }
        return JsonConvert.DeserializeObject<T>(text, Settings)!;
    }
    private static object? Untyped(JToken token) => token switch
    {
        JObject o => o.Properties().ToDictionary(p => p.Name, p => Untyped(p.Value)),
        JArray a => a.Select(Untyped).ToArray(),
        JValue v when v.Value is string value && Regex.IsMatch(value, @"^/Date\((-?\d+)(?:[+-]\d{4})?\)/$") =>
            DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(Regex.Match(value, @"^/Date\((-?\d+)").Groups[1].Value, Globalization.CultureInfo.InvariantCulture)).UtcDateTime,
        JValue v => v.Value,
        _ => null
    };
    private sealed class LegacyContract : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
        {
            var property = base.CreateProperty(member, serialization);
            if (member.GetCustomAttribute<ScriptIgnoreAttribute>() != null) property.Ignored = true;
            bool relative = member.Name == "RelativePath" || member.Name == "SidecarRelativePath" ||
                member.DeclaringType == typeof(AstroArchive.SourceManifest) && member.Name == "Destination";
            if (relative && property.ValueProvider != null) property.ValueProvider = new PortablePathValue(property.ValueProvider);
            if (member.DeclaringType == typeof(AstroArchive.EditedProject) && member.Name == "MetadataEdits" && property.ValueProvider != null)
                property.ValueProvider = new PortableEditsValue(property.ValueProvider);
            return property;
        }
    }
    // Keep the established Windows on-disk separator. Decode it at the boundary
    // so Linux in-memory paths use '/' while released Windows versions read '\\'.
    private sealed class PortablePathValue(IValueProvider inner) : IValueProvider
    {
        public object? GetValue(object target) => inner.GetValue(target) is string path ? path.Replace('/', '\\') : inner.GetValue(target);
        public void SetValue(object target, object? value) => inner.SetValue(target, value is string path ? path.Replace('\\', IO.Path.DirectorySeparatorChar) : value);
    }
    private sealed class PortableEditsValue(IValueProvider inner) : IValueProvider
    {
        public object? GetValue(object target) => inner.GetValue(target) is Dictionary<string, AstroArchive.EditedMetadata> edits
            ? edits.ToDictionary(p => p.Key.Replace('/', '\\'), p => p.Value) : inner.GetValue(target);
        public void SetValue(object target, object? value) => inner.SetValue(target, value is Dictionary<string, AstroArchive.EditedMetadata> edits
            ? edits.ToDictionary(p => p.Key.Replace('\\', IO.Path.DirectorySeparatorChar), p => p.Value) : value);
    }
    private sealed class LegacyDateConverter : JsonConverter<DateTime>
    {
        public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer) =>
            // JavaScriptSerializer recognises dates by the escaped JSON slashes.
            writer.WriteRawValue("\"\\/Date(" + new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeMilliseconds() + ")\\/\"");
        public override DateTime ReadJson(JsonReader reader, Type type, DateTime existing, bool hasExisting, JsonSerializer serializer)
        {
            string text = Convert.ToString(reader.Value, Globalization.CultureInfo.InvariantCulture) ?? "";
            var match = Regex.Match(text, @"^/Date\((-?\d+)(?:[+-]\d{4})?\)/$");
            return match.Success ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value, Globalization.CultureInfo.InvariantCulture)).UtcDateTime
                : DateTime.Parse(text, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind);
        }
    }
}
