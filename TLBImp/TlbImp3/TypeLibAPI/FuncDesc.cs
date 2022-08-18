// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TypeLibUtilities.TypeLibAPI
{
    internal class Utils
    {
        public static IntPtr AdvancePointer(IntPtr p, int offset)
        {
            return new IntPtr(p.ToInt64() + offset);
        }

        public static IntPtr AdvancePointer(IntPtr p, IntPtr offset)
        {
            return new IntPtr(p.ToInt64() + offset.ToInt64());
        }
    }

    internal class ParamDesc
    {
        // hold on to parent to avoid GC hole
        // because we need the memory pointed by IntPtr to be alive
        private readonly object parent;

        public ParamDesc(object parent, IntPtr varValue, PARAMFLAG flags)
        {
            this.parent = parent;
            this.VarValue = varValue;
            this.ParamFlags = flags;
        }

        public PARAMFLAG ParamFlags { get; private set; }
        public IntPtr VarValue { get; private set; }

        public bool IsIn { get { return (PARAMFLAG.PARAMFLAG_FIN & ParamFlags) != 0; } }
        public bool IsOut { get { return (PARAMFLAG.PARAMFLAG_FOUT & ParamFlags) != 0; } }
        public bool IsLCID { get { return (PARAMFLAG.PARAMFLAG_FLCID & ParamFlags) != 0; } }
        public bool IsRetval { get { return (PARAMFLAG.PARAMFLAG_FRETVAL & ParamFlags) != 0; } }
        public bool IsOpt { get { return (PARAMFLAG.PARAMFLAG_FOPT & ParamFlags) != 0; } }
        public bool HasDefault { get { return (PARAMFLAG.PARAMFLAG_FHASDEFAULT & ParamFlags) != 0; } }
        public bool HasCustData { get { return (PARAMFLAG.PARAMFLAG_FHASCUSTDATA & ParamFlags) != 0; } }
    }

    internal class IdlDesc
    {
        public IdlDesc(IDLFLAG flags)
        {
            this.IDLFlags = flags;
        }

        public IDLFLAG IDLFlags { get; private set; }

        public bool IsIn { get { return (IDLFLAG.IDLFLAG_FIN & IDLFlags) != 0; } }
        public bool IsOut { get { return (IDLFLAG.IDLFLAG_FOUT & IDLFlags) != 0; } }
        public bool IsLCID { get { return (IDLFLAG.IDLFLAG_FLCID & IDLFlags) != 0; } }
        public bool IsRetval { get { return (IDLFLAG.IDLFLAG_FRETVAL & IDLFlags) != 0; } }
    }

    internal class TypeDesc
    {
        // hold on to parent to avoid GC hole
        // because we need the memory pointed by IntPtr to be alive
        private readonly object parent;
        private readonly IntPtr lptdesc;

        public TypeDesc(object parent, IntPtr typeDesc, int vt)
        {
            this.parent = parent;
            this.lptdesc = typeDesc;
            this.VarType = vt;
        }

        public int VarType { get; private set; }

        public int HRefType => (int)(this.lptdesc.ToInt64() & 0xffffffff);

        public TypeDesc InnerTypeDesc
        {
            get
            {
                // The reason we need to get the TYPEDESC here is that we don't know
                // whether the typeDesc is valid or not until now
                TYPEDESC typeDesc = Marshal.PtrToStructure<TYPEDESC>(this.lptdesc);
                return new TypeDesc(this, typeDesc.desc.lptdesc, typeDesc.vt);
            }
        }

        public ArrayDesc InnerArrayDesc
        {
            get
            {
                return new ArrayDesc(this, this.lptdesc);
            }
        }

        public TypeInfo GetUserDefinedTypeInfo(TypeInfo typeinfo)
        {
            return typeinfo.GetRefTypeInfo(this.HRefType);
        }
    }

    internal class ArrayDesc
    {
        // hold on to parent to avoid GC hole
        // because we need the memory pointed by IntPtr to be alive
        private readonly object parent;
        private readonly IntPtr value;

        public ArrayDesc(object parent, IntPtr pArrayDesc)
        {
            this.parent = parent;
            this.value = pArrayDesc;
        }

        public SAFEARRAYBOUND[] GetBounds()
        {
            IntPtr arrayDesc = this.value;

            ushort dims = (ushort)Marshal.ReadInt16(Utils.AdvancePointer(arrayDesc, Marshal.SizeOf(typeof(TYPEDESC))));

            arrayDesc = Utils.AdvancePointer(arrayDesc, Marshal.OffsetOf(typeof(ARRAYDESC), "firstBound"));

            var bounds = new List<SAFEARRAYBOUND>();
            int safeArrayBoundSize = Marshal.SizeOf(typeof(SAFEARRAYBOUND));
            for (int i = 0; i < dims; ++i)
            {
                SAFEARRAYBOUND bound = (SAFEARRAYBOUND)Marshal.PtrToStructure(arrayDesc, typeof(SAFEARRAYBOUND));
                bounds.Add(bound);

                arrayDesc = Utils.AdvancePointer(arrayDesc, safeArrayBoundSize);
            }

            return bounds.ToArray();
        }

        public TypeDesc TypeDescElement
        {
            get
            {
                TYPEDESC typeDesc = Marshal.PtrToStructure<TYPEDESC>(this.value);
                return new TypeDesc(this, typeDesc.desc.lptdesc, typeDesc.vt);
            }
        }

    }

    internal class ElemDesc
    {
        // hold on to parent to avoid GC hole
        // because we need the memory pointed by IntPtr to be alive
        private readonly object parent;

        private IntPtr tdesc;
        private int vt;
        private IntPtr varValue;
        private int flags;

        public ElemDesc(VarDesc varDesc, ELEMDESC elemdesc)
        {
            this.parent = varDesc;
            Init(elemdesc);
        }

        public ElemDesc(FuncDesc funcdesc, ELEMDESC elemdesc)
        {
            this.parent = funcdesc;
            Init(elemdesc);
        }

        public TypeDesc TypeDesc { get { return new TypeDesc(this, this.tdesc, this.vt); } }
        public ParamDesc ParamDesc { get { return new ParamDesc(this, this.varValue, (PARAMFLAG)flags); } }
        public IdlDesc IdlDesc { get { return new IdlDesc((IDLFLAG)flags); } }

        private void Init(ELEMDESC elemDesc)
        {
            this.tdesc = elemDesc.tdesc.desc.lptdesc;
            this.vt = elemDesc.tdesc.vt;
            this.varValue = elemDesc.desc.paramdesc.lpVarValue;
            this.flags = (int)elemDesc.desc.paramdesc.wParamFlags;
        }
    }

    /// <summary>
    /// A wrapper for FUNCDESC
    /// It used to implement IDisposable because we hope the resource/memory could be released ASAP
    /// However we do need the TypeDesc/ParamDesc/... types that are holding a IntPtr to function correctly,
    /// so change this to rely on GC instead, assuming that TypeLib API doesn't attach any value resources
    /// to FUNCDESC/VARDESC/TYPEATTR/TYPELIBATTR
    /// </summary>
    internal class FuncDesc
    {
        private readonly FUNCDESC funcdesc;
        private readonly IntPtr ipFuncDesc;
        private readonly ITypeInfo typeinfo;

        public FuncDesc(ITypeInfo typeinfo, int index)
        {
            this.typeinfo = typeinfo;
            this.typeinfo.GetFuncDesc(index, out this.ipFuncDesc);
            this.funcdesc = Marshal.PtrToStructure<FUNCDESC>(this.ipFuncDesc);
        }

        ~FuncDesc()
        {
            if (this.ipFuncDesc != IntPtr.Zero)
            {
                this.typeinfo.ReleaseFuncDesc(this.ipFuncDesc);
            }
        }

        public int MemberId { get { return this.funcdesc.memid; } }
        public FUNCKIND Funckind { get { return this.funcdesc.funckind; } }
        public INVOKEKIND Invkind { get { return this.funcdesc.invkind; } }
        public CALLCONV Callconv { get { return this.funcdesc.callconv; } }
        public int ParamCount { get { return this.funcdesc.cParams; } }
        public int ParamOptCount { get { return this.funcdesc.cParamsOpt; } }
        public int VTableOffset { get { return this.funcdesc.oVft; } }
        public int ReturnCodeCount { get { return this.funcdesc.cScodes; } }
        public ElemDesc ElemDescFunc { get { return new ElemDesc(this, this.funcdesc.elemdescFunc); } }
        public int FuncFlags { get { return this.funcdesc.wFuncFlags; } }

        public ElemDesc GetElemDesc(int index)
        {
            IntPtr pParam = this.funcdesc.lprgelemdescParam;
            IntPtr pElemDesc = Utils.AdvancePointer(pParam, index * Marshal.SizeOf(typeof(ELEMDESC)));
            return new ElemDesc(this, Marshal.PtrToStructure<ELEMDESC>(pElemDesc));
        }

        // Expand out FUNCKIND
        public bool IsVirtual { get { return Funckind == FUNCKIND.FUNC_VIRTUAL; } }
        public bool IsPureVirtual { get { return Funckind == FUNCKIND.FUNC_PUREVIRTUAL; } }
        public bool IsNonVirtual { get { return Funckind == FUNCKIND.FUNC_NONVIRTUAL; } }
        public bool IsStatic { get { return Funckind == FUNCKIND.FUNC_STATIC; } }
        public bool IsDispatch { get { return Funckind == FUNCKIND.FUNC_DISPATCH; } }

        // Expand out INVOKEKIND
        public bool IsFunc { get { return Invkind == INVOKEKIND.INVOKE_FUNC; } }
        public bool IsPropertyGet { get { return Invkind == INVOKEKIND.INVOKE_PROPERTYGET; } }
        public bool IsPropertyPut { get { return Invkind == INVOKEKIND.INVOKE_PROPERTYPUT; } }
        public bool IsPropertyPutRef { get { return Invkind == INVOKEKIND.INVOKE_PROPERTYPUTREF; } }

        // Expand out CALLCONV
        public bool IsCdecl { get { return Callconv == CALLCONV.CC_CDECL; } }
        public bool IsPascal { get { return Callconv == CALLCONV.CC_PASCAL; } }
        public bool IsStdCall { get { return Callconv == CALLCONV.CC_STDCALL; } }
        public bool IsSysCall { get { return Callconv == CALLCONV.CC_SYSCALL; } }

        // Expand out FUNCFLAGS
        public bool IsRestricted { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FRESTRICTED) != 0; } }
        public bool IsSource { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FSOURCE) != 0; } }
        public bool IsBindable { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FBINDABLE) != 0; } }
        public bool IsRequestEdit { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FREQUESTEDIT) != 0; } }
        public bool IsDisplayBind { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FDISPLAYBIND) != 0; } }
        public bool IsDefaultBind { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FDEFAULTBIND) != 0; } }
        public bool IsHidden { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FHIDDEN) != 0; } }
        public bool IsUsesGetLastError { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FUSESGETLASTERROR) != 0; } }
        public bool IsDefaultCollElem { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FDEFAULTCOLLELEM) != 0; } }
        public bool IsUIDefault { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FUIDEFAULT) != 0; } }
        public bool IsNonBrowsable { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FNONBROWSABLE) != 0; } }
        public bool IsReplaceable { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FREPLACEABLE) != 0; } }
        public bool IsImmediateBind { get { return (FuncFlags & (int)FUNCFLAGS.FUNCFLAG_FIMMEDIATEBIND) != 0; } }
    }
}
