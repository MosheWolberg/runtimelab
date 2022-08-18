// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    [Flags]
    internal enum InterfaceInfoFlags
    {
        None = 0,
        SupportsIDispatch,
        IsCoClass,
        IsSource
    }

    /// <summary>
    /// Context information used to create interface methods on managed type. Could be a interface, or a coclass.
    /// </summary>
    internal class InterfaceInfo
    {
        private readonly InterfaceInfoFlags flags;
        private readonly Stack<TypeInfo> typeStack = new Stack<TypeInfo>();
        private readonly Stack<TypeAttr> attrStack = new Stack<TypeAttr>();

        public InterfaceInfo(ConverterInfo info, TypeBuilder typeBuilder, TypeInfo type, TypeAttr attr, InterfaceInfoFlags flags, TypeInfo implementingInterface = null)
        {
            this.ConverterInfo = info;
            this.TypeBuilder = typeBuilder;
            this.flags = flags;
            this.CurrentImplementingInterface = implementingInterface;
            this.CurrentSlot = 0;
            PushType(type, attr);

            this.PropertyInfo = new PropertyInfo(this);
        }

        public void PushType(TypeInfo type, TypeAttr attr)
        {
            this.typeStack.Push(type);
            this.attrStack.Push(attr);
        }

        public void PopType()
        {
            this.typeStack.Pop();
            this.attrStack.Pop();
        }

        /// <summary>
        /// Generate a unique member name according to prefix & suffix
        /// Will try prefix first if prefix is not null, otherwise try suffix (_2, _3, ...)
        /// </summary>
        public string GenerateUniqueMemberName(string name, Type[] paramTypes, MemberTypes memberType)
        {
            // TypeBuilder.GetMethod/GetEvent/GetProperty doesn't work before the type is created. 
            // ConverterInfo maintains a global TypeBuilder -> (Name, Type[]) mapping
            // So ask ConverterInfo if we already have that
            string newName = name;
            
            if (!this.ConverterInfo.HasDuplicateMemberName(this.TypeBuilder, newName, paramTypes, memberType))
            {
                return newName;
            }

            // If we are creating a coclass, try prefix first
            if (this.IsCoClass)
            {
                // Use the unique interface name instead of the type info name 
                // (but TlbImpv1 actually use the type info name, which is incorrect)

                // Use the current implementing interface instead of the active interface we are implementing
                // When coclass A implements IA2 which derives from IA1. 
                // m_currentImplementingInterface will always be IA2, while the active interface (RefTypeInfo) will be IA2 then IA1
                string prefix;
                if (IsSource)
                {
                    prefix = this.ConverterInfo.GetTypeRef(ConvType.EventInterface, this.CurrentImplementingInterface).ManagedName;
                }
                else
                {
                    prefix = this.ConverterInfo.GetTypeRef(ConvType.Interface, this.CurrentImplementingInterface).ManagedName;
                }
 
                // Remove the namespace of prefix and try the new name
                newName = RemoveNamespace(prefix) + "_" + name;
                if (!this.ConverterInfo.HasDuplicateMemberName(this.TypeBuilder, newName, paramTypes, memberType))
                {
                    return newName;
                }

                // Now use the prefixed name as starting point
                name = newName;
            }

            int postFix = 2;

            // OK. Prefix doesn't work. Let's try suffix
            // Find the first unique name for the type.
            do
            {
                newName = $"{name}_{postFix}";
                postFix++;
            }
            while (this.ConverterInfo.HasDuplicateMemberName(this.TypeBuilder, newName, paramTypes, memberType));

            return newName;
        }

        /// <summary>
        /// Whether this interface is a default interface of a coclass
        /// </summary>
        public bool IsDefaultInterface { get; set; }

        /// <summary>
        /// This one deserves some explanation
        /// if coclass A implements IA2 : IA1
        /// when we go to IA2 and implement all the methods on A for IA1, we need to override the method in IA2, not in IA1
        /// because it is possible for A to both implement IA1 & IA2.
        /// So in this case, we are emitting methods in IA1, but the current implementing interface is actually IA2
        /// </summary>
        public TypeInfo CurrentImplementingInterface { get; private set; }

        public ConverterInfo ConverterInfo { get; private set; }
        public PropertyInfo PropertyInfo { get; private set; }
        public TypeBuilder TypeBuilder { get; private set; }

        public TypeInfo RefTypeInfo { get { return this.typeStack.Peek(); } }
        public TypeAttr RefTypeAttr { get { return this.attrStack.Peek(); } }

        public bool EmitDispId => this.flags.HasFlag(InterfaceInfoFlags.SupportsIDispatch);
        public bool IsCoClass => this.flags.HasFlag(InterfaceInfoFlags.IsCoClass);
        public bool IsSource => this.flags.HasFlag(InterfaceInfoFlags.IsSource);

        public bool IsConversionLoss { get; set; }

        /// <summary>
        /// Whether we allow DISPID_NEWENUM members in the interface anymore
        /// Will be changed to false if we have already created a new enum member
        /// </summary>
        public bool AllowNewEnum { get; set; }

        public int CurrentSlot { get; set; }

        /// <summary>
        /// Remove namespace of a name
        /// </summary>
        private static string RemoveNamespace(string name)
        {
            int index = name.LastIndexOf(Type.Delimiter);
            if (index < 0)
            {
                return name;
            }

            if (index >= name.Length - 1)
            {
                return string.Empty;
            }

            return name.Substring(index + 1);
        }
    }
}
