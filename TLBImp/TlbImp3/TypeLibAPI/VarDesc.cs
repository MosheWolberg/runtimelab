// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;

namespace TypeLibUtilities.TypeLibAPI
{
    /// <summary>
    /// A wrapper for VARDESC
    /// It used to implement IDisposable because we hope the resource/memory could be released ASAP
    /// However we do need the TypeDesc/ParamDesc/... types that are holding a IntPtr to function correctly,
    /// so change this to rely on GC instead, assuming that TypeLib API doesn't attach any value resources
    /// to FUNCDESC/VARDESC/TYPEATTR/TYPELIBATTR
    /// </summary>
    internal class VarDesc
    {
        private readonly ITypeInfo typeinfo;
        private readonly IntPtr ipVarDesc;
        private readonly VARDESC vardesc;

        public VarDesc(ITypeInfo typeinfo, int index)
        {
            this.typeinfo = typeinfo;
            this.typeinfo.GetVarDesc(index, out this.ipVarDesc);
            this.vardesc = Marshal.PtrToStructure<VARDESC>(this.ipVarDesc);
        }

        ~VarDesc()
        {
            if (this.ipVarDesc != IntPtr.Zero)
            {
                this.typeinfo.ReleaseVarDesc(this.ipVarDesc);
            }
        }

        public int MemberId { get { return this.vardesc.memid; } }
        public int OffsetInBytes { get { return this.vardesc.desc.oInst; } }
        public IntPtr VarValue { get { return this.vardesc.desc.lpvarValue; } }
        public ElemDesc ElemDescVar { get { return new ElemDesc(this, this.vardesc.elemdescVar); } }
        public int VarFlags { get { return this.vardesc.wVarFlags; } }
        public VARKIND VarKind { get { return this.vardesc.varkind; } }

        // Expand out VARKIND
        public bool IsPerInstance { get { return VarKind == VARKIND.VAR_PERINSTANCE; } }
        public bool IsStatic { get { return VarKind == VARKIND.VAR_STATIC; } }
        public bool IsConst { get { return VarKind == VARKIND.VAR_CONST; } }
        public bool IsDispath { get { return VarKind == VARKIND.VAR_DISPATCH; } }

        // Expand out wVarFlags
        public bool IsReadOnly { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FREADONLY) != 0; } }
        public bool IsSource { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FSOURCE) != 0; } }
        public bool IsBindable { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FBINDABLE) != 0; } }
        public bool IsRequestEdit { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FREQUESTEDIT) != 0; } }
        public bool IsDisplayBind { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FDISPLAYBIND) != 0; } }
        public bool IsDefaultBind { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FDEFAULTBIND) != 0; } }
        public bool IsHidden { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FHIDDEN) != 0; } }
        public bool IsRestricted { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FRESTRICTED) != 0; } }
        public bool IsDefaultCollElem { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FDEFAULTCOLLELEM) != 0; } }
        public bool IsUIDefault { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FUIDEFAULT) != 0; } }
        public bool IsNonBrowsable { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FNONBROWSABLE) != 0; } }
        public bool IsReplaceable { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FREPLACEABLE) != 0; } }
        public bool IsImmediateBind { get { return ((int)VarFlags & (int)VARFLAGS.VARFLAG_FIMMEDIATEBIND) != 0; } }
    }
}
