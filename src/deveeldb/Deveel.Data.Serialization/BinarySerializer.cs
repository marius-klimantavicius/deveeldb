﻿// 
//  Copyright 2010-2016 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

using DryIoc;

namespace Deveel.Data.Serialization {
	public sealed class BinarySerializer {
		public BinarySerializer() {
			Encoding = Encoding.Unicode;
		}

		public Encoding Encoding { get; set; }

		public object Deserialize(Stream stream) {
			if (stream == null)
				throw new ArgumentNullException("stream");
			if (!stream.CanRead)
				throw new ArgumentException("The input stream cannot be read.", "stream");

			var reader = new BinaryReader(stream, Encoding);
			return Deserialize(reader);
		}

		public object Deserialize(BinaryReader reader) {
			if (reader == null)
				throw new ArgumentNullException("reader");

			var graphType = ReadType(reader);

			if (graphType == null)
				throw new InvalidOperationException("No type found in the graph stream");

#if PCL
			if (!graphType.GetTypeInfo().IsDefined(typeof(SerializableAttribute)))
#else
			if (!Attribute.IsDefined(graphType, typeof (SerializableAttribute)))
#endif
				throw new ArgumentException(String.Format("The type '{0}' is not marked as serializable.", graphType));

#if PCL
			if (graphType.IsAssignableTo(typeof(ISerializable)))
#else
			if (typeof (ISerializable).IsAssignableFrom(graphType))
#endif
				return CustomDeserialize(reader, graphType);

			return DeserializeType(reader, graphType);
		}

		private object DeserializeType(BinaryReader reader, Type graphType) {
			object obj;

#if !PCL
			if (graphType.IsValueType) {
#else
			if (graphType.IsValueType()) {
#endif
				obj = Activator.CreateInstance(graphType);
			} else {
				var ctor = GetDefaultConstructor(graphType);
				if (ctor == null)
					throw new NotSupportedException(String.Format("The type '{0}' does not specify any default empty constructor.", graphType));

				obj = ctor.Invoke(new object[0]);
			}

#if PCL
			var fields = graphType.GetRuntimeFields().Where(x => !x.IsStatic && !x.IsDefined(typeof(NonSerializedAttribute)));
			var properties =
				graphType.GetRuntimeProperties()
					.Where(x => !x.IsStatic() && x.CanWrite && !x.IsDefined(typeof (NonSerializedAttribute)));
#else
			var fields = graphType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(member => !member.IsDefined(typeof (NonSerializedAttribute), false));
			var properties = graphType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(member => member.CanWrite && !member.IsDefined(typeof (NonSerializedAttribute), false));
#endif

			var members = new List<MemberInfo>();
			members.AddRange(fields.Cast<MemberInfo>());
			members.AddRange(properties.Cast<MemberInfo>());

			var values = new Dictionary<string, object>();
			ReadValues(reader, Encoding, values);

			foreach (var member in members) {
				var memberName = member.Name;
				object value;

				if (values.TryGetValue(memberName, out value)) {
					// TODO: convert the source value to the destination value...

					if (member is PropertyInfo) {
						var property = (PropertyInfo) member;
						property.SetValue(obj, value, null);
					} else if (member is FieldInfo) {
						var field = (FieldInfo) member;
						field.SetValue(obj, value);
					}
				}
			}

			return obj;
		}

		private ConstructorInfo GetDefaultConstructor(Type type) {
#if PCL
			return type.GetConstructorOrNull(true);
#else
			var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (var ctor in ctors) {
				if (ctor.GetParameters().Length == 0)
					return ctor;
			}

			return null;
#endif
		}

		private object CustomDeserialize(BinaryReader reader, Type graphType) {
			var ctor = GetSpecialConstructor(graphType);
			if (ctor == null)
				throw new NotSupportedException(String.Format("The type '{0}' has not the special serialization constructor",
					graphType));

			var values = new Dictionary<string, object>();
			ReadValues(reader, Encoding, values);

			var info = new SerializationInfo(graphType, new FormatterConverter());
			foreach (var value in values) {
				var key = value.Key;
				var objValue = value.Value;

				Type valueType = typeof (object);
				if (objValue != null)
					valueType = objValue.GetType();

				info.AddValue(key, objValue, valueType);
			}

			var context = new StreamingContext();

			return ctor.Invoke(new object[] {info, context});
		}

		private static void ReadValues(BinaryReader reader, Encoding encoding, IDictionary<string, object> values) {
			int count = reader.ReadInt32();

			for (int i = 0; i < count; i++) {
				var keyLen = reader.ReadInt32();
				var keyChars = reader.ReadChars(keyLen);
				var key = new string(keyChars);

				var value = ReadValue(reader, encoding);

				values[key] = value;
			}
		}

