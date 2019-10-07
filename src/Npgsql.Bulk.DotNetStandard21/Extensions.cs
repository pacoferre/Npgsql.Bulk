﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Npgsql.Bulk
{
    internal static class Extensions
    {

        public static T GetCustomAttribute<T>(this Type type)
            where T: Attribute
        {
            return type.GetTypeInfo().GetCustomAttribute<T>();
        }

    }
}
