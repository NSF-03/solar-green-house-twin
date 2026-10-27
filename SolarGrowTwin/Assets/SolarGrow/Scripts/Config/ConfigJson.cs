using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Newtonsoft settings for GreenhouseConfig export/import. Only public instance fields are written
    /// (never UnityEngine.Object properties such as name or hideFlags), Vector3 is written as {x,y,z}
    /// (Newtonsoft alone loops on Vector3.normalized), enums as names, and lists are replaced, not appended.
    /// </summary>
    public static class ConfigJson
    {
        static JsonSerializerSettings settings;

        public static JsonSerializerSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    JsonSerializerSettings s = new JsonSerializerSettings();
                    s.ContractResolver = new PublicFieldsContractResolver();
                    s.Converters.Add(new Vector3JsonConverter());
                    s.Converters.Add(new StringEnumConverter());
                    s.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    s.MissingMemberHandling = MissingMemberHandling.Ignore;
                    s.Formatting = Formatting.Indented;
                    settings = s;
                }
                return settings;
            }
        }

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        public static void Populate(string json, object target)
        {
            JsonConvert.PopulateObject(json, target, Settings);
        }
    }

    /// <summary>Serializes public instance fields only.</summary>
    public sealed class PublicFieldsContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            List<MemberInfo> members = new List<MemberInfo>();
            FieldInfo[] fields = objectType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.IsInitOnly || f.IsLiteral) continue;
                if (f.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                members.Add(f);
            }
            return members;
        }
    }

    /// <summary>Vector3 as {"x":..,"y":..,"z":..}; also reads [x, y, z].</summary>
    public sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            JObject o = token as JObject;
            if (o != null)
                return new Vector3(Read(o["x"], existingValue.x), Read(o["y"], existingValue.y), Read(o["z"], existingValue.z));
            JArray a = token as JArray;
            if (a != null && a.Count >= 3)
                return new Vector3(Read(a[0], existingValue.x), Read(a[1], existingValue.y), Read(a[2], existingValue.z));
            return existingValue;
        }

        static float Read(JToken token, float fallback)
        {
            double d;
            return TwinJson.TryGetNumber(token, out d) ? (float)d : fallback;
        }
    }
}
