﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ChoETL
{
    public static class ChoDictionaryEx
    {
		public static IEnumerable<KeyValuePair<string, object>> Flatten(this IDictionary<string, object> dict, bool useNestedKeyFormat = true)
		{
			return Flatten(dict, null, useNestedKeyFormat);
		}

		private static IEnumerable<KeyValuePair<string, object>> Flatten(this IList list, string key, bool useNestedKeyFormat = true)
		{
			int index = 0;
			foreach (var item in list)
			{
				if (item is IDictionary<string, object>)
				{
					foreach (var kvp1 in Flatten(item as IDictionary<string, object>, "{0}_{1}".FormatString(key, index++)))
						yield return kvp1;
				}
				else if (item is IList)
				{
					foreach (var kvp1 in Flatten(item as IList, "{0}_{1}".FormatString(key, index++)))
						yield return kvp1;
				}
				else
					yield return new KeyValuePair<string, object>("{0}_{1}".FormatString(key, index++), item);
			}

		}
		private static IEnumerable<KeyValuePair<string, object>> Flatten(this IDictionary<string, object> dict, string key = null, bool useNestedKeyFormat = true)
		{
			foreach (var kvp in dict)
			{
				if (kvp.Value is IDictionary<string, object>)
				{
					foreach (var tuple in Flatten(kvp.Value as IDictionary<string, object>, key == null ? kvp.Key : useNestedKeyFormat ? "{0}_{1}".FormatString(key, kvp.Key) : kvp.Key, useNestedKeyFormat))
						yield return tuple;
				}
				else if (kvp.Value is IList)
				{
					foreach (var tuple in Flatten(kvp.Value as IList, key == null ? kvp.Key : useNestedKeyFormat ? "{0}_{1}".FormatString(key, kvp.Key) : kvp.Key, useNestedKeyFormat))
						yield return tuple;
				}
				else
					yield return new KeyValuePair<string, object>(key == null ? kvp.Key.ToString() : useNestedKeyFormat ? "{0}_{1}".FormatString(key, kvp.Key.ToString()) : kvp.Key.ToString(), kvp.Value);
			}
		}

		public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            ChoGuard.ArgumentNotNull(dict, "Dictionary");

            if (dict.ContainsKey(key))
                dict[key] = value;
            else
                dict.Add(key, value);
        }

        public static bool ContainsKey<TValue>(this IDictionary<string, TValue> dict, string key, bool ignoreCase, CultureInfo culture)
        {
            ChoGuard.ArgumentNotNull(dict, "Dictionary");
            ChoGuard.ArgumentNotNull(culture, "Culture");

            return dict.Keys.Where(i => String.Compare(i, key, ignoreCase, culture) == 0).Any();
        }

        public static void AddOrUpdateValue<TValue>(this IDictionary<string, TValue> dict, string key, TValue value, bool ignoreCase, CultureInfo culture)
        {
            ChoGuard.ArgumentNotNull(dict, "Dictionary");
            ChoGuard.ArgumentNotNull(culture, "Culture");

            string cultureSpecificKeyName = dict.Keys.Where(i => String.Compare(i, key, ignoreCase, culture) == 0).FirstOrDefault();
            if (cultureSpecificKeyName.IsNullOrWhiteSpace())
                dict.Add(cultureSpecificKeyName, value);
            else
                dict[cultureSpecificKeyName] = value;
        }

        public static TValue GetValue<TValue>(this IDictionary<string, TValue> dict, string key, bool ignoreCase, CultureInfo culture, TValue defaultValue = default(TValue))
        {
            ChoGuard.ArgumentNotNull(dict, "Dictionary");
            ChoGuard.ArgumentNotNull(culture, "Culture");

            string cultureSpecificKeyName = dict.Keys.Where(i => String.Compare(i, key, ignoreCase, culture) == 0).FirstOrDefault();
            if (!cultureSpecificKeyName.IsNullOrWhiteSpace())
                return dict[cultureSpecificKeyName];
            else
                return defaultValue;
        }

        public static object ToObject(this IDictionary<string, object> dict, Type type)
        {
            object target = Activator.CreateInstance(type);
            string key = null;
            foreach (var p in ChoType.GetProperties(type))
            {
                if (p.GetCustomAttribute<ChoIgnoreMemberAttribute>() != null)
                    continue;

                key = p.Name;
                var attr = p.GetCustomAttribute<ChoPropertyAttribute>();
                if (attr != null && !attr.Name.IsNullOrWhiteSpace())
                    key = attr.Name.NTrim();

                if (!dict.ContainsKey(key))
                    continue;

                p.SetValue(target, dict[key].CastObjectTo(p.PropertyType));
            }

            return target;
        }

        public static T ToObject<T>(this IDictionary<string, object> source)
            where T : class, new()
        {
            return (T)ToObject(source, typeof(T));
            var someObject = new T();
            var someObjectType = someObject.GetType();

            foreach (var item in source)
            {
                someObjectType
                         .GetProperty(item.Key)
                         .SetValue(someObject, item.Value, null);
            }

            return someObject;
        }

        public static IDictionary<string, object> AsDictionary(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            string key = null;
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach (var p in source.GetType().GetProperties(bindingAttr))
            {
                if (p.GetCustomAttribute<ChoIgnoreMemberAttribute>() != null)
                    continue;

                key = p.Name;
                var attr = p.GetCustomAttribute<ChoPropertyAttribute>();
                if (attr != null && !attr.Name.IsNullOrWhiteSpace())
                    key = attr.Name.NTrim();

                if (dict.ContainsKey(key))
                    continue;

                dict.Add(key, p.GetValue(source, null));
            }
            return dict;
        }
    }
}
