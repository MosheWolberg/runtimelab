// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace TypeLibUtilities.Event
{
    internal class TCEAdapterGenerator
    {
        public static void Process(ModuleBuilder moduleBuilder, IEnumerable<EventItfInfo> eventItfList)
        {
            // Generate the TCE adapters for all the event sources.
            foreach (EventItfInfo curr in eventItfList)
            {
                // Retrieve the information from the event interface info.
                Type EventItfType = curr.GetEventItfType();
                Type SrcItfType = curr.GetSrcItfType();
                string EventProviderName = curr.GetEventProviderName();

                // Generate the sink interface helper.
                Type sinkHelperType = new EventSinkHelperWriter(moduleBuilder, SrcItfType, EventItfType).Perform();

                // Generate the event provider.
                new EventProviderWriter(moduleBuilder, EventProviderName, EventItfType, SrcItfType, sinkHelperType).Perform();
            }
        }

        public static MethodInfo[] GetNonPropertyMethods(Type type)
        {
            MethodInfo[] aMethods = type.GetMethods();
            var methods = new List<MethodInfo>(aMethods);
            foreach (System.Reflection.PropertyInfo prop in type.GetProperties())
            {
                MethodInfo[] accessors = prop.GetAccessors();
                foreach (MethodInfo accessor in accessors)
                {
                    for (int i = 0; i < methods.Count; i++)
                    {
                        if (methods[i] == accessor)
                        {
                            methods.RemoveAt(i);
                        }
                    }
                }
            }

            return methods.ToArray();
        }
    }
}
