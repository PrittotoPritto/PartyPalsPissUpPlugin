using System;
using System.Text.Json;
using System.Reflection;

namespace PissUpPlugin
{
	public class JSONSerialisation
	{
		//Throws
		public static IGame? LoadFile(string path)
		{
			var file = new System.IO.FileInfo(path);
			System.IO.FileStream read = file.OpenRead();
			System.IO.MemoryStream memory = new System.IO.MemoryStream();
			read.CopyTo(memory);
			Utf8JsonReader reader = new Utf8JsonReader(memory.ToArray());
			return Deserialize(reader);
		}
		//Throws
		public static bool SaveFile(string path, IGame game)
		{
			var file = new System.IO.FileInfo(path);
			if (!file.Exists)
			{
				file.Create();
				file = new System.IO.FileInfo(path);
			}
			System.IO.FileStream write = file.OpenWrite();
			Utf8JsonWriter writer = new Utf8JsonWriter(write, new JsonWriterOptions { Indented = true });
			return Serialize(ref writer, game);
		}

		static IGame? Deserialize(Utf8JsonReader reader)
		{
			reader.Read();
			if (reader.TokenType == JsonTokenType.StartObject)
			{
				//Expecting a type name
				reader.Read();
				if (reader.TokenType == JsonTokenType.PropertyName)
				{
					if (reader.GetString() == "type")
					{
						reader.Read();
						var typeName = reader.GetString();
						Assembly? assembly = Assembly.GetAssembly(typeof(JSONSerialisation));
						if (assembly != null && typeName != null)
						{
							Type? gameType = assembly.GetType(typeName);
							reader.Read();
							if (gameType != null && reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "game")
							{
								return (IGame?)JsonSerializer.Deserialize(ref reader, gameType);
							}
						}
					}
				}
			}
			return null;
		}

		static bool Serialize(ref Utf8JsonWriter writer, IGame game)
		{
			Assembly? assembly = Assembly.GetAssembly(typeof(JSONSerialisation));
			if (assembly != null)
			{
				string? gameType = game.GetType().FullName;
				if (gameType != null)
				{
					writer.WriteStartObject();
					writer.WritePropertyName("type");
					writer.WriteStringValue((string)gameType);
					writer.WritePropertyName("game");
					JsonSerializer.Serialize(writer, game, game.GetType(), new JsonSerializerOptions { WriteIndented = true });
					writer.WriteEndObject();
					writer.Flush();
					return true;
				}
			}
			return false;
		}
	}
}
