// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;

namespace TypeLibUtilities.TypeLibAPI
{
    /// <summary>
    /// Wrapper for TYPEATTR
    /// It used to implement IDisposable because we hope the resource/memory could be released ASAP
    /// However we do need the TypeDesc/ParamDesc/... types that are holding a IntPtr to function correctly,
    /// so change this to rely on GC instead, assuming that TypeLib API doesn't attach any value resources
    /// to FUNCDESC/VARDESC/TYPEATTR/TYPELIBATTR
    /// </summary>
    internal class TypeAttr
    {
        private readonly TYPEATTR typeattr;
        private readonly IntPtr ipTypeAttr;
        private readonly ITypeInfo typeinfo;

        public TypeAttr(ITypeInfo typeinfo)
        {
            this.typeinfo = typeinfo;
            this.typeinfo.GetTypeAttr(out this.ipTypeAttr);
            this.typeattr = Marshal.PtrToStructure<TYPEATTR>(this.ipTypeAttr);

            this.IdlDesc = new IdlDesc(this.typeattr.idldescType.wIDLFlags);
        }

        ~TypeAttr()
        {
            if (this.ipTypeAttr != IntPtr.Zero)
            {
                this.typeinfo.ReleaseTypeAttr(this.ipTypeAttr);
            }
        }

        public Guid Guid { get { return this.typeattr.guid; } }
        public int SizeInstanceInBytes { get { return this.typeattr.cbSizeInstance; } }
        public TYPEKIND Typekind { get { return this.typeattr.typekind; } }
        public int FuncsCount { get { return this.typeattr.cFuncs; } }
        public int DataFieldCount { get { return this.typeattr.cVars; } }
        public int ImplTypesCount { get { return this.typeattr.cImplTypes; } }
        public int SizeVTableInBytes { get { return this.typeattr.cbSizeVft; } }
        public int Alignment { get { return this.typeattr.cbAlignment; } }
        public TYPEFLAGS TypeFlags { get { return this.typeattr.wTypeFlags; } }
        public int MajorVerNum { get { return this.typeattr.wMajorVerNum; } }
        public int MinorVerNum { get { return this.typeattr.wMinorVerNum; } }
        public TypeDesc TypeDescAlias { get { return new TypeDesc(this, this.typeattr.tdescAlias.desc.lptdesc, this.typeattr.tdescAlias.vt); } }
        public IdlDesc IdlDesc { get; private set; }

        // Expand out TYPEKIND
        public bool IsEnum { get { return Typekind == TYPEKIND.TKIND_ENUM; } }
        public bool IsRecord { get { return Typekind == TYPEKIND.TKIND_RECORD; } }
        public bool IsModule { get { return Typekind == TYPEKIND.TKIND_MODULE; } }
        public bool IsInterface { get { return Typekind == TYPEKIND.TKIND_INTERFACE; } }
        public bool IsIDispatch { get { return Typekind == TYPEKIND.TKIND_DISPATCH; } }
        public bool IsCoClass { get { return Typekind == TYPEKIND.TKIND_COCLASS; } }
        public bool IsAlias { get { return Typekind == TYPEKIND.TKIND_ALIAS; } }
        public bool IsUnion { get { return Typekind == TYPEKIND.TKIND_UNION; } }

        // Expand out TYPEFLAGS
        public bool IsAppObject { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FAPPOBJECT) != 0; } }
        public bool IsCanCreate { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FCANCREATE) != 0; } }
        public bool IsLicensed { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FLICENSED) != 0; } }
        public bool IsPreDeclId { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FPREDECLID) != 0; } }
        public bool IsHidden { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FHIDDEN) != 0; } }
        public bool IsControl { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FCONTROL) != 0; } }
        public bool IsDual { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FDUAL) != 0; } }
        public bool IsNonExtensible { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FNONEXTENSIBLE) != 0; } }
        public bool IsOleAutomation { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FOLEAUTOMATION) != 0; } }
        public bool IsRestricted { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FRESTRICTED) != 0; } }
        public bool IsAggregatable { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FAGGREGATABLE) != 0; } }
        public bool IsReplaceable { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FREPLACEABLE) != 0; } }
        public bool IsDispatchable { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FDISPATCHABLE) != 0; } }
        public bool IsReverseBind { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FREVERSEBIND) != 0; } }
        public bool IsProxy { get { return (TypeFlags & TYPEFLAGS.TYPEFLAG_FPROXY) != 0; } }
    }
}
