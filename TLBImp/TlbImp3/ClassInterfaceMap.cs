// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Represents collected mapping information for default interface & event interface to coclass / class interfaces 
    /// </summary>
    internal class ClassInterfaceMap
    {
        private readonly IEnumerable<DefaultInterfaceInfo> defaultInterfaceInfos;

        public static ClassInterfaceMap Create(ConverterInfo info)
        {
            IEnumerable<DefaultInterfaceInfo> defIntInfos = Collect(info);
            CreateLocalClassInterfaces(info, defIntInfos);

            return new ClassInterfaceMap(defIntInfos);
        }

        private ClassInterfaceMap(IEnumerable<DefaultInterfaceInfo> defaultInterfaceInfos)
        {
            this.defaultInterfaceInfos = defaultInterfaceInfos;
        }

        public bool TryGetExclusiveDefaultInterfaceForCoclass(TypeInfo coclassTypeInfo, out TypeInfo exclusiveDefaultInterfaceTypeInfo)
        {
            string expectedName = coclassTypeInfo.GetDocumentation();
            foreach (DefaultInterfaceInfo defaultInterfaceInfo in this.defaultInterfaceInfos)
            {
                if (defaultInterfaceInfo.IsExclusive
                    && defaultInterfaceInfo.CoClassName == expectedName)
                {
                    exclusiveDefaultInterfaceTypeInfo = defaultInterfaceInfo.DefaultInterface;
                    return true;
                }
            }

            exclusiveDefaultInterfaceTypeInfo = null;
            return false;
        }

        public bool TryGetCoClassForExclusiveDefaultInterface(TypeInfo interfaceTypeInfo, out TypeInfo coclassTypeInfo)
        {
            string expectedName = interfaceTypeInfo.GetDocumentation();
            foreach (DefaultInterfaceInfo defaultInterfaceInfo in this.defaultInterfaceInfos)
            {
                if (defaultInterfaceInfo.IsExclusive
                    && defaultInterfaceInfo.DefaultInterfaceName == expectedName)
                {
                    coclassTypeInfo = defaultInterfaceInfo.CoClass;
                    return true;
                }
            }

            coclassTypeInfo = null;
            return false;
        }

        /// <summary>
        /// Collect CoClass details
        /// </summary>
        private static IEnumerable<DefaultInterfaceInfo> Collect(ConverterInfo info)
        {
            var defaultInterfaceInfos = new List<DefaultInterfaceInfo>();

            // Remember all the interface name to coclass name mapping
            var interfaceToCoClassMapping = new Dictionary<string, string>();

            // For every coclass
            int typeInfoCount = info.TypeLib.GetTypeInfoCount();
            for (int i = 0; i < typeInfoCount; ++i)
            {
                TypeInfo type = info.TypeLib.GetTypeInfo(i);

                TypeAttr attr = type.GetTypeAttr();
                if (attr.Typekind != TYPEKIND.TKIND_COCLASS)
                {
                    continue;
                }

                var defaultInterfaceInfo = new DefaultInterfaceInfo()
                {
                    CoClass = type
                };

                // Walk the list of implemented interfaces
                bool skippedUnknownOrDispatch = false;
                for (int j = 0; j < attr.ImplTypesCount; ++j)
                {
                    TypeInfo typeImpl = type.GetRefType(j);
                    TypeAttr attrImpl = typeImpl.GetTypeAttr();

                    IMPLTYPEFLAGS flags = type.GetImplTypeFlags(j);
                    bool isDefault = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULT);
                    bool isSource = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE);

                    // For invalid default interfaces, such as 
                    // coclass MyObj
                    // {
                    //     [default] interface IUnknown;
                    //     interface IA;
                    // }
                    // to use the first valid interface, which is IA;

                    // Skip IUnknown & IDispatch
                    if (attrImpl.Guid == WellKnownGuids.IID_IDispatch
                        || attrImpl.Guid == WellKnownGuids.IID_IUnknown)
                    {
                        skippedUnknownOrDispatch = (!isSource && isDefault);
                        continue;
                    }

                    // Skip non-dispatch interfaces that don't derive from IUnknown
                    if (!attrImpl.IsIDispatch && !ConvCommon.IsDerivedFromIUnknown(typeImpl))
                    {
                        continue;
                    }

                    if (isSource)
                    {
                        // Default source interface
                        // If explicitly stated as default, use that
                        // otherwise, try to use the first one
                        if (isDefault
                            || defaultInterfaceInfo.DefaultSourceInterface == null)
                        {
                            defaultInterfaceInfo.DefaultSourceInterface = typeImpl;
                        }
                    }
                    else
                    {
                        // Default interface
                        // If explicitly stated as default, use that
                        // otherwise, try to use the first one
                        if (isDefault
                            || defaultInterfaceInfo.DefaultInterface == null) 
                        {
                            defaultInterfaceInfo.DefaultInterface = typeImpl;
                        }
                    }
                }

                // The logic that populates interfaceToCoClassMapping broke unintentionally in Dev10. We keep the
                // Dev10 logic by default and revert to the original one if the Legacy35 compat switch is specified.

                if (info.Settings.IsUseLegacy35QuirksMode)
                {
                    // Add the default interface to the map (the only one allowed to point to non-null coclass).
                    if (defaultInterfaceInfo.DefaultInterface != null && !skippedUnknownOrDispatch)
                    {
                        if (interfaceToCoClassMapping.ContainsKey(defaultInterfaceInfo.DefaultInterfaceName))
                        {
                            // there's another coclass having this one as default interface - assign null
                            interfaceToCoClassMapping[defaultInterfaceInfo.DefaultInterfaceName] = null;
                        }
                        else
                        {
                            interfaceToCoClassMapping.Add(defaultInterfaceInfo.DefaultInterfaceName, defaultInterfaceInfo.CoClassName);
                        }
                    }
                }

                // Walk through the list of implemented interfaces again. This time we remember all the implemented interfaces (including base)
                for (int j = 0; j < attr.ImplTypesCount; ++j)
                {
                    TypeInfo typeImpl = type.GetRefType(j);
                    Debug.Assert(typeImpl != null);

                    string name = typeImpl.GetDocumentation();
                    while (typeImpl != null)
                    {
                        if (info.Settings.IsUseLegacy35QuirksMode)
                        {
                            // We should re-read name, otherwise this entire loop does not make sense.
                            // Do it only under the Legacy35 compat switch for maximum compatibility.
                            name = typeImpl.GetDocumentation();
                        }

                        // Check if we already saw this interface
                        string coClassName;
                        if (interfaceToCoClassMapping.TryGetValue(name, out coClassName))
                        {
                            // and if it is for a different interface
                            if (coClassName != defaultInterfaceInfo.CoClassName)
                            {
                                // Set the name to null so that we know we've seen other interfaces
                                interfaceToCoClassMapping[name] = null;
                            }
                        }
                        else
                        {
                            coClassName = null;
                            if (!info.Settings.IsUseLegacy35QuirksMode)
                            {
                                // The right thing to do here is add null because name is not guaranteed to be
                                // the default interface (it is handled above). We keep adding the coclass name
                                // if the Legacy35 compact switch is off.
                                coClassName = defaultInterfaceInfo.CoClassName;
                            }

                            interfaceToCoClassMapping.Add(name, coClassName);
                        }

                        TypeAttr attrImpl = typeImpl.GetTypeAttr();
                        if (attrImpl.ImplTypesCount != 1)
                        {
                            break;
                        }

                        typeImpl = typeImpl.GetRefType(0);
                    }
                }

                // We do allow coclass that doesn't have any 'valid' default interfaces to have a class interface
                // For example, 
                // coclass MyObject {
                //     [default] interface IUnknown;
                // }
                defaultInterfaceInfos.Add(defaultInterfaceInfo);
            }

            foreach (DefaultInterfaceInfo defaultInterfaceInfo in defaultInterfaceInfos)
            {
                bool isExclusive = true;
                if (defaultInterfaceInfo.DefaultInterface != null)
                {
                    string coClassName;
                    if (interfaceToCoClassMapping.TryGetValue(defaultInterfaceInfo.DefaultInterfaceName, out coClassName))
                    {
                        if (coClassName == null)
                        {
                            isExclusive = false;
                        }
                    }

                    defaultInterfaceInfo.IsExclusive = isExclusive;
                }
            }

            return defaultInterfaceInfos;
        }

        /// <summary>
        /// Create local class interfaces
        /// </summary>
        private static void CreateLocalClassInterfaces(ConverterInfo converterInfo, IEnumerable<DefaultInterfaceInfo> defaultInterfaceInfos)
        {
            // Walk the candidate list and generate class interfaces
            // Note: We need to create the class interface in two phases because
            // during creation of class interface, we'll need to create interface & event interface, which
            // could have parameters resolve back to coclass which requires certain class interface
            // So split into two stages so that when we are doing creation all necessary information are inplace
            List<ConvClassInterfaceLocal> convClassInterfaceLocals = new List<ConvClassInterfaceLocal>();

            // Phase 1: Create ConvClassInterfaceLocal instances and associate interface with class interfaces
            foreach (DefaultInterfaceInfo info in defaultInterfaceInfos)
            {
                try
                {
                    ConvClassInterfaceLocal local = new ConvClassInterfaceLocal(
                        converterInfo,
                        info.CoClass,
                        info.DefaultInterface,
                        info.DefaultSourceInterface,
                        info.IsExclusive);

                    convClassInterfaceLocals.Add(local);
                }
                catch (ReflectionTypeLoadException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TlbImpResolveRefFailWrapperException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TlbImpGeneralException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TypeLoadException)
                {
                    throw; // TypeLoadException is critical. Throw.
                }
                catch (Exception)
                {
                }
            }

            // Phase 2: Create the class interface
            foreach (ConvClassInterfaceLocal local in convClassInterfaceLocals)
            {
                try
                {
                    local.Create();
                }
                catch (ReflectionTypeLoadException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TlbImpResolveRefFailWrapperException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TlbImpGeneralException)
                {
                    throw; // Fatal failure. Throw
                }
                catch (TypeLoadException)
                {
                    throw; // TypeLoadException is critical. Throw.
                }
                catch (Exception)
                {
                }
            }

        }

        /// <summary>
        /// Struct that holds default interface information for the coclass 
        /// At most the coclass has two default interfaces, one default interface and one source interface
        /// </summary>
        private class DefaultInterfaceInfo
        {
            public TypeInfo CoClass { get; set; }
            public string CoClassName => this.CoClass.GetDocumentation();

            public TypeInfo DefaultInterface { get; set; }
            public string DefaultInterfaceName => this.DefaultInterface.GetDocumentation();

            public TypeInfo DefaultSourceInterface { get; set; }
            public string DefaultSourceInterfaceName => this.DefaultSourceInterface.GetDocumentation();

            public bool IsExclusive { get; set; }
        }
    }
}
