// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for doing creation of class interface
    /// </summary>
    interface IConvClassInterface : IConvBase
    {
    }

    /// <summary>
    /// Create class interface in local type lib
    /// There is no ConvClassInterfaceExternal
    /// </summary>
    internal class ConvClassInterfaceLocal : ConvLocalBase, IConvClassInterface
    {
        private readonly TypeInfo coclassTypeInfo;
        private readonly TypeInfo defaultInterfaceTypeInfo;
        private readonly TypeInfo defaultSourceInterfaceTypeInfo;
        private readonly bool isExclusive;

        private IConvInterface convInterface;
        private IConvInterface convSourceInterface;

        public ConvClassInterfaceLocal(
            ConverterInfo info,
            TypeInfo coclassTypeInfo,
            TypeInfo defaultInterfaceTypeInfo,
            TypeInfo defaultSourceInterfaceTypeInfo,
            bool isExclusive)
            : base(info, coclassTypeInfo, ConvLocalFlags.DeferDefineType)
        {
            this.coclassTypeInfo = coclassTypeInfo;
            this.defaultInterfaceTypeInfo = defaultInterfaceTypeInfo;
            this.defaultSourceInterfaceTypeInfo = defaultSourceInterfaceTypeInfo;
            this.isExclusive = isExclusive;

            this.DefineType();
        }

        public override ConvType ConvType => ConvType.ClassInterface;

        protected override void OnDefineType()
        {
            string classInterfaceName = this.convInfo.GetUniqueManagedName(this.coclassTypeInfo, ConvType.ClassInterface);

            Type defaultInterfaceType = null;
            Type defaultSourceInterfaceType = null;

            // Convert default interface
            if (this.defaultInterfaceTypeInfo != null)
            {
                this.convInterface = (IConvInterface)this.convInfo.GetTypeRef(ConvType.Interface, this.defaultInterfaceTypeInfo);

                // Don't create the interface because we haven't associated the default interface with the class interface yet
                // We don't want to create anything in the "Define" stage
                //this.convInterface.Create();
                defaultInterfaceType = this.convInterface.ManagedType;
            }

            // Convert default source interface
            if (this.defaultSourceInterfaceTypeInfo != null)
            {
                this.convSourceInterface = (IConvInterface)this.convInfo.GetTypeRef(ConvType.Interface, this.defaultSourceInterfaceTypeInfo);

                // Don't create the interface because we haven't associated the default interface with the class interface yet
                // We don't want to create anything in the "Define" stage
                // this.convSourceInterface.Create();
                Type sourceInterfaceType = this.convSourceInterface.RealManagedType;
                IConvEventInterface convEventInterface = this.convSourceInterface.DefineEventInterface();

                // Don't create the interface because we haven't associated the default interface with the class interface yet
                // We don't want to create anything in the "Define" stage
                // convEventInterface.Create();
                defaultSourceInterfaceType = this.convSourceInterface.EventInterface.ManagedType;
            }

            // Prepare list of implemented interfaces
            List<Type> implTypes = new List<Type>();
            if (defaultInterfaceType != null)
            {
                implTypes.Add(defaultInterfaceType);
            }

            if (defaultSourceInterfaceType != null)
            {
                implTypes.Add(defaultSourceInterfaceType);
            }

            // Create the class interface
            this.typeBuilder = this.convInfo.ModuleBuilder.DefineType(
                    classInterfaceName,
                    TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Import,
                    null,
                    implTypes.ToArray());

            // Link to it so that ManagedType will return the class interface while GetWrappedInterfaceType will return the 
            // real interface
            // This must be done before creating the coclass because coclass needs this information
            // Only do so when the default interface is exclusively belongs to one coclass
            if (this.convInterface != null && this.isExclusive)
            {
                // Check if the default interface -> class interface relationship exists in the default
                // interface's type lib. That means we only need to check if the default interface and
                // the coclass are in the same type library.
                TypeLib typeLib = this.convInterface.RefTypeInfo.GetContainingTypeLib();
                Guid libIdOfDefaultInterface = typeLib.GetLibAttr().Guid;
                TypeLib coclassTypeLib = this.coclassTypeInfo.GetContainingTypeLib();
                Guid libIdOfCoclass = coclassTypeLib.GetLibAttr().Guid;
                if (libIdOfDefaultInterface.Equals(libIdOfCoclass))
                {
                    this.convInterface.AssociateWithExclusiveClassInterface(this);
                }
            }

            // Emit GuidAttribute, which is the same as the default interface, if it exists
            // If there is no default Interface here, and the coclass implements IDispatch or IUnknown as non-source
            // interface, we use the IDispatch or IUnknown's guid.
            if (defaultInterfaceType != null)
            {
                ConvCommon.DefineGuid(this.convInterface.RefTypeInfo, this.convInterface.RefNonAliasedTypeInfo, this.typeBuilder);
            }
            else
            {
                TypeInfo implementedIDispatchOrIUnknownTypeInfo = null;
                TypeAttr attr = this.coclassTypeInfo.GetTypeAttr();
                for (int i = 0; i < attr.ImplTypesCount; ++i)
                {
                    IMPLTYPEFLAGS flags = this.coclassTypeInfo.GetImplTypeFlags(i);

                    TypeInfo typeImpl = this.coclassTypeInfo.GetRefType(i);
                    TypeAttr attrImpl = typeImpl.GetTypeAttr();
                    if (attrImpl.Guid == WellKnownGuids.IID_IDispatch
                        || attrImpl.Guid == WellKnownGuids.IID_IUnknown)
                    {
                        // If more than one IDispatch or IUnknown exist, we will pick the default one;
                        // If none of them is with the default flag, pick the first one.
                        if (!flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE)
                            && (flags.HasFlag(IMPLTYPEFLAGS.IMPLTYPEFLAG_FDEFAULT) || implementedIDispatchOrIUnknownTypeInfo == null))
                        {
                            implementedIDispatchOrIUnknownTypeInfo = typeImpl;
                        }
                    }
                }

                if (implementedIDispatchOrIUnknownTypeInfo != null)
                {
                    ConvCommon.DefineGuid(
                        implementedIDispatchOrIUnknownTypeInfo,
                        implementedIDispatchOrIUnknownTypeInfo,
                        this.typeBuilder);
                }
            }

            // Make sure we know about the class interface before we go to define the coclass in the next statement
            this.convInfo.RegisterType(this.typeBuilder, this);
            this.convInfo.AddToSymbolTable(this.coclassTypeInfo, ConvType.ClassInterface, this);

            // Handle [CoClass(typeof(...))]
            Type typeRefCoClass = this.convInfo.GetTypeRef(ConvType.CoClass, this.coclassTypeInfo).ManagedType;
            ConstructorInfo ctorCoClassAttribute = typeof(CoClassAttribute).GetConstructor(new Type[] { typeof(Type) });

            // For back compatibility, use full name to create CoClassAttribute, instead of assembly qualified name.
            CustomAttributeBlobBuilder blobBuilder = new CustomAttributeBlobBuilder();
            blobBuilder.AddFixedArg(typeRefCoClass.FullName);
            this.typeBuilder.SetCustomAttribute(ctorCoClassAttribute, blobBuilder.GetBlob());
        }

        /// <summary>
        /// Create the class interface
        /// If the default interface is exclusively owned/used by this co-class, isExclusive should be set to true,
        /// meaning that we should replace every occurrence of the default interface with the class interface 
        /// </param>
        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            if (this.convInterface != null)
                this.convInterface.Create();

            if (this.convSourceInterface != null)
            {
                this.convSourceInterface.Create();
                this.convSourceInterface.EventInterface.Create();
            }

            // Create the type and add it to the list of created types
            return this.typeBuilder.CreateType();
        }
    }

    /// <summary>
    /// We need to replace external CoClass with external class interface in the signature
    /// therefore we need to support ConvClassInterfaceExternal. However, we should not create 
    /// it in the resolve process, but create it when we create ConvCoClassExternal
    /// </summary>
    class ConvClassInterfaceExternal : IConvClassInterface
    {
        private readonly TypeInfo typeInfo;

        public ConvClassInterfaceExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType, ConverterAssemblyInfo converterAssemblyInfo)
        {
            this.typeInfo = typeInfo;
            this.ManagedType = managedType;

            info.RegisterType(managedType, this);

            // Associate the default interface with the class interface
            TypeInfo defaultInterfaceTypeInfo;
            if (converterAssemblyInfo.ClassInterfaceMap.TryGetExclusiveDefaultInterfaceForCoclass(typeInfo, out defaultInterfaceTypeInfo))
            {
                var convInterface = (IConvInterface)info.GetTypeRef(ConvType.Interface, defaultInterfaceTypeInfo);
                convInterface.AssociateWithExclusiveClassInterface(this);
            }
        }

        public void Create()
        {
            // Do nothing
        }

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.typeInfo;

        public Type ManagedType { get; private set; }

        public Type RealManagedType => this.ManagedType;

        public ConvType ConvType => ConvType.Struct;

        public string ManagedName => RealManagedType.FullName;

        public ConvScope ConvScope => ConvScope.External;
    }
}