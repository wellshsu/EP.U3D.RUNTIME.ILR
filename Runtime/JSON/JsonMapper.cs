//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.Utils;
using EP.U3D.LIBRARY.JSON;

namespace EP.U3D.RUNTIME.ILR.JSON
{
    public class JsonMapper : LIBRARY.JSON.JsonMapper
    {
        #region Private Methods

        private static void AddArrayMetadata(Type type)
        {
            if (array_metadata.ContainsKey(type))
                return;

            ArrayMetadata data = new ArrayMetadata();

            data.IsArray = type.IsArray;

            if (type.GetInterface("System.Collections.IList") != null)
                data.IsList = true;

            if (type is ILRuntime.Reflection.ILRuntimeWrapperType)
            {
                var wt = (ILRuntime.Reflection.ILRuntimeWrapperType)type;
                if (data.IsArray)
                {
                    data.ElementType = wt.CLRType.ElementType.ReflectionType;
                }
                else
                {
                    data.ElementType = wt.CLRType.GenericArguments[0].Value.ReflectionType;
                }
            }
            else
            {
                foreach (PropertyInfo p_info in type.GetProperties())
                {
                    if (p_info.Name != "Item")
                        continue;

                    ParameterInfo[] parameters = p_info.GetIndexParameters();

                    if (parameters.Length != 1)
                        continue;

                    if (parameters[0].ParameterType == typeof(int))
                        data.ElementType = p_info.PropertyType;
                }
            }

            lock (array_metadata_lock)
            {
                try
                {
                    array_metadata.Add(type, data);
                }
                catch (ArgumentException)
                {
                    return;
                }
            }
        }

        private static void AddObjectMetadata(Type type)
        {
            if (object_metadata.ContainsKey(type))
                return;

            ObjectMetadata data = new ObjectMetadata();

            if (type.GetInterface("System.Collections.IDictionary") != null)
                data.IsDictionary = true;

            data.Properties = new Dictionary<string, PropertyMetadata>();

            //this do for :  int x｛get;private set}   set is null~
            var flag = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            List<PropertyInfo> props = new List<PropertyInfo>();
            Type startType = type;
            while (startType != null)
            {
                props.AddRange(startType.GetProperties(flag));
                startType = startType.BaseType;
            }


            foreach (PropertyInfo p_info in props)
            {
                if (p_info.Name == "Item")
                {
                    ParameterInfo[] parameters = p_info.GetIndexParameters();

                    if (parameters.Length != 1)
                        continue;

                    if (parameters[0].ParameterType == typeof(string))
                    {
                        if (type is ILRuntime.Reflection.ILRuntimeWrapperType)
                        {
                            data.ElementType = ((ILRuntime.Reflection.ILRuntimeWrapperType)type).CLRType
                                .GenericArguments[1].Value.ReflectionType;
                        }
                        else
                            data.ElementType = p_info.PropertyType;
                    }

                    continue;
                }

                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = p_info;
                p_data.Type = p_info.PropertyType;

                data.Properties.Add(p_info.Name, p_data);
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                        BindingFlags.NonPublic);

            foreach (FieldInfo f_info in fields)
            {
                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = f_info;
                p_data.IsField = true;
                p_data.Type = f_info.FieldType;

                data.Properties.Add(f_info.Name, p_data);
            }

            lock (object_metadata_lock)
            {
                try
                {
                    object_metadata.Add(type, data);
                }
                catch (ArgumentException)
                {
                    return;
                }
            }
        }

