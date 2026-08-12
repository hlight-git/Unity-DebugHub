using IngameDebugConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Hlight.Debug.Hub
{
    internal static class Executor
    {
        readonly static Dictionary<string, object> registry = new();
        const string DOT_OUTSIDE_BRACKETS_PATTERN = @"\.(?![^\[\](){}]*[\]\)}])";
        const string OPEN_SQUARE_BRACKET_PATTERN = @"(?=\[)";

        public abstract class Entry
        {
            public Type Type { get; set; }
            public object Value { get; set; }

            public static bool IsMethod(string entry) => entry.Contains('(');
            public static bool IsIndexer(string entry) => entry.StartsWith('[');
            public static Entry From(Type sourceType, object source, string entry)
            {
                if (IsMethod(entry))
                {
                    return new MethodEntry(sourceType, source, entry);
                }
                if (IsIndexer(entry))
                {
                    return new IndexerEntry(sourceType, source, entry);
                }
                return new PropertyEntry(sourceType, source, entry);
            }

            public Entry GetNext(string nextEntry)
            {
                if (Value == null)
                {
                    throw new Exception($"Cannot query to \"{nextEntry}\" because source object is null.");
                }
                return From(Type, Value, nextEntry);
            }
        }

        class PropertyEntry : Entry
        {
            public PropertyEntry(Type sourceType, object source, string memberName)
            {
                Value = sourceType.GetValueOfFieldOrPropertyRecursive(source, memberName, out var memberInfoType);
                if (memberInfoType == null) throw new Exception($"Not found field (or property) \"{memberName}\" in `{sourceType.Name}`.");

                Type = Value != null ? Value.GetType() : memberInfoType;
            }
        }

        class IndexerEntry : Entry
        {
            public PropertyInfo Indexer { get; private set; }
            
            public IndexerEntry(Type sourceType, object source, string entry)
            {
                List<string> stringArgs = new();
                DebugLogConsole.FetchArgumentsFromCommand(SubStringBetween(entry, '[', ']'), stringArgs);
                int paramCount = stringArgs.Count;

                if (paramCount == 0)
                {
                    throw new Exception("Number of arguments cannot be zero.");
                }

                List<PropertyInfo> sameNumberOfParamIndexers = new();

                sourceType.AddIndexersRecursive(sameNumberOfParamIndexers, p => p.GetIndexParameters().Length == paramCount);

                int indexerCount = sameNumberOfParamIndexers.Count();

                if (indexerCount == 0)
                {
                    throw new Exception($"No indexers with {paramCount} parameters were found.");
                }
                else if (indexerCount == 1)
                {
                    Indexer = sameNumberOfParamIndexers[0];
                }
                else if (entry.Contains('{') && entry.Contains('}'))
                {
                    Indexer = sameNumberOfParamIndexers[int.Parse(SubStringBetween(entry, '{', '}'))];
                }
                else
                {
                    throw new Exception($"There is more than one indexer needs {paramCount} parameter(s):\n-{string.Join("\n-", sameNumberOfParamIndexers)}" +
                        $"\n.Use braces to select the one you want. For example: object[$x, $y]{{2}}.");
                }

                ParameterInfo[] parameterInfos = Indexer.GetIndexParameters();
                Value = Indexer.GetValue(source, Enumerable.Range(0, paramCount).Select(i => ParseObject(stringArgs[i], parameterInfos[i].ParameterType)).ToArray());
                Type = Value != null ? Value.GetType() : Indexer.PropertyType;
            }
        }

        class MethodEntry : Entry
        {
            public MethodEntry(Type sourceType, object source, string entry)
            {
                List<string> stringArgs = new();
                DebugLogConsole.FetchArgumentsFromCommand(entry[(entry.IndexOf('(') + 1)..^1], stringArgs);

                int paramCount = stringArgs.Count;

                StringBuilder methodNameBuilder = new();

                for (int i = 0; i < entry.Length; i++)
                {
                    if (entry[i] == '<' || entry[i] == '{' || entry[i] == '(')
                    {
                        break;
                    }
                    methodNameBuilder.Append(entry[i]);
                }

                string methodName = methodNameBuilder.ToString();

                MethodInfo method;
                List<MethodInfo> overloads = new();

                sourceType.AddMethodsRecursive(overloads, m => m.Name == methodName && paramCount == m.GetParameters().Length);

                if (overloads.Count == 0)
                {
                    throw new Exception($"No methods found that match with \"{entry}\"");
                }
                else if (overloads.Count == 1)
                {
                    method = overloads[0];
                }
                else if (entry.Contains('{') && entry.Contains('}')) // isOverloadedMethod
                {
                    method = overloads[int.Parse(SubStringBetween(entry, '{', '}'))];
                }
                else
                {
                    throw new Exception($"There is more than one method with named '{methodName}' and needs {paramCount} parameter(s):\n-{string.Join("\n-", overloads)}." +
                        $"\nUse braces to select the one you want. For example: Method{{0}}(args).");
                }

                if (entry.Contains('<') && entry.Contains('>')) // isGenericMethod
                {
                    List<string> buffer = new();
                    DebugLogConsole.FetchArgumentsFromCommand(SubStringBetween(entry, '<', '>'), buffer);
                    method = method.MakeGenericMethod(buffer.Select(a => GetRegisteredObject(a.Substring(1))).Cast<Type>().ToArray());
                }
                else if (method.IsGenericMethod)
                {
                    throw new Exception($"The matched method is a generic method: {method}.\nPlease pass the type parameter according to the syntax: Method<$T>.");
                }

                ParameterInfo[] parameterInfos = method.GetParameters();

                Value = method.Invoke(source, Enumerable.Range(0, parameterInfos.Length).Select(i => ParseObject(stringArgs[i], parameterInfos[i].ParameterType)).ToArray());
                Type = Value != null ? Value.GetType() : method.ReturnType;
            }
        }

        /// Lấy đoạn giữa cặp ký tự đầu tiên. Không xử lý lồng nhau: "a[b[0]]" trả về "b[0",
        /// nên query lồng ngoặc phải qua registry ($x) thay vì viết trực tiếp.
        /// ponytail: nâng lên parser thật khi nào thực sự cần query lồng.
        static string SubStringBetween(string str, char head, char tail)
        {
            int startIndex = str.IndexOf(head) + 1;
            int endIndex = str.IndexOf(tail, startIndex);
            if (startIndex == 0 || endIndex < 0)
            {
                throw new Exception($"\"{str}\" is missing a matching '{head}' ... '{tail}' pair.");
            }
            return str.Substring(startIndex, endIndex - startIndex);
        }

        internal static object ParseObject(string str, Type type)
        {
            if (str == "null") return null;
            if (str.StartsWith('$')) return GetRegisteredObject(str.Substring(1));
            if (type == null) throw new Exception($"Cannot parse argument '{str}' because the target type is unknown.");
            if (DebugLogConsole.ParseArgument(str, type, out object output)) return output;
            throw new Exception($"Cannot parse argument '{str}' to type '{type.FullName}'");
        }

        static object Get(Assembly assembly, string typeName, string[] entryRequest)
        {
            Type type = GetType(assembly, typeName);

            Entry curEntry = Entry.From(type, null, entryRequest[0]);

            for (int i = 1; i < entryRequest.Length; i++)
            {
                curEntry = curEntry.GetNext(entryRequest[i]);
            }

            return curEntry.Value;
        }

        static string[] GetEntryRequest(string query)
        {
            return Regex.Split(query, DOT_OUTSIDE_BRACKETS_PATTERN).SelectMany(s => Regex.Split(s, OPEN_SQUARE_BRACKET_PATTERN)).ToArray();
        }

        public static void Bind(string key, object value)
        {
            registry[key] = value;
        }

        public static Type GetType(Assembly assembly, string typeName)
        {
            if (assembly == null)
            {
                throw new Exception($"Assembly not found!");
            }

            if (typeName.Contains('<') && typeName.Contains('>')) // isGenericType
            {
                List<string> buffer = new();
                DebugLogConsole.FetchArgumentsFromCommand(SubStringBetween(typeName, '<', '>'), buffer);
                typeName = typeName.Substring(0, typeName.IndexOf('<')) + $"`{buffer.Count}";
                Type type = assembly.GetType(typeName) ?? throw new Exception($"{assembly.GetName().Name} do not contains type \"{typeName}\". Are you missing a namespace?");
                return type.MakeGenericType(buffer.Select(a => GetRegisteredObject(a.Substring(1))).Cast<Type>().ToArray());
            }
            return assembly.GetType(typeName) ?? throw new Exception($"{assembly.GetName().Name} do not contains type \"{typeName}\". Are you missing a namespace?");
        }

        public static object GetRegisteredObject(string key)
        {
            if (registry.TryGetValue(key, out var value)) return value;
            throw new Exception($"\"{key}\" is not registered!");
        }

        /// Registry giữ object của session trước khi bật "Enter Play Mode without domain reload",
        /// nên phải xoá như IngameDebugConsole làm với danh sách command của nó.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registry.Clear();
        }

        public static object Get(Assembly assembly, string typeName, string query)
        {
            return Get(assembly, typeName, GetEntryRequest(query));
        }

        public static void Set(Assembly assembly, string typeName, string query, string value)
        {
            string[] entryRequest = GetEntryRequest(query);
            string lastEntry = entryRequest[^1];

            if (entryRequest.Length == 1)
            {
                if (Entry.IsIndexer(lastEntry))
                {
                    throw new Exception($"An indexer needs an object in front of it. For example: \"Items{lastEntry}\".");
                }

                // Query một tầng = member static của chính type đó: set trực tiếp trên type với source null.
                // (Đi qua Get() sẽ trả về *giá trị* của member rồi tìm member đó trên chính giá trị đó.)
                Type staticOwner = GetType(assembly, typeName);
                staticOwner.SetMemberValue(null, lastEntry, ParseObject(value, MemberTypeOf(staticOwner, lastEntry)));
                return;
            }

            object source = Get(assembly, typeName, entryRequest[..^1]);
            if (source == null)
            {
                throw new Exception($"Cannot set \"{lastEntry}\" because the object in front of it is null.");
            }

            if (Entry.IsIndexer(lastEntry))
            {
                string entry = lastEntry;
                List<string> stringArgs = new();
                DebugLogConsole.FetchArgumentsFromCommand(SubStringBetween(entry, '[', ']'), stringArgs);
                int paramCount = stringArgs.Count;

                if (paramCount == 0)
                {
                    throw new Exception("Number of arguments cannot be zero.");
                }

                MethodInfo setter;

                List<MethodInfo> matchSetters = new();
                source.GetType().AddMethodsRecursive(matchSetters, m => m.Name == "set_Item" && m.GetParameters().Length == paramCount + 1);

                int matchCount = matchSetters.Count();

                if (matchCount == 0)
                {
                    throw new Exception($"No indexers with {paramCount} parameters were found.");
                }
                else if (matchCount == 1)
                {
                    setter = matchSetters[0];
                }
                else if (entry.Contains('{') && entry.Contains('}'))
                {
                    setter = matchSetters[int.Parse(SubStringBetween(entry, '{', '}'))];
                }
                else
                {
                    throw new Exception($"There is more than one indexer needs {paramCount} parameter(s):\n-{string.Join("\n-", matchSetters)}" +
                        $"\n.Use braces to select the one you want. For example: object[$x, $y]{{2}}.");
                }

                ParameterInfo[] parameterInfos = setter.GetParameters();
                object[] args = new object[parameterInfos.Length];

                for (int i = 0; i < paramCount; i++)
                {
                    args[i] = ParseObject(stringArgs[i], parameterInfos[i].ParameterType);
                }

                args[paramCount] = ParseObject(value, parameterInfos[paramCount].ParameterType);

                setter.Invoke(source, args);
                return;
            }

            Type owner = source.GetType();

            // Struct lấy ra từ query là bản copy đã boxing: ghi vào đây thì object gốc không đổi.
            // ponytail: báo lỗi rõ, không viết cơ chế write-back ngược cả chuỗi query cho một debug tool.
            if (owner.IsValueType)
            {
                throw new Exception($"\"{lastEntry}\" belongs to struct `{owner.Name}`, which was copied when it was read, " +
                    "so writing to it would be lost. Set the whole struct or call a method on the owner instead.");
            }

            owner.SetMemberValue(source, lastEntry, ParseObject(value, MemberTypeOf(owner, lastEntry)));
        }

        static Type MemberTypeOf(Type owner, string memberName)
        {
            return owner.GetMemberType(memberName)
                ?? throw new Exception($"Not found field (or property) \"{memberName}\" in `{owner.Name}`.");
        }
    }
}