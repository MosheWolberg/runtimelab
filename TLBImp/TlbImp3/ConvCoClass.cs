// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for conversion from ITypeInfo of a coclass to managed class
    /// </summary>
    interface IConvCoClass : IConvBase
    {
        IConvInterface DefaultInterface { get; }
    }

    /// <summary>
    /// Conversion a local ITypeInfo to class
    /// </summary>
    internal class ConvCoClassLocal : ConvLocalBase, IConvCoClass
    {
        private IConvClassInterface classInterface;

        public ConvCoClassLocal(ConverterInfo info, TypeInfo type)
            : base(info, type, ConvLocalFlags.DealWithAlias)
        {
        }

        public override ConvType ConvType => ConvType.CoClass;

        public IConvInterface DefaultInterface { get; private set; }

        protected override void OnDefineType()
        {
            TypeInfo typeInfo = this.RefNonAliasedTypeInfo;
            string name = this.convInfo.GetUniqueManagedName(RefTypeInfo, ConvType.CoClass);

            // Collect information for a list of interfaces & event interface types
            var intfList = new List<Type>();
            var eventIntfList = new List<Type>();
            TypeInfo defaultInterfaceTypeInfo = null;

            string sourceInterfaceNames = string.Empty;
            bool hasDefaultInterface = false;

            TypeAttr attr = typeInfo.GetTypeAttr();
            for (int i = 0; i < attr.ImplTypesCount; ++i)
            {
                IMPLTYPEFLAGS flags = typeInfo.GetImplTypeFlags(i);
                bool isDefault = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULT);
                bool isSource = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE);

                TypeInfo typeImpl = typeInfo.GetRefType(i);
                TypeAttr attrImpl = typeImpl.GetTypeAttr();
                // Skip IUnknown & IDispatch
                if (attrImpl.Guid == WellKnownGuids.IID_IDispatch
                    || attrImpl.Guid == WellKnownGuids.IID_IUnknown)
                {
                    continue;
                }

                // Skip non-dispatch interfaces that doesn't derive from IUnknown
                if (!attrImpl.IsIDispatch && !ConvCommon.IsDerivedFromIUnknown(typeImpl))
                {
                    continue;
                }

                IConvInterface convInterface = (IConvInterface)this.convInfo.GetTypeRef(ConvType.Interface, typeImpl);
                ConvCommon.ThrowIfImplementingExportedClassInterface(this.RefTypeInfo, convInterface);

                // For source interfaces, try create the event interface
                // Could be already created if it is the default source interface
                if (isSource)
                {
                    convInterface.DefineEventInterface();
                }

                // Use the RealManagedType (avoid getting the class interface)
                Type typeRef = convInterface.RealManagedType;

                // Append the source interface name to the list for the ComSourceInterfacesAttribute
                if (isSource)
                {
                    string interfaceName;
                    if (convInterface.ConvScope == ConvScope.External)
                    {
                        interfaceName = typeRef.AssemblyQualifiedName + "\0";
                    }
                    else
                    {
                        interfaceName = typeRef.FullName + "\0";
                    }

                    // Insert default source interface to the beginning
                    if (isDefault)
                    {
                        sourceInterfaceNames = interfaceName + sourceInterfaceNames;
                    }
                    else
                    {
                        sourceInterfaceNames = sourceInterfaceNames + interfaceName;
                    }
                }

                if (isDefault)
                {
                    // Add the real interface first
                    if (isSource)
                    {
                        // For source interface, use the event interface instead
                        // Insert to the beginning
                        eventIntfList.Insert(0, convInterface.EventInterface.ManagedType);
                    }
                    else
                    {
                        this.DefaultInterface = convInterface;

                        // Insert to the beginning
                        intfList.Insert(0, typeRef);
                        hasDefaultInterface = true;
                        defaultInterfaceTypeInfo = typeImpl;
                    }
                }
                else
                {
                    if (isSource)
                    {
                        // For source interface, add the event interface instead
                        eventIntfList.Add(convInterface.EventInterface.ManagedType);
                    }
                    else
                    {
                        if (this.DefaultInterface == null)
                        {
                            this.DefaultInterface = convInterface;
                            defaultInterfaceTypeInfo = typeImpl;
                        }

                        intfList.Add(typeRef);
                    }
                }
            }

            // Get class interface
            this.classInterface = (IConvClassInterface)this.convInfo.GetTypeRef(ConvType.ClassInterface, this.RefTypeInfo);
            if (this.classInterface == null)
            {
                throw new TlbImpInvalidTypeConversionException(this.RefTypeInfo);
            }

            // Build implemented type list in a specific order
            List<Type> implTypeList = new List<Type>();
            if (hasDefaultInterface)
            {
                implTypeList.Add(intfList[0]);
                intfList.RemoveAt(0);

                implTypeList.Add(this.classInterface.ManagedType);
            }
            else
            {
                implTypeList.Add(this.classInterface.ManagedType);
                if (intfList.Any())
                {
                    implTypeList.Add(intfList[0]);
                    intfList.RemoveAt(0);
                }
            }

            if (eventIntfList.Any())
            {
                implTypeList.Add(eventIntfList[0]);
                eventIntfList.RemoveAt(0);
            }

            implTypeList.AddRange(intfList);
            implTypeList.AddRange(eventIntfList);

            // Check to see if the default interface has a member with a DISPID of DISPID_NEWENUM.
            if (defaultInterfaceTypeInfo != null)
            {
                bool implementsIEnumerable = ConvCommon.ExplicitlyImplementsIEnumerable(typeInfo, attr);
                if (!implementsIEnumerable && ConvCommon.HasNewEnumMember(this.convInfo, defaultInterfaceTypeInfo, name))
                {
                    implTypeList.Add(typeof(System.Collections.IEnumerable));
                }
            }

            // Check to see if the IEnumerable Custom Value exists on the CoClass if doesn't inherit from IEnumerable yet
            if (!implTypeList.Contains(typeof(System.Collections.IEnumerable)))
            {
                if (ConvCommon.HasForceIEnumerableCustomAttribute(typeInfo))
                {
                    implTypeList.Add(typeof(System.Collections.IEnumerable));
                }
            }

            // Define the type
            this.typeBuilder = this.convInfo.ModuleBuilder.DefineType(
                name,
                TypeAttributes.Public | TypeAttributes.Import,
                typeof(object),
                implTypeList.ToArray());

            // Handle [Guid(...)] custom attribute
            ConvCommon.DefineGuid(RefTypeInfo, RefNonAliasedTypeInfo, this.typeBuilder);

            // Handle [ClassInterface(ClassInterfaceType.None)]
            this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ClassInterfaceAttribute>(ClassInterfaceType.None));

            // Handle [TypeLibType(...)] if evaluate to non-0
            TypeAttr refTypeAttr = RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.TypeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)refTypeAttr.TypeFlags));
            }

            // Handle [ComSourceInterfacesAttribute]
            if (sourceInterfaceNames != string.Empty)
            {
                sourceInterfaceNames += "\0";
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComSourceInterfacesAttribute>(sourceInterfaceNames));
            }

            // Add to symbol table automatically
            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.CoClass, this);

            // Register type
            this.convInfo.RegisterType(this.typeBuilder, this);
        }

        /// <summary>
        /// Create the type for coclass
        /// </summary>
        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            // Create constructor
            // This is created before creating other methods because if anything fails, the constructor would still be valid
            // Otherwise reflection API will try to create one for us which will have incorrect setting (such as no internalcall flag)
            this.CreateConstructor();

            bool isConversionLoss = false;

            TypeInfo typeInfo = this.RefNonAliasedTypeInfo;
            if (!this.convInfo.Settings.Flags.HasFlag(TypeLibImporterFlags.PreventClassMembers))
            {
                TypeAttr attr = typeInfo.GetTypeAttr();
                var processedInterfaces = new HashSet<Guid>();

                // Iterate through every interface and override the methods
                // Process the default interface first
                for (int i = 1; i <= 2; ++i)
                {
                    for (int j = 0; j < attr.ImplTypesCount; ++j)
                    {
                        IMPLTYPEFLAGS flags = typeInfo.GetImplTypeFlags(j);
                        bool isDefault = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULT);
                        bool isSource = flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE);

                        // Use exclusive-or to process just the default interface on the first pass
                        if (isDefault ^ (i == 2))
                        {
                            TypeInfo typeImpl = typeInfo.GetRefType(j);
                            TypeAttr attrImpl = typeImpl.GetTypeAttr();
                            if (attrImpl.Guid == WellKnownGuids.IID_IUnknown
                                || attrImpl.Guid == WellKnownGuids.IID_IDispatch)
                            {
                                continue;
                            }

                            // Skip non-dispatch interfaces that doesn't derive from IUnknown
                            if (!attrImpl.IsIDispatch && !ConvCommon.IsDerivedFromIUnknown(typeImpl))
                            {
                                continue;
                            }

                            // Skip duplicate interfaces in type library
                            // In .IDL you can actually write:
                            // coclass A
                            // {
                            //     interface IA; 
                            //     interface IA;
                            //     ...
                            // }
                            if (!processedInterfaces.Contains(attrImpl.Guid))
                            {
                                isConversionLoss |= HandleParentInterface(typeImpl, isSource, isConversionLoss, isDefault);
                                processedInterfaces.Add(attrImpl.Guid);
                            }
                        }
                    }
                }
            }

            //Emit ComConversionLoss attribute if necessary
            if (isConversionLoss)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComConversionLossAttribute>());
            }

            return this.typeBuilder.CreateType();
        }

        /// <summary>
        /// Implement methods in parent interfaces
        /// </summary>
        /// <returns>Indicates if the parent interface has conversion loss</returns>
        private bool HandleParentInterface(TypeInfo type, bool isSource, bool isConversionLoss, bool isDefault)
        {
            TypeAttr attr = type.GetTypeAttr();
            bool supportedIDispatch = ConvCommon.InterfaceSupportsDispatch(type, attr);
            InterfaceInfoFlags intFlags =
                (supportedIDispatch ? InterfaceInfoFlags.SupportsIDispatch : InterfaceInfoFlags.None)
                | InterfaceInfoFlags.IsCoClass
                | (isSource ? InterfaceInfoFlags.IsSource : InterfaceInfoFlags.None);
            var interfaceInfo = new InterfaceInfo(this.convInfo, this.typeBuilder, type, attr, intFlags, type);
            interfaceInfo.IsDefaultInterface = isDefault;

            if (isSource)
            {
                // When adding override methods to the interface, we need to use the event interface for source interfaces
                ConvCommon.CreateEventInterfaceCommon(interfaceInfo);
            }
            else
            {
                ConvCommon.CreateInterfaceCommon(interfaceInfo);
            }

            return interfaceInfo.IsConversionLoss;
        }

        /// <summary>
        /// Generate the special internal call constructor for RCW's
        /// </summary>
        private void CreateConstructor()
        {
            TypeInfo typeInfo = this.RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();

            MethodAttributes methodAttributes = typeAttr.IsCanCreate ?
                MethodAttributes.Public :
                MethodAttributes.Assembly;

            ConstructorBuilder method = this.typeBuilder.DefineDefaultConstructor(methodAttributes);
            ConstructorInfo ctorMethodImpl = typeof(MethodImplAttribute).GetConstructor(new Type[] { typeof(MethodImplOptions) });
            method.SetImplementationFlags(MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime);
        }
    }

    // In type library you can refer to the coclass (which is weird...) and will be converted to class interface
    // So we should support external ConvCoClass
    internal class ConvCoClassExternal : IConvCoClass
    {
        private readonly TypeInfo typeInfo;
        private readonly Type managedType;

        public ConvCoClassExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType, ConverterAssemblyInfo converterAssemblyInfo)
        {
            this.typeInfo = typeInfo;
            this.managedType = managedType;

            info.RegisterType(managedType, this);

            TypeInfo defaultTypeInfo = ConvCommon.GetDefaultInterface(ConvCommon.GetAlias(typeInfo));
            if (defaultTypeInfo != null)
            {
                this.DefaultInterface = (IConvInterface)info.GetTypeRef(ConvType.Interface, defaultTypeInfo);
            }
        }

        public IConvInterface DefaultInterface { get; private set; }

        public void Create()
        {
            // Do nothing as the type is already created
        }

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.RefTypeInfo;

        public Type ManagedType => this.managedType;

        public Type RealManagedType => this.ManagedType;

        public ConvType ConvType => ConvType.CoClass;

        public string ManagedName => this.RealManagedType.FullName;

        public ConvScope ConvScope => ConvScope.External;
    }
}
