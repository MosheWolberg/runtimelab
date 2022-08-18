// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    // Used to represent a TypeLibrary and the interop assembly for it
    internal class ConverterAssemblyInfo
    {
        private const char NestedTypeSeparator = '+';
        private readonly TypeLib typeLib;
        private readonly ConverterInfo info;

        public ConverterAssemblyInfo(ConverterInfo info, Assembly assembly, TypeLib typeLib)
        {
            this.typeLib = typeLib;
            this.info = info;

            this.Assembly = assembly;
            this.ClassInterfaceMap = ClassInterfaceMap.Create(this.info);

            // Try GUID_ManagedName
            string namespaceMaybe;
            if (!this.info.TryGetCustomNamespaceFromTypeLib(typeLib, out namespaceMaybe))
            {
                // Try to use the namespace of the first type
                Type[] types = this.Assembly.GetTypes();
                if (types.Length > 0)
                {
                    namespaceMaybe = types[0].Namespace;
                }

                // Otherwise use the type library name
                if (namespaceMaybe == null)
                {
                    namespaceMaybe = this.typeLib.GetDocumentation();
                }
            }

            this.Namespace = namespaceMaybe;
        }

        public Type ResolveType(TypeInfo typeInfo, ConvType convType, out string expectedName)
        {
            // This is our best guess
            string managedName = this.info.GetRecommendedManagedName(typeInfo, convType, this.Namespace);
            int separatorPos = managedName.IndexOf(NestedTypeSeparator);
            expectedName = managedName;

            // If there is a '+' and it is neither the first or the last
            if (separatorPos > 0 && separatorPos < managedName.Length - 1)
            {
                string parentName = managedName.Substring(0, separatorPos);
                Type parentType = this.Assembly.GetType(parentName);
                return parentType.GetNestedType(managedName.Substring(separatorPos + 1));
            }

            return this.Assembly.GetType(managedName);
        }

        public ClassInterfaceMap ClassInterfaceMap { get; private set; }

        public string Namespace { get; private set; }

        public Assembly Assembly { get; private set; }
    }

    struct ConverterSettings
    {
        public string Namespace;
        public TypeLibImporterFlags Flags;
        public bool IsConvertVariantBoolFieldToBool;
        public bool IsUseLegacy35QuirksMode;
    }

    internal class ConverterInfo
    {
        private static readonly char[] InvalidNamespaceChars = "/\\".ToCharArray();

        private readonly Dictionary<string, IConvBase> symbolTable = new Dictionary<string, IConvBase>();
        private readonly Dictionary<string, IConvBase> typeTable = new Dictionary<string, IConvBase>();
        private readonly Dictionary<Guid, ConverterAssemblyInfo> typeLibMappingTable = new Dictionary<Guid, ConverterAssemblyInfo>();

        // maps a internal name to a unique managed name
        // so that we could find the assigned name for a existing type
        private readonly SortedDictionary<string, string> globalNameTable = new SortedDictionary<string, string>(StringComparer.InvariantCulture);

        // keeps a list of all managed name for duplication check
        private readonly HashSet<string> globalManagedNames = new HashSet<string>(StringComparer.InvariantCulture);

        // Mapping from TypeBuilder to NameTable (Dictionary<string, Type[]>)
        // Used for generating unique member names
        private readonly Dictionary<string, MemberTable> memberTables = new Dictionary<string, MemberTable>();
        private readonly HashSet<TypeBuilder> defaultMemberTable = new HashSet<TypeBuilder>();

        // Type library currently being converted
        private readonly Guid typeLibGuid;

        // Callback to client to resolve assemblies and notify of events
        private readonly ITypeLibImporterNotifySink importerSink;

        public ConverterInfo(
            ModuleBuilder moduleBuilder,
            TypeLib typeLib,
            ITypeLibImporterNotifySink resolver,
            ConverterSettings settings)
        {
            this.ModuleBuilder = moduleBuilder;
            this.TypeLib = typeLib;
            this.typeLibGuid = this.TypeLib.GetLibAttr().Guid;
            this.Settings = settings;
            this.importerSink = resolver;

            BuildGlobalNameTable();
        }

        public TypeLib TypeLib { get; private set; }

        /// <summary>
        /// Return all IConvBase as IEnumerable
        /// </summary>
        public IEnumerable<IConvBase> GetAllConvBase => this.symbolTable.Values;

        /// <summary>
        /// Settings for converter
        /// </summary>
        public ConverterSettings Settings { get; private set; }

        /// <summary>
        /// Reflection Emit helper to generate types for the interop assembly
        /// </summary>
        public ModuleBuilder ModuleBuilder { get; private set; }

        public bool TransformDispRetVal => this.Settings.Flags.HasFlag(TypeLibImporterFlags.TransformDispRetVals);

        // Used to resolve a given type reference (params, fields, etc.)
        public IConvBase GetTypeRef(ConvType convType, TypeInfo type)
        {
            IConvBase ret;
            if (!this.ResolveInternal(type, convType, out ret))
            {
                switch (convType)
                {
                    case ConvType.Enum:
                        ret = new ConvEnumLocal(this, type);
                        break;
                    case ConvType.Interface:
                        ret = new ConvInterfaceLocal(this, type);
                        break;
                    case ConvType.Struct:
                        ret = new ConvStructLocal(this, type);
                        break;
                    case ConvType.Union:
                        ret = new ConvUnionLocal(this, type);
                        break;
                    case ConvType.CoClass:
                        ret = new ConvCoClassLocal(this, type);
                        break;
                    case ConvType.Module:
                        ret = new ConvModuleLocal(this, type);
                        break;
                    default:
                        ret = null;
                        Debug.Fail($"{nameof(ConvType)} value not supported");
                        break;
                }
            }

            Debug.Assert(ret != null && ret.ConvType == convType);
            return ret;
        }

        // Helpers to perform actual definitions for types.
        public IConvBase GetInterface(TypeInfo type, TypeAttr attr)
        {
            IConvInterface convInterface = (IConvInterface)GetTypeRef(ConvType.Interface, type);
            convInterface.Create();
            return convInterface;
        }
       
        public IConvBase GetStruct(TypeInfo type, TypeAttr attr)
        {
            IConvStruct convStruct = (IConvStruct)GetTypeRef(ConvType.Struct, type);
            convStruct.Create();
            return convStruct;
        }
        public IConvBase GetUnion(TypeInfo type, TypeAttr attr)
        {
            IConvUnion convUnion = (IConvUnion)GetTypeRef(ConvType.Union, type);
            convUnion.Create();
            return convUnion;
        }
        public IConvBase GetEnum(TypeInfo type, TypeAttr attr)
        {
            IConvEnum convEnum = (IConvEnum)GetTypeRef(ConvType.Enum, type);
            convEnum.Create();
            return convEnum;
        }
        public IConvBase GetCoClass(TypeInfo type, TypeAttr attr)
        {
            IConvCoClass convCoClass = (IConvCoClass)GetTypeRef(ConvType.CoClass, type);
            convCoClass.Create();
            return convCoClass;
        }

        public IConvBase GetModule(TypeInfo type, TypeAttr attr)
        {
            IConvModule convModule = (IConvModule)GetTypeRef(ConvType.Module, type);
            convModule.Create();
            return convModule;
        }

        /// <summary>
        /// Returns whether the interface supports calling by IDispatch
        /// </summary>
        /// <param name="interfaceType">The interface</param>
        /// <returns>True if the interface supports calling by IDispatch, false otherwise</returns>
        public bool TypeSupportsDispatch(Type type)
        {
            IConvBase convBase;
            if (!this.typeTable.TryGetValue(type.FullName, out convBase))
            {
                return false;
            }

            var convInterface = convBase as IConvInterface;
            if (convInterface != null)
            {
                // dispinterface?
                if (convInterface.RefTypeInfo.GetTypeAttr().IsIDispatch)
                {
                    return true;
                }

                return ConvCommon.IsDerivedFromIDispatch(convInterface.RefTypeInfo);
            }

            return convBase is IConvClassInterface || convBase is IConvEventInterface;
        }

        /// <summary>
        /// Get the unique type name for types that doesn't fall into one of the ConvTypesm, such as event delegates.
        /// Cannot be called multiple times. Multiple calls for the same name will generate multiple name entries
        /// </summary>
        /// <param name="name">The recommended name for delegate</param>
        /// <returns>The unique name</returns>
        public string GetUniqueManagedName(string name)
        {
            string managedName = name;
            int index = 2;
            while (this.globalManagedNames.Contains(managedName))
            {
                managedName = name + "_" + index.ToString();
                index++;
            }

            this.globalManagedNames.Add(managedName);

            return managedName;
        }

        /// <summary>
        /// Get the unique managed name for the ITypeInfo & ConvType
        /// Can be called multiple times as it will cache the entries in a internal table
        /// Note that this is for global types only (not members)
        /// </summary>
        /// <returns>The unique name</returns>
        public string GetUniqueManagedName(TypeInfo type, ConvType convType)
        {
            string recommendedName = GetRecommendedManagedName(type, convType, useDefaultNamespace: false, ignoreCustomNamespace: false);
            string internalName = GetInternalEncodedManagedName(type, convType);
            string managedName;
            if (!this.globalNameTable.TryGetValue(internalName, out managedName))
            {
                managedName = recommendedName;
                if (convType == ConvType.EventInterface || convType == ConvType.CoClass)
                {
                    // We generate unique type names by add "_<n" for EventInterface/CoClass
                    // because these names are "forged"
                    int index = 2;
                    while (this.globalManagedNames.Contains(managedName))
                    {
                        managedName = recommendedName + "_" + index.ToString();
                        index++;
                    }
                }
                else
                {
                    // Duplicated custom managed name needs an warning
                    if (this.globalManagedNames.Contains(recommendedName))
                    {
                        ReportEvent(
                            WarningCode.Wrn_DuplicateTypeName,
                            Resource.FormatString("Wrn_DuplicateTypeName", recommendedName));

                        throw new TlbImpInvalidTypeConversionException(type);
                    }
                }

                this.globalNameTable.Add(internalName, managedName);
                this.globalManagedNames.Add(managedName);
            }

            return managedName;
        }

        public string GetManagedName(TypeInfo type, string theNamespace)
        {
            string name = GetCustomManagedName(type, false, false);
            if (name != null)
            {
                return name;
            }

            return GetGeneratedManagedName(type, theNamespace);
        }

        public string GetManagedName(TypeInfo type, bool useDefaultNamespace, bool ignoreCustomNamespace)
        {
            string name = GetCustomManagedName(type, useDefaultNamespace, ignoreCustomNamespace);
            if (name != null)
            {
                return name;
            }

            return GetGeneratedManagedName(type, useDefaultNamespace);
        }

        public string GetRecommendedManagedName(
            TypeInfo type,
            ConvType convType,
            string theNamespace)
        {
            if (convType == ConvType.EventInterface)
            {
                return GetRecommendedManagedName(type, convType, useDefaultNamespace: false, ignoreCustomNamespace: false);
            }
            else
            {
                return GetManagedName(type, theNamespace);
            }
        }

        /// <summary>
        /// Get recommended managed name according to information obtained from ITypeInfo. 
        /// Doesn't guarantee that the name is unique. Can be called multiple times
        /// GUID_ManagedName is also considered
        /// </summary>
        public string GetRecommendedManagedName(
            TypeInfo type,
            ConvType convType,
            bool useDefaultNamespace,
            bool ignoreCustomNamespace)
        {
            if (convType == ConvType.EventInterface)
            {
                // Special treatment for event interfaces
                // For coclass that are referring to external source interfaces, we can define the event interfaces
                // in local assemblies and the namespace will always be the namespace of the importing type lib
                return GetManagedName(type, useDefaultNamespace: true, ignoreCustomNamespace: true) + "_Event";
            }

            string name = GetManagedName(type, useDefaultNamespace, ignoreCustomNamespace);
            if (convType == ConvType.CoClass)
            {
                name += "Class";
            }

            return name;
        }

        public bool TryGetCustomNamespaceFromTypeLib(TypeLib typeLib, out string customNamespace)
        {
            // Support for GUID_ManagedName (for namespace)
            // Favor the custom name over everything else including /namespace option
            customNamespace = typeLib.GetCustData<string>(CustomAttributeGuids.GUID_ManagedName);
            if (customNamespace != null)
            {
                customNamespace = customNamespace.Trim();
                if (customNamespace.ToUpper().EndsWith(".DLL"))
                {
                    customNamespace = customNamespace.Substring(0, customNamespace.Length - 4);
                }
                else if (customNamespace.ToUpper().EndsWith(".EXE"))
                {
                    customNamespace = customNamespace.Substring(0, customNamespace.Length - 4);
                }
            }

            return customNamespace != null;
        }

        /// <summary>
        /// Check whether a type builder has a member with the specified name & parameter type
        /// </summary>
        public bool HasDuplicateMemberName(TypeBuilder builder, string name, Type[] paramType, MemberTypes memberType)
        {
            // Retrieve the member name table
            MemberTable memberTable;
            if (!this.memberTables.TryGetValue(builder.FullName, out memberTable))
            {
                memberTable = new MemberTable();
                this.memberTables.Add(builder.FullName, memberTable);
            }

            return memberTable.HasDuplicateMember(name, paramType, memberType);
        }

        /// <summary>
        /// Register the type so that later it could be looked-up to find the corresonding IConvBase
        /// It could be a TypeBuilder or a real Type
        /// </summary>
        /// <param name="type">The type to register</param>
        /// <param name="convBase">The IConvBase to register</param>
        public void RegisterType(Type type, IConvBase convBase)
        {
            Debug.Assert(convBase != null);
            if (!this.typeTable.ContainsKey(type.FullName))
            {
                this.typeTable.Add(type.FullName, convBase);
            }
        }

        /// <summary>
        /// Add to symbol table. Basically this should be called in every constructor of IConvBase derived classes
        /// A IConvBase instance should be added to the symbol type the moment it is defined so that we know a IConvBase
        /// instance is already created for this particular name
        /// </summary>
        /// <remarks>
        /// The name we use in internal symbol table is actually the name of the TypeInfo, instead of the name
        /// of the real managed type. This is no different than using the TypeInfo as the key.
        /// </remarks>
        public void AddToSymbolTable(TypeInfo typeInfo, ConvType convType, IConvBase convBase)
        {
            string name = GetInternalEncodedManagedName(typeInfo, convType);
            this.symbolTable.Add(name, convBase);
        }

        public void SetDefaultMember(TypeBuilder typeBuilder, string name)
        {
            if (!this.defaultMemberTable.Contains(typeBuilder))
            {
                typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<DefaultMemberAttribute>(name));
                this.defaultMemberTable.Add(typeBuilder);
            }
        }

        public void ReportEvent(WarningCode code, string eventMsg)
        {
            this.importerSink.ReportEvent(ImporterEventKind.NOTIF_CONVERTWARNING, (int)code, eventMsg);
        }

        public void ReportEvent(MessageCode code, string eventMsg)
        {
            this.importerSink.ReportEvent(ImporterEventKind.NOTIF_TYPECONVERTED, (int)code, eventMsg);
        }

        /// <summary>
        /// Build global name table for existing types in the type library
        /// The reason we need to put those names into the table first is that
        /// their name should not be changed because they come from existing types
        /// </summary>
        private void BuildGlobalNameTable()
        {
            // Traverse each type and add the names into the type. Their names are already in the type library
            // and should not be changed
            int count = this.TypeLib.GetTypeInfoCount();
            for (int n = 0; n < count; ++n)
            {
                try
                {
                    TypeInfo type = this.TypeLib.GetTypeInfo(n);
                    TypeAttr attr = type.GetTypeAttr();
                    switch (attr.Typekind)
                    {
                        case TYPEKIND.TKIND_COCLASS:
                            // class interface uses the original name of the coclass, and coclass use the generated name XXXClass
                            GetUniqueManagedName(type, ConvType.ClassInterface);
                            break;

                        case TYPEKIND.TKIND_INTERFACE:
                            GetUniqueManagedName(type, ConvType.Interface);
                            break;

                        case TYPEKIND.TKIND_RECORD:
                            GetUniqueManagedName(type, ConvType.Struct);
                            break;

                        case TYPEKIND.TKIND_UNION:
                            GetUniqueManagedName(type, ConvType.Union);
                            break;

                        case TYPEKIND.TKIND_ENUM:
                            GetUniqueManagedName(type, ConvType.Enum);
                            break;

                        case TYPEKIND.TKIND_MODULE:
                            GetUniqueManagedName(type, ConvType.Module);
                            break;
                    }
                }
                catch (TlbImpInvalidTypeConversionException)
                {
                    // Swallow this exception. Usually it is caused by duplicated managed name which we can definitely ignore this
                }
            }
        }

        /// <summary>
        /// The name represents a (TypeInfo, ConvType) pair and is unique to a type library. Used in SymbolTable
        /// </summary>
        private string GetInternalEncodedManagedName(TypeInfo typeInfo, ConvType convType)
        {
            TypeLibAttr typeLibAttr = typeInfo.GetContainingTypeLib().GetLibAttr();
            return typeInfo.GetDocumentation() + "[" + convType.ToString() + "," + typeLibAttr.Guid + "]";
        }

        /// <summary>
        /// Gets the generated managed name from the namespace & typeinfo name
        /// </summary>
        private string GetGeneratedManagedName(TypeInfo type, bool useDefaultNamespace)
        {
            string docName = type.GetDocumentation();

            // Figure out assembly namespace (using best guess)
            string tlbNamespace;
            if (useDefaultNamespace)
            {
                tlbNamespace = ComputeNamespaceFromTypeLib(this.TypeLib, type);
            }
            else
            {
                TypeLib typeLib = type.GetContainingTypeLib();
                tlbNamespace = ComputeNamespaceFromTypeLib(typeLib, type);
            }

            if (string.IsNullOrEmpty(tlbNamespace))
            {
                return docName;
            }

            return tlbNamespace + Type.Delimiter + docName;
        }

        /// <summary>
        /// Gets the generated managed name from the namespace & typeinfo name
        /// </summary>
        private string GetGeneratedManagedName(TypeInfo type, string theNamespace)
        {
            string docName = type.GetDocumentation();
            if (string.IsNullOrEmpty(theNamespace))
            {
                return docName;
            }

            return theNamespace + Type.Delimiter + docName;
        }

        /// <summary>
        /// Gets the namespace for the type lib
        /// </summary>
        private string ComputeNamespaceFromTypeLib(TypeLib typeLib, TypeInfo type)
        {
            string tlbNamespace;
            if (this.TryGetCustomNamespaceFromTypeLib(typeLib, out tlbNamespace))
            {
                return tlbNamespace;
            }

            TypeLibAttr attr = typeLib.GetLibAttr();
            if (attr.Guid == this.typeLibGuid)
            {
                tlbNamespace = this.Settings.Namespace;
            }
            else
            {
                tlbNamespace = typeLib.GetDocumentation();
                if (tlbNamespace.IndexOfAny(InvalidNamespaceChars) >= 0)
                {
                    string tlbFilePath;
                    Guid tlbGuid = attr.Guid;
                    int hr = OleAut32.QueryPathOfRegTypeLib(ref tlbGuid, (ushort)attr.MajorVerNum, (ushort)attr.MinorVerNum, (int)attr.Lcid, out tlbFilePath);

                    ReportEvent(
                        WarningCode.Wrn_InvalidNamespace,
                        Resource.FormatString("Wrn_InvalidNamespace", tlbFilePath, tlbNamespace));

                    throw new TlbImpInvalidTypeConversionException(type);
                }
            }

            return tlbNamespace;
        }

        /// <summary>
        /// Check if this type has managed name (GUID_ManagedName)
        /// </summary>
        /// <param name="type">The type to be checked</param>
        /// <returns>The managed name, or null if the GUID_ManagedNamea doesn't exist</returns>
        private string GetCustomManagedName(TypeInfo type, bool useDefaultNamespace, bool ignoreCustomNamespace)
        {
            // Support GUID_ManagedName
            string name = type.GetCustData<string>(CustomAttributeGuids.GUID_ManagedName);
            if (name != null && ignoreCustomNamespace)
            {
                string theNamespace = null;
                if (useDefaultNamespace)
                {
                    theNamespace = ComputeNamespaceFromTypeLib(this.TypeLib, type);
                }
                else
                {
                    TypeLib typeLib = type.GetContainingTypeLib();
                    theNamespace = ComputeNamespaceFromTypeLib(typeLib, type);
                }

                int index = name.LastIndexOf(Type.Delimiter);
                if (index < 0)
                {
                    // custom name does not specify namespace - prepend theNamespace
                    name = theNamespace + Type.Delimiter + name;
                }
                else
                {
                    // custom name specifies namespace - replace it with theNamespace
                    name = theNamespace + name.Substring(index);
                }
            }

            return name;
        }

        private bool ResolveInternal(TypeInfo type, ConvType convType, out IConvBase convBase)
        {
            convBase = null;

            // See if it is already mapped
            if (this.symbolTable.TryGetValue(GetInternalEncodedManagedName(type, convType), out convBase))
            {
                return true;
            }

            TypeLib typeLib = type.GetContainingTypeLib();
            Guid libid = typeLib.GetLibAttr().Guid;

            // See if this is defined in the typelib associated with this class
            if (libid == this.typeLibGuid)
            {
                return false;
            }

            // See if we have not already imported this assembly
            ConverterAssemblyInfo converterAssemblyInfo;
            if (!this.typeLibMappingTable.TryGetValue(libid, out converterAssemblyInfo))
            {
                Assembly assembly = null;
                string asmName = typeLib.GetCustData<string>(CustomAttributeGuids.GUID_ExportedFromComPlus);
                if (asmName != null)
                {
                    try
                    {
                        assembly = Assembly.ReflectionOnlyLoad(asmName);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (assembly == null)
                {
                    try
                    {
                        assembly = this.importerSink.ResolveRef(typeLib);
                    }
                    catch (TlbImpResolveRefFailWrapperException)
                    {
                        // Avoid wrapping wrapper with wrapper exception
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new TlbImpResolveRefFailWrapperException(ex);
                    }
                }

                if (assembly == null)
                {
                    // null means that the resolver has failed and we should skip this failure and continue with the next type
                    throw new TlbImpInvalidTypeConversionException(type);
                }

                converterAssemblyInfo = new ConverterAssemblyInfo(this, assembly, typeLib);
                this.typeLibMappingTable.Add(libid, converterAssemblyInfo);
            }

            string expectedName;
            Type convertedType = converterAssemblyInfo.ResolveType(type, convType, out expectedName);
            if (convertedType == null)
            {
                throw new TlbImpGeneralException(
                    Resource.FormatString("Err_CanotFindReferencedType", expectedName, converterAssemblyInfo.Assembly.FullName),
                    ErrorCode.Err_CanotFindReferencedType);
            }
            else
            {
                // Create external IConvBase instance
                switch (convType)
                {
                    case ConvType.Interface:
                        convBase = new ConvInterfaceExternal(this, type, convertedType, converterAssemblyInfo);
                        break;

                    case ConvType.Enum:
                        convBase = new ConvEnumExternal(this, type, convertedType);
                        break;

                    case ConvType.Struct:
                        convBase = new ConvStructExternal(this, type, convertedType);
                        break;

                    case ConvType.Union:
                        convBase = new ConvUnionExternal(this, type, convertedType);
                        break;

                    case ConvType.ClassInterface:
                        Debug.Fail("Only ConvCoClassExternal can create ConvClassInterfaceExternal");
                        break;

                    case ConvType.EventInterface:
                        Debug.Fail("We should not reference a external event interface!");
                        break;

                    case ConvType.CoClass:
                        convBase = new ConvCoClassExternal(this, type, convertedType, converterAssemblyInfo);
                        break;
                }
            }

            return convBase != null;
        }

        /// <summary>
        /// Maintain a list of members (method, event, property)
        /// Used to generate unique member name for a particular type
        /// </summary>
        private class MemberTable
        {
            private readonly Dictionary<string, List<Type[]>> methods = new Dictionary<string, List<Type[]>>();
            private readonly HashSet<string> events = new HashSet<string>();
            private readonly HashSet<string> properties = new HashSet<string>();

            public bool HasDuplicateMember(string name, Type[] paramTypes, MemberTypes memberType)
            {
                if (paramTypes == null)
                {
                    paramTypes = new Type[0];
                }

                // The rule is: there should be no method/property/event that has the same name
                // except that methods can have the same name but different signature, and cannot have the same
                // name as other property/events
                switch (memberType)
                {
                    case MemberTypes.Method:
                        if (HasMethodWithParam(name, paramTypes) || HasEvent(name) || HasProperty(name))
                        {
                            return true;
                        }
                        else
                        {
                            List<Type[]> paramTypeArrays;
                            if (!this.methods.TryGetValue(name, out paramTypeArrays))
                            {
                                paramTypeArrays = new List<Type[]>();
                                this.methods.Add(name, paramTypeArrays);
                            }

                            paramTypeArrays.Add(paramTypes);
                        }
                        break;

                    case MemberTypes.Property:
                        if (HasProperty(name) || HasEvent(name) || HasMethod(name))
                        {
                            return true;
                        }
                        else
                        {
                            this.properties.Add(name);
                        }
                        break;

                    case MemberTypes.Event:
                        if (HasEvent(name) || HasProperty(name) || HasMethod(name))
                        {
                            return true;
                        }
                        else
                        {
                            this.events.Add(name);
                        }
                        break;

                    default:
                        Debug.Fail($"Unknown {nameof(MemberTypes)} value");
                        break;
                }

                return false;
            }

            private bool HasMethodWithParam(string name, Type[] paramTypes)
            {
                List<Type[]> paramTypesArray;
                if (!this.methods.TryGetValue(name, out paramTypesArray))
                {
                    return false;
                }

                // Check all function name overloads for a match
                foreach (Type[] paramTypesLocal in paramTypesArray)
                {
                    if (paramTypesLocal.Length != paramTypes.Length)
                    {
                        continue;
                    }

                    bool match = true;
                    for (int i = 0; i < paramTypesLocal.Length; ++i)
                    {
                        if (paramTypesLocal[i] != paramTypes[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool HasMethod(string name)
            {
                return this.methods.ContainsKey(name);
            }

            private bool HasEvent(string name)
            {
                return this.events.Contains(name);
            }

            private bool HasProperty(string name)
            {
                return this.properties.Contains(name);
            }
        }
    }
}
