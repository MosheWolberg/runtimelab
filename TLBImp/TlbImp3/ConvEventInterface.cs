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

using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for event interface creation
    /// </summary>
    internal interface IConvEventInterface : IConvBase
    {
        string EventProviderName { get; }

        /// <summary>
        /// The corresponding source interface
        /// </summary>
        IConvInterface SourceInterface { get; }

        /// <summary>
        /// Get the event delegate
        /// </summary>
        /// <param name="memberInfo">The MemberInfo of a source interface that is used to create this delegate</param>
        /// <returns>The delegate type</returns>
        Type GetEventDelegate(InterfaceMemberInfo memberInfo);
    }

    /// <summary>
    /// Converts a local ITypeInfo (source interface) to the event interface
    /// </summary>
    internal class ConvEventInterfaceLocal : ConvLocalBase, IConvEventInterface
    {
        private readonly IConvInterface convInterface;

        // Keeps a mapping of delegate types & function ID.
        private readonly Dictionary<InterfaceMemberInfo, Type> delegateTypes = new Dictionary<InterfaceMemberInfo, Type>();

        public ConvEventInterfaceLocal(IConvInterface convInterface, ConverterInfo info)
            : base(info, convInterface.RefTypeInfo, ConvLocalFlags.DeferDefineType)
        {
            this.convInterface = convInterface;
            this.DefineType();
        }

        public override ConvType ConvType => ConvType.EventInterface;

        public string EventProviderName { get; private set; }

        /// <summary>
        /// Returns the source interface
        /// </summary>
        public IConvInterface SourceInterface => this.convInterface;

        /// <summary>
        /// Get the event delegate for specified method in the source interface. Create a new one if necessary
        /// </summary>
        /// <param name="index">Function index</param>
        /// <returns>The delegate type</returns>
        public Type GetEventDelegate(InterfaceMemberInfo memberInfo)
        {
            // Check if we already have a delegate type for method n
            Type delegateType;
            if (this.delegateTypes.TryGetValue(memberInfo, out delegateType))
            {
                return delegateType;
            }

            TypeInfo type = memberInfo.RefTypeInfo;

            // If not, create a new delegate
            FuncDesc func = type.GetFuncDesc(memberInfo.Index);

            string eventName = type.GetDocumentation(func.MemberId);

            // The naming scheme changed unintentionally in Dev10. We follow the Dev10 scheme by default
            // and revert to the original one if the Legacy35 compat switch is specified. Note that event
            // interface must inherit from another interface in order for this switch to make a difference.
            TypeInfo nameScope;
            if (this.convInfo.Settings.IsUseLegacy35QuirksMode)
            {
                // the old naming scheme - use the declaring type of the member
                nameScope = type;
            }
            else
            {
                // the new naming scheme - use the type that we are converting
                nameScope = this.convInterface.RefTypeInfo;
            }

            string delegateName = this.convInfo.GetRecommendedManagedName(nameScope, ConvType.Interface, useDefaultNamespace: true, ignoreCustomNamespace: false) + "_" + type.GetDocumentation(func.MemberId) + "EventHandler";

            // Deal with name collisions
            delegateName = this.convInfo.GetUniqueManagedName(delegateName);

            TypeBuilder delegateTypeBuilder = this.convInfo.ModuleBuilder.DefineType(
                delegateName,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(MulticastDelegate));

            // Create constructor for the delegate
            ConstructorBuilder delegateCtorBuilder = delegateTypeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.HasThis,
                new Type[] { typeof(object), typeof(UIntPtr) });

            delegateCtorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Create methods for the delegate
            TypeAttr attr = type.GetTypeAttr();
            var interfaceInfoForDelegate = new InterfaceInfo(this.convInfo, delegateTypeBuilder, type, attr, InterfaceInfoFlags.IsSource);
            interfaceInfoForDelegate.AllowNewEnum = !this.convInterface.ImplementsIEnumerable;
            ConvCommon.CreateMethodForDelegate(interfaceInfoForDelegate, func, memberInfo.Index);

            // Emit ComVisibleAttribute(false)
            delegateTypeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForComVisible(false));

            // Emit TypeLibTypeAttribute(TypeLibTypeFlags.FHidden) to hide it from object browser in VB
            delegateTypeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>(TypeLibTypeFlags.FHidden));

            // Create the delegate
            delegateType = delegateTypeBuilder.CreateType();
            this.delegateTypes[memberInfo] = delegateType;
            return delegateType;
        }

        protected override void OnDefineType()
        {
            // Create event interface type
            string name = this.convInfo.GetUniqueManagedName(this.convInterface.RefTypeInfo, ConvType.EventInterface);
            this.typeBuilder = this.convInfo.ModuleBuilder.DefineType(
                name,
                TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.AutoLayout);

            this.convInfo.RegisterType(this.typeBuilder, this);
            this.convInfo.AddToSymbolTable(this.convInterface.RefTypeInfo, ConvType.EventInterface, this);
        }

        /// <summary>
        /// Create the event interface
        /// </summary>
        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            this.convInterface.Create();
            TypeAttr attr = this.convInterface.RefTypeInfo.GetTypeAttr();

            // Emit attributes
            // Emit [ComEventInterfaceAttribute(...)]
            ConstructorInfo ctorComEventInterface = typeof(ComEventInterfaceAttribute).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(Type), typeof(Type) },
                null);

            // Build the blob manually before creating the event interface / provider types. 
            // We only need to give the name of the types, in order to simplify creation logic and avoid dependency
            CustomAttributeBlobBuilder blobBuilder = new CustomAttributeBlobBuilder();

            string eventInterfaceFullyQualifiedName = this.convInterface.ManagedName;
            if (this.convInterface.ConvScope == ConvScope.External)
            {
                eventInterfaceFullyQualifiedName = this.convInterface.ManagedType.AssemblyQualifiedName;
            }

            blobBuilder.AddFixedArg(eventInterfaceFullyQualifiedName); // source interface

            // Handle event provider name generation collision scenario
            this.EventProviderName = this.convInfo.GetUniqueManagedName(
                this.convInfo.GetRecommendedManagedName(this.convInterface.RefTypeInfo, ConvType.Interface, useDefaultNamespace: true, ignoreCustomNamespace: true) + "_EventProvider");

            blobBuilder.AddFixedArg(this.EventProviderName); // corresponding event provider

            this.typeBuilder.SetCustomAttribute(ctorComEventInterface, blobBuilder.GetBlob());

            // Emit ComVisibleAttribute(false)
            this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForComVisible(false));

            // Emit TypeLibTypeAttribute for TYPEFLAG_FHIDDEN
            this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>(TypeLibTypeFlags.FHidden));

            bool isConversionLoss = false;

            // Warn if the type has any properties
            Type interfaceType = this.convInterface.RealManagedType;
            if (interfaceType.GetProperties().Any())
            {
                // Emit a warning and we'll skip the properties
                this.convInfo.ReportEvent(
                    WarningCode.Wrn_NoPropsInEvents,
                    Resource.FormatString("Wrn_NoPropsInEvents", RefTypeInfo.GetDocumentation()));

                isConversionLoss = true;
            }

            // Create event interface
            var eventInterfaceInfo = new InterfaceInfo(this.convInfo, this.typeBuilder, this.convInterface.RefTypeInfo, attr, InterfaceInfoFlags.IsSource);

            ConvCommon.CreateEventInterfaceCommon(eventInterfaceInfo);
            isConversionLoss |= eventInterfaceInfo.IsConversionLoss;

            // Emit ComConversionLossAttribute if necessary
            if (eventInterfaceInfo.IsConversionLoss)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComConversionLossAttribute>());
            }

            return this.typeBuilder.CreateType();
        }
    }
}