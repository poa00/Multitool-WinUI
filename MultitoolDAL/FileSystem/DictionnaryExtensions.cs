﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multitool.DAL.FileSystem
{
    internal static class DictionnaryExtensions
    {
        public static bool ContainsKeyInvariant(this Dictionary<string, FileSystemCache> source, string key)
        {
            foreach (KeyValuePair<string, FileSystemCache> pair in source)
            {
                if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static FileSystemCache Get(this Dictionary<string, FileSystemCache> source, string key)
        {
            if (source.TryGetValue(key, out FileSystemCache sysCache))
            {
                return sysCache;
            }
            else
            {
                foreach (KeyValuePair<string, FileSystemCache> pair in source)
                {
                    if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }
            return null;
        }
    }
}