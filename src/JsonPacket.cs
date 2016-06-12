using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.ServiceModel.Web;
using System.IO;

namespace CodeAtlas
{
	[DataContract]
	public class JsonDict<V>
	{
		[DataMember]
		Dictionary<string, V> dict = new Dictionary<string, V>();

		public JsonDict() { }

		protected JsonDict(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

// 		public void GetObjectData(SerializationInfo info, StreamingContext context)
// 		{
// 			foreach (string key in dict.Keys)
// 			{
// 				string ks = key.ToString();
// 				info.AddValue(key, dict[key]);
// 			}
// 		}

		public void Add(string key, V value)
		{
			dict.Add(key, value);
		}

		[IgnoreDataMember]
		public V this[string index]
		{
			set { dict[index] = value; }
			get { return dict[index]; }
		}
	}

    [DataContract(Namespace = "MyAddin2")]
    class JsonPacket
    {
        [DataMember(Order = 0)]
        public string f { get; set; }
        [DataMember(Order = 1)]
        public object[] p { get; set; }


        public static JsonPacket fromJson(string dataString)
        {
            var mStream = new MemoryStream(Encoding.Default.GetBytes(dataString));
			var serializer = new DataContractJsonSerializer(typeof(JsonPacket));
			JsonPacket packet = (JsonPacket)serializer.ReadObject(mStream);
			return packet;
        }

		public string toJson()
		{
			var serializer = new DataContractJsonSerializer(
				typeof(JsonPacket));
			var stream = new MemoryStream();
			serializer.WriteObject(stream, this);

			byte[] dataBytes = new byte[stream.Length];
			stream.Position = 0;
			stream.Read(dataBytes, 0, (int)stream.Length);

			string dataString = Encoding.UTF8.GetString(dataBytes);
			return dataString;
		}
    }
}