		private static object ReadValue(BinaryReader reader, Encoding encoding) {
			var typeCode = reader.ReadByte();
			var nullCheck = reader.ReadBoolean();

			if (nullCheck)
				return null;

			if (typeCode == BooleanType)
				return reader.ReadBoolean();
			if (typeCode == ByteType)
				return reader.ReadByte();
			if (typeCode == Int16Type)
				return reader.ReadInt16();
			if (typeCode == Int32Type)
				return reader.ReadInt32();
			if (typeCode == Int64Type)
				return reader.ReadInt64();
			if (typeCode == SingleType)
				return reader.ReadSingle();
			if (typeCode == DoubleType)
				return reader.ReadDouble();
			if (typeCode == StringType)
				return reader.ReadString();
			if (typeCode == ObjectType)
				return ReadObject(reader, encoding);
			if (typeCode == ArrayType)
				return ReadArray(reader, encoding);

			throw new NotSupportedException("Invalid type code in serialization graph");
		}

		private static Type ReadType(BinaryReader reader) {
			var typeString = reader.ReadString();
			return Type.GetType(typeString, true);
		}

		private static object ReadObject(BinaryReader reader, Encoding encoding) {
			var serializer = new BinarySerializer {
				Encoding = encoding
			};

			return serializer.Deserialize(reader);
		}

		private static Array ReadArray(BinaryReader reader, Encoding encoding) {
			var objType = ReadType(reader);
			var arrayLength = reader.ReadInt32();
			var array = Array.CreateInstance(objType, arrayLength);
			for (int i = 0; i < arrayLength; i++) {
				array.SetValue(ReadValue(reader, encoding), i);
			}

			return array;
		}

		private ConstructorInfo GetSpecialConstructor(Type type) {
#if PCL
			return type.GetConstructorOrNull(true, typeof (SerializationInfo), typeof (StreamingContext));
#else
			var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (var ctor in ctors) {
				var paramTypes = ctor.GetParameters().Select(x => x.ParameterType).ToArray();
				if (paramTypes.Length == 2 && 
					paramTypes[0] == typeof(SerializationInfo) && 
					paramTypes[1] == typeof(StreamingContext))
					return ctor;
			}

			return null;
#endif
		}

		public void Serialize(Stream stream, object obj) {
			if (stream == null)
				throw new ArgumentNullException("stream");

			if (!stream.CanWrite)
				throw new ArgumentException("The serialization stream is not writable.");

			var writer = new BinaryWriter(stream, Encoding);
			Serialize(writer, obj);
		}

