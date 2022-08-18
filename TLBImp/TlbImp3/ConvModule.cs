// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for conversion from ITypeInfo of a module to managed class
    /// </summary>
    interface IConvModule : IConvBase
    {
    }

    /// <summary>
    /// Conversion a local ITypeInfo to module    
    /// </summary>
    internal class ConvModuleLocal : ConvLocalBase, IConvModule
    {
        public ConvModuleLocal(ConverterInfo info, TypeInfo type)
            : base(info, type, ConvLocalFlags.DealWithAlias)
        {
        }

        public override ConvType ConvType => ConvType.Module;

        protected override void OnDefineType()
        {
            TypeInfo typeInfo = RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();
            string name = this.convInfo.GetUniqueManagedName(RefTypeInfo, ConvType.Module);

            // Create the managed type for the module
            // It should be abstract & sealed, which is the same as a static class in C#
            // Also, reflection will create a default constructor for you if the class has no constructor,
            // except if the class is interface, valuetype, enum, or a static class, so this works pretty well
            // except that this is slightly different than tlbimpv1, as tlbimpv1 the class is not sealed
            this.typeBuilder = this.convInfo.ModuleBuilder.DefineType(
                name,
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
                typeof(Object));

            // Handle [Guid(...)] custom attribute
            ConvCommon.DefineGuid(RefTypeInfo, RefNonAliasedTypeInfo, this.typeBuilder);

            // Handle [TypeLibType(...)] if evaluate to non-0
            TypeAttr refTypeAttr = RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.TypeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)refTypeAttr.TypeFlags));
            }

            // Add to symbol table automatically
            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.Module, this);

            // Register type
            this.convInfo.RegisterType(this.typeBuilder, this);
        }

        /// <summary>
        /// Create the type for coclass
        /// </summary>
        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            // Create constant fields for the module
            ConvCommon.CreateConstantFields(this.convInfo, RefNonAliasedTypeInfo, this.typeBuilder, ConvType.Module);

            return this.typeBuilder.CreateType();
        }
    }
}
