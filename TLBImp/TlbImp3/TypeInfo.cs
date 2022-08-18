// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    internal class TypeInfo
    {
        private readonly ITypeInfo typeInfo;
        private readonly ITypeInfo2 typeInfo2;

        public TypeInfo(ITypeInfo typeinfo)
        {
            this.typeInfo = typeinfo;
            this.typeInfo2 = this.typeInfo as ITypeInfo2;
        }

        public TypeAttr GetTypeAttr()
        {
            return new TypeAttr(this.typeInfo);
        }

        public FuncDesc GetFuncDesc(int index)
        {
            return new FuncDesc(this.typeInfo, index);
        }

        public VarDesc GetVarDesc(int index)
        {
            return new VarDesc(this.typeInfo, index);
        }

        public string[] GetNames(int memid, int len)
        {
            var strarray = new string[len];
            var ptrArray = new IntPtr[len];
            var gch = GCHandle.Alloc(ptrArray, GCHandleType.Pinned);
            try
            {
                this.typeInfo.GetNames(memid, gch.AddrOfPinnedObject(), strarray.Length, out len);
            }
            finally
            {
                gch.Free();
            }

            for (int i = 0; i < len; ++i)
            {
                var val = string.Empty;
                if (ptrArray[i] != IntPtr.Zero)
                {
                    // This API doesn't support null BSTR, which it should
                    val = Marshal.PtrToStringBSTR(ptrArray[i]);
                }

                strarray[i] = val;
            }

            return strarray;
        }

        public bool TryGetRefTypeForDual(out TypeInfo typeInfo)
        {
            // https://docs.microsoft.com/en-us/windows/desktop/api/oaidl/nf-oaidl-itypeinfo-getreftypeofimpltype
            int href;
            int hr = this.typeInfo.GetRefTypeOfImplType(-1, out href);
            if (hr != 0)
            {
                typeInfo = null;
                return false;
            }

            this.typeInfo.GetRefTypeInfo(href, out ITypeInfo typeInfoLocal);
            typeInfo = new TypeInfo(typeInfoLocal);
            return true;
        }

        public TypeInfo GetRefType(int index)
        {
            int href;
            int hr = this.typeInfo.GetRefTypeOfImplType(index, out href);
            if (hr != 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }

            this.typeInfo.GetRefTypeInfo(href, out ITypeInfo typeinfo);
            return new TypeInfo(typeinfo);
        }

        public TypeInfo GetRefTypeInfo(int href)
        {
            this.typeInfo.GetRefTypeInfo(href, out ITypeInfo typeinfo);
            return new TypeInfo(typeinfo);
        }

        public IMPLTYPEFLAGS GetImplTypeFlags(int index)
        {
            this.typeInfo.GetImplTypeFlags(index, out IMPLTYPEFLAGS flags);
            return flags;
        }

        public string GetDocumentation(int index)
        {
            // The reason why we want to pass NULL (IntPtr.Zero) is that
            // ITypeInfo2::GetDocumentation will try to load up the corresponding
            // DLL and look for the GetDocumentation() entry point and ask it for
            // documentation, which will probably fail. As a result, GetDocumentation()
            // will fail.
            // To avoid this issue, always pass NULL for the last 3 arguments which we don't need anyways
            this.typeInfo.GetDocumentation(index, out string name, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return name;
        }

        public string GetDocumentation()
        {
            return GetDocumentation(TYPEATTR.MEMBER_ID_NIL);
        }

        public TypeLib GetContainingTypeLib()
        {
            this.typeInfo.GetContainingTypeLib(out ITypeLib typelib, out _);
            return new TypeLib(typelib);
        }

        public T GetCustData<T>(Guid guid) where T : class
        {
            if (this.typeInfo2 == null)
            {
                return default(T);
            }

            object obj;
            if (this.typeInfo2.GetCustData(ref guid, out obj) != 0)
            {
                obj = null;
            }

            return (T)obj;
        }

        public T GetFuncCustData<T>(int index, Guid guid) where T : class
        {
            if (this.typeInfo2 == null)
            {
                return default(T);
            }

            object obj;
            if (this.typeInfo2.GetFuncCustData(index, ref guid, out obj) != 0)
            {
                obj = null;
            }

            return (T)obj;
        }

        public T GetVarCustData<T>(int index, Guid guid) where T : class
        {
            if (this.typeInfo2 == null)
            {
                return default(T);
            }

            object obj;
            if (this.typeInfo2.GetVarCustData(index, ref guid, out obj) != 0)
            {
                obj = null;
            }

            return (T)obj;
        }

        /// <summary>
        /// Is this type a StdOle2.Guid? The test is done using the GUID of type library
        /// </summary>
        public bool IsStdOleGuid()
        {
            TypeLib typeLib = this.GetContainingTypeLib();
            if (typeLib.GetDocumentation() == "GUID")
            {
                TypeLibAttr typeLibAttr = typeLib.GetLibAttr();
                return typeLibAttr.Guid == WellKnownGuids.TYPELIBID_STDOLE2;
            }

            return false;
        }
    }
}
