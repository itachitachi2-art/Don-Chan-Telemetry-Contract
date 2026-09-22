using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DonChan.TelemetryProbe
{
    internal static class ReflectionUtil
    {
        public const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags All = AllInstance | AllStatic;

        public static Type FindType(string fullOrShortName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var exact = asm.GetType(fullOrShortName, false);
                    if (exact != null) return exact;
                    var match = asm.GetTypes().FirstOrDefault(t => t.Name == fullOrShortName);
                    if (match != null) return match;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var match = ex.Types.Where(t => t != null).FirstOrDefault(t => t.Name == fullOrShortName || t.FullName == fullOrShortName);
                    if (match != null) return match;
                }
                catch { }
            }
            return null;
        }

        public static object ReadMember(object target, params string[] names)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            BindingFlags flags = instance == null ? AllStatic : AllInstance;
            foreach (string name in names)
            {
                try
                {
                    var p = type.GetProperty(name, flags);
                    if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(instance, null);
                }
                catch { }
                try
                {
                    var f = type.GetField(name, flags);
                    if (f != null) return f.GetValue(instance);
                }
                catch { }
            }
            return null;
        }

        public static object InvokeNoArgs(object target, params string[] methodNames)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            BindingFlags flags = instance == null ? AllStatic : AllInstance;
            foreach (string name in methodNames)
            {
                try
                {
                    var m = type.GetMethods(flags).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 0 && !x.ContainsGenericParameters);
                    if (m != null) return m.Invoke(instance, null);
                }
                catch { }
            }
            return null;
        }

        public static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName)) return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            BindingFlags flags = instance == null ? AllStatic : AllInstance;
            object[] actualArgs = args ?? new object[0];
            MethodInfo[] methods;
            try { methods = type.GetMethods(flags).Where(x => x.Name == methodName && !x.ContainsGenericParameters).ToArray(); }
            catch { return null; }
            foreach (MethodInfo method in methods)
            {
                ParameterInfo[] parameters;
                try { parameters = method.GetParameters(); } catch { continue; }
                if (parameters.Length != actualArgs.Length) continue;
                bool compatible = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (actualArgs[i] == null) continue;
                    Type expected = parameters[i].ParameterType;
                    if (expected.IsByRef) expected = expected.GetElementType();
                    if (expected != null && !expected.IsInstanceOfType(actualArgs[i])) { compatible = false; break; }
                }
                if (!compatible) continue;
                try { return method.Invoke(instance, actualArgs); } catch { }
            }
            return null;
        }

        public static string TypeName(object value) { return value == null ? null : value.GetType().FullName; }
        public static int ObjectId(object value) { return value == null ? 0 : RuntimeHelpers.GetHashCode(value); }

        public static object Inspect(object value, int depth, int maxMembers, int maxCollectionItems)
        {
            return InspectInternal(value, depth, maxMembers, maxCollectionItems, new HashSet<int>());
        }

        private static object InspectInternal(object value, int depth, int maxMembers, int maxCollectionItems, HashSet<int> visited)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (IsScalar(type)) return Scalar(value);

            int id = ObjectId(value);
            if (!type.IsValueType)
            {
                if (visited.Contains(id)) return new Dictionary<string, object> { { "$ref", id }, { "$type", type.FullName } };
                visited.Add(id);
            }

            if (value is IEnumerable && !(value is string))
            {
                var list = new List<object>();
                int n = 0;
                try
                {
                    foreach (var item in (IEnumerable)value)
                    {
                        if (n++ >= maxCollectionItems) { list.Add("<truncated>"); break; }
                        list.Add(depth > 0 ? InspectInternal(item, depth - 1, maxMembers, maxCollectionItems, visited) : Summary(item));
                    }
                }
                catch (Exception ex) { list.Add("<enumeration-error:" + ex.GetType().Name + ">"); }
                return new Dictionary<string, object> { { "$type", type.FullName }, { "$id", id }, { "items", list } };
            }

            var result = new Dictionary<string, object>();
            result["$type"] = type.FullName;
            result["$id"] = id;
            if (depth <= 0) return result;

            int count = 0;
            foreach (var field in SafeFields(type))
            {
                if (count++ >= maxMembers) { result["$truncated"] = true; break; }
                try
                {
                    object memberValue = field.GetValue(value);
                    result["field:" + field.Name] = InspectInternal(memberValue, depth - 1, maxMembers, maxCollectionItems, visited);
                }
                catch (Exception ex) { result["field:" + field.Name] = "<error:" + ex.GetType().Name + ">"; }
            }

            // Public parameterless properties are useful, but intentionally limited after fields to reduce side effects.
            foreach (var prop in SafeProperties(type))
            {
                if (count++ >= maxMembers) { result["$truncated"] = true; break; }
                string key = "property:" + prop.Name;
                if (result.ContainsKey(key)) continue;
                try
                {
                    object memberValue = prop.GetValue(value, null);
                    result[key] = InspectInternal(memberValue, depth - 1, maxMembers, maxCollectionItems, visited);
                }
                catch (Exception ex) { result[key] = "<error:" + ex.GetType().Name + ">"; }
            }
            return result;
        }

        private static IEnumerable<FieldInfo> SafeFields(Type type)
        {
            FieldInfo[] fields;
            try { fields = type.GetFields(AllInstance); } catch { return new FieldInfo[0]; }
            return fields.Where(f => !f.IsStatic && !typeof(Delegate).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name);
        }

        private static IEnumerable<PropertyInfo> SafeProperties(Type type)
        {
            PropertyInfo[] props;
            try { props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public); } catch { return new PropertyInfo[0]; }
            return props.Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetMethod != null && !p.GetMethod.IsStatic).OrderBy(p => p.Name).Take(32);
        }

        private static object Summary(object value)
        {
            if (value == null) return null;
            Type t = value.GetType();
            if (IsScalar(t)) return Scalar(value);
            return new Dictionary<string, object> { { "$type", t.FullName }, { "$id", ObjectId(value) } };
        }

        private static bool IsScalar(Type t)
        {
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);
        }

        private static object Scalar(object value)
        {
            try { return value is Enum ? value.ToString() : value; }
            catch { return "<unprintable>"; }
        }
    }
}
