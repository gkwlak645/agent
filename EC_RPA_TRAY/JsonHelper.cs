using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EC_RPA_TRAY.Core
{
    public static class JsonHelper
    {
        //! @brief      Serialize의 DateTime 형식을 고정하여 변환한다.
        public static string Serialize<T>(T entry)
        {
            JsonSerializerSettings CustomDateFormatSettings = new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-dd HH:mm:ss"
            };

            string custom_format_string = JsonConvert.SerializeObject(entry, CustomDateFormatSettings);

            return custom_format_string;
        }

        public static int Deserialize<T>(string json_string, ref T recv_list)
        {
            if (string.IsNullOrWhiteSpace(json_string))
            {
                return -1;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTime,
                DateFormatString = "yyyy-MM-dd HH:mm:ss",
            };

            settings.Converters.Add(new StringTimeConverter());
            settings.Converters.Add(new StringLongConverter());
            settings.Converters.Add(new StringDoubleConverter());

            recv_list = JsonConvert.DeserializeObject<T>(json_string, settings);

            return 0;
        }

        public static int Deserialize<T>(StreamReader sr, ref T recv_list)
        {
            if (sr == null)
            {
                return -1;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTime,
                DateFormatString = "yyyy-MM-dd HH:mm:ss",
            };

            settings.Converters.Add(new StringTimeConverter());
            settings.Converters.Add(new StringLongConverter());
            settings.Converters.Add(new StringDoubleConverter());

            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = JsonSerializer.Create(settings);
                recv_list = serializer.Deserialize<T>(reader);
            }

            return 0;
        }

        public class StringTimeConverter : DateTimeConverterBase
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.Value == null)
                {
                    return DateTime.MinValue;
                }

                if (reader.Value.GetType() == typeof(string))
                {
                    string reader_value = reader.Value as string;
                    if (string.IsNullOrWhiteSpace(reader_value))
                    {
                        return DateTime.MinValue;
                    }

                    if (reader_value.Length > 19)
                    {
                        reader_value = reader_value.Substring(0, 19);
                        //return reader_value.SafeDateTime("yyyy-MM-dd HH:mm:ss");
                        return reader_value;
                    }
                }

                //return reader.Value.SafeDateTime();
                return reader.Value;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                DateTime dateTimeValue = (DateTime)value;
                if (dateTimeValue == DateTime.MinValue)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteValue(value);
            }
        }

        public class StringLongConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(long));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.Value == null)
                    return (long)0;

                if (reader.Value.GetType() == typeof(string))
                {
                    string reader_value = reader.Value as string;
                    if (string.IsNullOrWhiteSpace(reader_value))
                    {
                        return (long)0;
                    }
                }

                //return reader.Value.SafeLong();
                return reader.Value;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }

        public class StringDoubleConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(double));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.Value == null)
                    return (double)0;

                if (reader.Value.GetType() == typeof(string))
                {
                    string reader_value = reader.Value as string;
                    if (string.IsNullOrWhiteSpace(reader_value))
                    {
                        return (double)0;
                    }
                }

                //return reader.Value.SafeDouble();
                return reader.Value;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }

    }
}