        private static new object ReadValue(Type inst_type, JsonReader reader)
        {
            reader.Read();

            if (reader.Token == JsonToken.ArrayEnd)
                return null;

            //ILRuntime doesn't support nullable valuetype
            Type underlying_type = inst_type; //Nullable.GetUnderlyingType(inst_type);
            Type value_type = inst_type;

            if (reader.Token == JsonToken.Null)
            {
                if (inst_type.IsClass || underlying_type != null)
                {
                    return null;
                }

                throw new JsonException(String.Format(
                    "Can't assign null to an instance of type {0}",
                    inst_type));
            }

            if (reader.Token == JsonToken.Double ||
                reader.Token == JsonToken.Int ||
                reader.Token == JsonToken.Long ||
                reader.Token == JsonToken.String ||
                reader.Token == JsonToken.Boolean)
            {
                Type json_type = reader.Value.GetType();
                var vt = value_type is ILRuntime.Reflection.ILRuntimeWrapperType
                    ? ((ILRuntime.Reflection.ILRuntimeWrapperType)value_type).CLRType.TypeForCLR
                    : value_type;

                if (vt.IsAssignableFrom(json_type))
                    return reader.Value;
                if (vt is ILRuntime.Reflection.ILRuntimeType && ((ILRuntime.Reflection.ILRuntimeType)vt).ILType.IsEnum)
                {
                    if (json_type == typeof(int) || json_type == typeof(long) || json_type == typeof(short) ||
                        json_type == typeof(byte))
                        return reader.Value;
                }

                // If there's a custom importer that fits, use it
                if (custom_importers_table.ContainsKey(json_type) &&
                    custom_importers_table[json_type].ContainsKey(
                        vt))
                {
                    ImporterFunc importer =
                        custom_importers_table[json_type][vt];

                    return importer(reader.Value);
                }

                // Maybe there's a base importer that works
                if (base_importers_table.ContainsKey(json_type) &&
                    base_importers_table[json_type].ContainsKey(
                        vt))
                {
                    ImporterFunc importer =
                        base_importers_table[json_type][vt];

                    return importer(reader.Value);
                }

                // Maybe it's an enum
                if (vt.IsEnum)
                    return Enum.ToObject(vt, reader.Value);

                // Try using an implicit conversion operator
                MethodInfo conv_op = GetConvOp(vt, json_type);

                if (conv_op != null)
                    return conv_op.Invoke(null,
                        new object[] { reader.Value });

                if (json_type.IsValueType && vt.IsValueType)
                {
                    if (vt == typeof(int))
                    {
                        return Convert.ToInt32(reader.Value);
                    }
                    else if (vt == typeof(float))
                    {
                        return (float)Convert.ToDouble(reader.Value);
                    }
                    else if (vt == typeof(double))
                    {
                        return Convert.ToDouble(reader.Value);
                    }
                }

                // No luck
                throw new JsonException(String.Format(
                    "Can't assign value '{0}' (type {1}) to type {2}",
                    reader.Value, json_type, inst_type));
            }

            object instance = null;

            if (reader.Token == JsonToken.ArrayStart)
            {
                AddArrayMetadata(inst_type);
                ArrayMetadata t_data = array_metadata[inst_type];

                if (!t_data.IsArray && !t_data.IsList)
                    throw new JsonException(String.Format(
                        "Type {0} can't act as an array",
                        inst_type));

                IList list;
                Type elem_type;

                if (!t_data.IsArray)
                {
                    list = (IList)Activator.CreateInstance(inst_type);
                    elem_type = t_data.ElementType;
                }
                else
                {
                    list = new ArrayList();
                    elem_type = inst_type.GetElementType();
                }

                while (true)
                {
                    object item = ReadValue(elem_type, reader);
                    if (item == null && reader.Token == JsonToken.ArrayEnd)
                        break;
                    var rt = elem_type is ILRuntime.Reflection.ILRuntimeWrapperType
                        ? ((ILRuntime.Reflection.ILRuntimeWrapperType)elem_type).RealType
                        : elem_type;
                    item = rt.CheckCLRTypes(item);
                    list.Add(item);
                }

                if (t_data.IsArray)
                {
                    int n = list.Count;
                    instance = Array.CreateInstance(elem_type, n);

                    for (int i = 0; i < n; i++)
                        ((Array)instance).SetValue(list[i], i);
                }
                else
                    instance = list;
            }
            else if (reader.Token == JsonToken.ObjectStart)
            {
                AddObjectMetadata(value_type);
                ObjectMetadata t_data = object_metadata[value_type];
                if (value_type is ILRuntime.Reflection.ILRuntimeType)
                    instance = ((ILRuntime.Reflection.ILRuntimeType)value_type).ILType.Instantiate();
                else
                {
                    if (value_type is ILRuntime.Reflection.ILRuntimeWrapperType)
                        value_type = ((ILRuntime.Reflection.ILRuntimeWrapperType)value_type).RealType;
                    instance = Activator.CreateInstance(value_type);
                }

                while (true)
                {
                    reader.Read();

                    if (reader.Token == JsonToken.ObjectEnd)
                        break;

                    string property = (string)reader.Value;

                    if (t_data.Properties.ContainsKey(property))
                    {
                        PropertyMetadata prop_data =
                            t_data.Properties[property];

                        if (prop_data.IsField)
                        {
                            var p_prop = ((FieldInfo)prop_data.Info);
                            var value = ReadValue(prop_data.Type, reader);
                            p_prop.SetValue(instance, value);
                        }
                        else
                        {
                            var p_info = (PropertyInfo)prop_data.Info;
                            var value = ReadValue(prop_data.Type, reader);
                            p_info.SetValue(instance, value);
                        }
                    }
                    else
                    {
                        if (!t_data.IsDictionary)
                        {
                            if (!reader.SkipNonMembers)
                            {
                                throw new JsonException(String.Format(
                                    "The type {0} doesn't have the " +
                                    "property '{1}'",
                                    inst_type, property));
                            }
                            else
                            {
                                ReadSkip(reader);
                                continue;
                            }
                        }

                        var rt = t_data.ElementType is ILRuntime.Reflection.ILRuntimeWrapperType
                            ? ((ILRuntime.Reflection.ILRuntimeWrapperType)t_data.ElementType).RealType
                            : t_data.ElementType;
                        ((IDictionary)instance).Add(
                            property, rt.CheckCLRTypes(ReadValue(
                                t_data.ElementType, reader)));
                    }
                }
            }

            return instance;
        }