		public void Serialize(BinaryWriter writer, object obj) {
			if (writer == null)
				throw new ArgumentNullException("writer");
			if (obj == null)
				throw new ArgumentNullException("obj");

			var objType = obj.GetType();

#if !PCL
			if (!Attribute.IsDefined(objType, typeof(SerializableAttribute)))
#else
			if (!objType.GetTypeInfo().IsDefined(typeof(SerializableAttribute)))
#endif
				throw new ArgumentException(String.Format("The type '{0} is not serializable", objType.FullName));

			var graph = new SerializationInfo(objType, new FormatterConverter());
			var context = new StreamingContext();

#if PCL
			if (objType.IsTypeOf(typeof(ISerializable))) {
#else
			if (typeof (ISerializable).IsAssignableFrom(objType)) {
#endif
				((ISerializable) obj).GetObjectData(graph, context);
			} else {
				GetObjectValues(objType, obj, graph);
			}

			SerializeGraph(writer, Encoding, objType, graph);
		}

		private static void GetObjectValues(Type objType, object obj, SerializationInfo graph) {
#if PCL
			var fields =
				objType.GetRuntimeFields()
					.Where(x => !x.IsStatic && !x.IsDefined(typeof (NonSerializedAttribute)) && !x.Name.EndsWith("_BackingField"));
			var properties = objType.GetRuntimeProperties()
				.Where(x => !x.IsStatic() && x.CanWrite && !x.IsDefined(typeof (NonSerializedAttribute)));
#else
			var fields = objType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				.Where(x => !x.IsDefined(typeof (NonSerializedAttribute), false) && !x.Name.EndsWith("_BackingField"));
			var properties = objType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(x => !x.IsDefined(typeof (NonSerializedAttribute), false) && x.CanRead);
#endif

			var members = new List<MemberInfo>();
			members.AddRange(fields.Cast<MemberInfo>());
			members.AddRange(properties.Cast<MemberInfo>());

			foreach (var member in members) {
				var memberName = member.Name;
				Type memberType;

				object value;
				if (member is FieldInfo) {
					value = ((FieldInfo) member).GetValue(obj);
					memberType = ((FieldInfo) member).FieldType;
				} else if (member is PropertyInfo) {
					value = ((PropertyInfo) member).GetValue(obj, null);
					memberType = ((PropertyInfo) member).PropertyType;
				} else {
					throw new NotSupportedException();
				}

				graph.AddValue(memberName, value, memberType);
			}
		}

		private static void SerializeGraph(BinaryWriter writer, Encoding encoding, Type graphType, SerializationInfo graph) {
			var fullName = graphType.AssemblyQualifiedName;

			if (String.IsNullOrEmpty(fullName))
				throw new InvalidOperationException("Could not obtain the assembly qualified name of the type.");

			writer.Write(fullName);

			var count = graph.MemberCount;

			writer.Write(count);

			var en = graph.GetEnumerator();
			while (en.MoveNext()) {
				var key = en.Name;
				var keyLength = key.Length;

				writer.Write(keyLength);
				writer.Write(key.ToCharArray());

				var value = en.Value;
				var objType = en.ObjectType;

#if PCL
				if (objType.IsAbstract() && value != null)
#else
				if ((objType.IsAbstract ||
				     objType.IsInterface) &&
				    value != null)
#endif
					objType = value.GetType();

				SerializeValue(writer, encoding, objType, value);
			}
		}

		private const byte BooleanType = 1;
		private const byte ByteType = 2;
		private const byte Int16Type = 3;
		private const byte Int32Type = 4;
		private const byte Int64Type = 5;
		private const byte SingleType = 6;
		private const byte DoubleType = 7;
		private const byte StringType = 8;
		private const byte ObjectType = 15;
		private const byte ArrayType = 20;

		private static byte? GetTypeCode(Type type) {
			if (type.IsArray)
				return ArrayType;

#if PCL
			if (type.IsPrimitive()) {
#else
			if (type.IsPrimitive) {
#endif
				if (type == typeof(bool))
					return BooleanType;
				if (type == typeof(byte))
					return ByteType;
				if (type == typeof(short))
					return Int16Type;
				if (type == typeof(int))
					return Int32Type;
				if (type == typeof(long))
					return Int64Type;
				if (type == typeof(float))
					return SingleType;
				if (type == typeof(double))
					return DoubleType;
			}

			if (type == typeof (string))
				return StringType;

#if PCL
			if (type.GetTypeInfo().IsDefined(typeof(SerializableAttribute)))
#else
			if (Attribute.IsDefined(type, typeof(SerializableAttribute)))
#endif
				return ObjectType;

			return null;
		}

		private static void WriteValueHead(BinaryWriter writer, byte typeCode, Type type, object value) {
			var nullCheck = value == null;

			writer.Write(typeCode);
			writer.Write(nullCheck);

			if (nullCheck)
				return;

			if (typeCode == ArrayType) {
				var typeString = type.GetElementType().FullName;
				writer.Write(typeString);

				var array = (Array) value;
				var arrayLength = array.Length;

				writer.Write(arrayLength);
			}
		}

		private static void SerializeValue(BinaryWriter writer, Encoding encoding, Type type, object value) {
			var typeCode = GetTypeCode(type);
			if (typeCode == null)
				throw new NotSupportedException(String.Format("The type '{0}' is not supported.", type));

			WriteValueHead(writer, typeCode.Value, type, value);

			if (value == null)
				return;

			if (typeCode == ArrayType) {
				var array = (Array) value;
				var arrayLength = array.Length;
				var arrayType = type.GetElementType();

				for (int i = 0; i < arrayLength; i++) {
					var element = array.GetValue(i);
					SerializeValue(writer, encoding, arrayType, element);
				}
			} else if (typeCode == ObjectType) {
				var serializer = new BinarySerializer {Encoding = encoding};
				serializer.Serialize(writer, value);
			} else if (typeCode == BooleanType) {
				writer.Write((bool) value);
			} else if (typeCode == ByteType) {
				writer.Write((byte) value);
			} else if (typeCode == Int16Type) {
				writer.Write((short) value);
			} else if (typeCode == Int32Type) {
				writer.Write((int) value);
			} else if (typeCode == Int64Type) {
				writer.Write((long) value);
			} else if (typeCode == SingleType) {
				writer.Write((float) value);
			} else if (typeCode == DoubleType) {
				writer.Write((double) value);
			} else if (typeCode == StringType) {
				writer.Write((string) value);
			}
		}
	}
}
