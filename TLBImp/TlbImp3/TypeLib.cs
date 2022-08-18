// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Reflection;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    [Flags]
    public enum TypeLibImporterFlags
    {
        None                        = 0x00000000,
        PrimaryInteropAssembly      = 0x00000001,
        UnsafeInterfaces            = 0x00000002,
        SafeArrayAsSystemArray      = 0x00000004,
        TransformDispRetVals        = 0x00000008,
        PreventClassMembers         = 0x00000010,
        SerializableValueClasses    = 0x00000020,
        ImportAsX86                 = 0x00000100,
        ImportAsX64                 = 0x00000200,
        ImportAsItanium             = 0x00000400,
        ImportAsAgnostic            = 0x00000800,
        // ReflectionOnlyLoading       = 0x00001000,
        NoDefineVersionResource     = 0x00002000,
        ImportAsArm                 = 0x00004000,
    }

    internal enum ImporterEventKind
    {
        NOTIF_TYPECONVERTED         = 0,
        NOTIF_CONVERTWARNING        = 1,
        ERROR_REFTOINVALIDTYPELIB   = 2,
    }

    internal interface ITypeLibImporterNotifySink
    {
        void ReportEvent(
                ImporterEventKind eventKind,
                int eventCode,
                string eventMsg);
        Assembly ResolveRef(TypeLib typeLib);
    }

    internal class TypeLib
    {
        private readonly ITypeLib typeLib;
        private readonly ITypeLib2 typeLib2;

        public TypeLib(ITypeLib typeLib)
        {
            this.typeLib = typeLib;
            this.typeLib2 = this.typeLib as ITypeLib2;
        }

        public int GetTypeInfoCount()
        {
            return this.typeLib.GetTypeInfoCount();
        }

        public TypeInfo GetTypeInfo(int index)
        {
            ITypeInfo typeinfo;
            this.typeLib.GetTypeInfo(index, out typeinfo);
            return new TypeInfo(typeinfo);
        }

        public TypeLibAttr GetLibAttr()
        {
            return new TypeLibAttr(this.typeLib);
        }

        public String GetDocumentation(int index)
        {
            this.typeLib.GetDocumentation(index, out string name, out _, out _, out _);
            return name;
        }

        public String GetDocumentation()
        {
            return GetDocumentation(TYPEATTR.MEMBER_ID_NIL);
        }

        public T GetCustData<T>(Guid guid) where T : class
        {
            if (this.typeLib2 == null)
            {
                return default(T);
            }

            object val;
            this.typeLib2.GetCustData(ref guid, out val);
            return (T)val;
        }
    }
}