        private static new IJsonWrapper ReadValue(WrapperFactory factory,
            JsonReader reader)
        {
            reader.Read();

            if (reader.Token == JsonToken.ArrayEnd ||
                reader.Token == JsonToken.Null)
                return null;

            IJsonWrapper instance = factory();

            if (reader.Token == JsonToken.String)
            {
                instance.SetString((string)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Double)
            {
                instance.SetDouble((double)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Int)
            {
                instance.SetInt((int)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Long)
            {
                instance.SetLong((long)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Boolean)
            {
                instance.SetBoolean((bool)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.ArrayStart)
            {
                instance.SetJsonType(JsonType.Array);

                while (true)
                {
                    IJsonWrapper item = ReadValue(factory, reader);
                    if (item == null && reader.Token == JsonToken.ArrayEnd)
                        break;

                    ((IList)instance).Add(item);
                }
            }
            else if (reader.Token == JsonToken.ObjectStart)
            {
                instance.SetJsonType(JsonType.Object);

                while (true)
                {
                    reader.Read();

                    if (reader.Token == JsonToken.ObjectEnd)
                        break;

                    string property = (string)reader.Value;

                    ((IDictionary)instance)[property] = ReadValue(
                        factory, reader);
                }
            }

            return instance;
        }

        private static void WriteValue(object obj, JsonWriter writer,
            bool writer_is_private,
            int depth)
        {
            if (depth > max_nesting_depth)
                throw new JsonException(
                    String.Format("Max allowed object depth reached while " +
                                  "trying to export from type {0}",
                        obj.GetType()));

            if (obj == null)
            {
                writer.Write(null);
                return;
            }

            if (obj is IJsonWrapper)
            {
                if (writer_is_private)
                    writer.TextWriter.Write(((IJsonWrapper)obj).ToJson());
                else
                    ((IJsonWrapper)obj).ToJson(writer);

                return;
            }

            if (obj is String)
            {
                writer.Write((string)obj);
                return;
            }

            if (obj is Single)
            {
                writer.Write((float)obj);

                return;
            }

            if (obj is Double)
            {
                writer.Write((double)obj);
                return;
            }

            if (obj is Int32)
            {
                writer.Write((int)obj);
                return;
            }

            if (obj is Boolean)
            {
                writer.Write((bool)obj);
                return;
            }

            if (obj is Int64)
            {
                writer.Write((long)obj);
                return;
            }

            if (obj is Array)
            {
                writer.WriteArrayStart();

                foreach (object elem in (Array)obj)
                    WriteValue(elem, writer, writer_is_private, depth + 1);

                writer.WriteArrayEnd();

                return;
            }

            if (obj is IList)
            {
                writer.WriteArrayStart();
                foreach (object elem in (IList)obj)
                    WriteValue(elem, writer, writer_is_private, depth + 1);
                writer.WriteArrayEnd();

                return;
            }

            if (obj is IDictionary)
            {
                writer.WriteObjectStart();
                foreach (DictionaryEntry entry in (IDictionary)obj)
                {
                    writer.WritePropertyName((string)entry.Key);
                    WriteValue(entry.Value, writer, writer_is_private,
                        depth + 1);
                }

                writer.WriteObjectEnd();

                return;
            }

            Type obj_type;
            if (obj is ILRuntime.Runtime.Intepreter.ILTypeInstance)
            {
                obj_type = ((ILRuntime.Runtime.Intepreter.ILTypeInstance)obj).Type.ReflectionType;
            }
            else if (obj is ILRuntime.Runtime.Enviorment.CrossBindingAdaptorType)
            {
                obj_type = ((ILRuntime.Runtime.Enviorment.CrossBindingAdaptorType)obj).ILInstance.Type.ReflectionType;
            }
            else
                obj_type = obj.GetType();

            // See if there's a custom exporter for the object
            if (custom_exporters_table.ContainsKey(obj_type))
            {
                ExporterFunc exporter = custom_exporters_table[obj_type];
                exporter(obj, writer);

                return;
            }

            // If not, maybe there's a base exporter
            if (base_exporters_table.ContainsKey(obj_type))
            {
                ExporterFunc exporter = base_exporters_table[obj_type];
                exporter(obj, writer);

                return;
            }

            // Last option, let's see if it's an enum
            if (obj is Enum)
            {
                Type e_type = Enum.GetUnderlyingType(obj_type);

                if (e_type == typeof(long)
                    || e_type == typeof(uint)
                    || e_type == typeof(ulong))
                    writer.Write((ulong)obj);
                else
                    writer.Write((int)obj);

                return;
            }

            // Okay, so it looks like the input should be exported as an
            // object
            AddTypeProperties(obj_type);
            IList<PropertyMetadata> props = type_properties[obj_type];

            writer.WriteObjectStart();
            foreach (PropertyMetadata p_data in props)
            {
                if (p_data.IsField)
                {
                    writer.WritePropertyName(p_data.Info.Name);
                    WriteValue(((FieldInfo)p_data.Info).GetValue(obj),
                        writer, writer_is_private, depth + 1);
                }
                else
                {
                    PropertyInfo p_info = (PropertyInfo)p_data.Info;

                    if (p_info.CanRead)
                    {
                        writer.WritePropertyName(p_data.Info.Name);
                        WriteValue(p_info.GetValue(obj, null),
                            writer, writer_is_private, depth + 1);
                    }
                }
            }

            writer.WriteObjectEnd();
        }

        #endregion

        #region Public Methods
        public static new string ToJson(object obj, bool isformat = false)
        {
            if (isformat)
            {
                var jw = new JsonWriter() { IndentValue = 2, PrettyPrint = true };
                ToJson(obj, jw);
                return jw.ToString();
            }


            lock (static_writer_lock)
            {
                static_writer.Reset();

                WriteValue(obj, static_writer, true, 0);

                return static_writer.ToString();
            }
        }

        public static new void ToJson(object obj, JsonWriter writer)
        {
            WriteValue(obj, writer, false, 0);
        }

        public static new JsonData ToObject(JsonReader reader)
        {
            return (JsonData)ToWrapper(
                delegate { return new JsonData(); }, reader);
        }

        public static new JsonData ToObject(TextReader reader)
        {
            JsonReader json_reader = new JsonReader(reader);

            return (JsonData)ToWrapper(
                delegate { return new JsonData(); }, json_reader);
        }

        public static new JsonData ToObject(string json)
        {
            return (JsonData)ToWrapper(
                delegate { return new JsonData(); }, json);
        }

        public static new T ToObject<T>(JsonReader reader)
        {
            return (T)ReadValue(typeof(T), reader);
        }

        public static new T ToObject<T>(TextReader reader)
        {
            JsonReader json_reader = new JsonReader(reader);

            return (T)ReadValue(typeof(T), json_reader);
        }

        public static new T ToObject<T>(string json)
        {
            JsonReader reader = new JsonReader(json);

            return (T)ReadValue(typeof(T), reader);
        }

        /// <summary>
        /// Extension by xiaofan
        /// </summary>
        /// <param name="t"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public static new object ToObject(Type t, string json)
        {
            JsonReader reader = new JsonReader(json);

            return ReadValue(t, reader);
        }

        public static new IJsonWrapper ToWrapper(WrapperFactory factory,
            JsonReader reader)
        {
            return ReadValue(factory, reader);
        }

        public static new IJsonWrapper ToWrapper(WrapperFactory factory,
            string json)
        {
            JsonReader reader = new JsonReader(json);

            return ReadValue(factory, reader);
        }
        #endregion

        #region ILR Methods
        public unsafe static void RegisterCLRRedirection(ILRuntime.Runtime.Enviorment.AppDomain appdomain)
        {
            foreach (var i in typeof(JsonMapper).GetMethods())
            {
                if (i.Name == "ToObject" && i.IsGenericMethodDefinition)
                {
                    var param = i.GetParameters();
                    if (param[0].ParameterType == typeof(string))
                    {
                        appdomain.RegisterCLRMethodRedirection(i, JsonToObject);
                    }
                    else if (param[0].ParameterType == typeof(JsonReader))
                    {
                        appdomain.RegisterCLRMethodRedirection(i, JsonToObject2);
                    }
                    else if (param[0].ParameterType == typeof(TextReader))
                    {
                        appdomain.RegisterCLRMethodRedirection(i, JsonToObject3);
                    }
                }
            }
        }

        public unsafe static StackObject* JsonToObject(ILIntepreter intp, StackObject* esp, IList<object> mStack,
            CLRMethod method, bool isNewObj)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = intp.AppDomain;
            StackObject* ptr_of_this_method;
            StackObject* __ret = ILIntepreter.Minus(esp, 1);
            ptr_of_this_method = ILIntepreter.Minus(esp, 1);
            System.String json =
                (System.String)typeof(System.String).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain,
                    mStack));
            intp.Free(ptr_of_this_method);
            var type = method.GenericArguments[0].ReflectionType;
            var result_of_this_method = ReadValue(type, new JsonReader(json));

            return ILIntepreter.PushObject(__ret, mStack, result_of_this_method);
        }

        public unsafe static StackObject* JsonToObject2(ILIntepreter intp, StackObject* esp, IList<object> mStack,
            CLRMethod method, bool isNewObj)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = intp.AppDomain;
            StackObject* ptr_of_this_method;
            StackObject* __ret = ILIntepreter.Minus(esp, 1);
            ptr_of_this_method = ILIntepreter.Minus(esp, 1);
            JsonReader json =
                (JsonReader)typeof(JsonReader).CheckCLRTypes(
                    StackObject.ToObject(ptr_of_this_method, __domain, mStack));
            intp.Free(ptr_of_this_method);
            var type = method.GenericArguments[0].ReflectionType;
            var result_of_this_method = ReadValue(type, json);

            return ILIntepreter.PushObject(__ret, mStack, result_of_this_method);
        }

        public unsafe static StackObject* JsonToObject3(ILIntepreter intp, StackObject* esp, IList<object> mStack,
            CLRMethod method, bool isNewObj)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = intp.AppDomain;
            StackObject* ptr_of_this_method;
            StackObject* __ret = ILIntepreter.Minus(esp, 1);
            ptr_of_this_method = ILIntepreter.Minus(esp, 1);
            TextReader json =
                (TextReader)typeof(TextReader).CheckCLRTypes(
                    StackObject.ToObject(ptr_of_this_method, __domain, mStack));
            intp.Free(ptr_of_this_method);
            var type = method.GenericArguments[0].ReflectionType;
            var result_of_this_method = ReadValue(type, new JsonReader(json));

            return ILIntepreter.PushObject(__ret, mStack, result_of_this_method);
        }
        #endregion
    }
}