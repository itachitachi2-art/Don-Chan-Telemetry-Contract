using System;
using System.Collections;
using System.Reflection;

namespace DonChan.ZombieRadar
{
    internal static class ReflectionHelper
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static IEnumerable ReadEnumerable(object target, params string[] names)
        {
            object value = Read(target, names);
            return value as IEnumerable;
        }

        internal static object Read(object target, params string[] names)
        {
            if (target == null) return null;
            Type t = target.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                PropertyInfo p = t.GetProperty(name, Flags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    try { return p.GetValue(target, null); } catch { }
                }
                FieldInfo f = t.GetField(name, Flags);
                if (f != null)
                {
                    try { return f.GetValue(target); } catch { }
                }
            }
            return null;
        }
    }
}
