// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;

namespace TypeLibUtilities.TypeLibAPI
{
    internal class TypeLibAttr
    {
        private readonly TYPELIBATTR attr;

        public TypeLibAttr(ITypeLib typelib)
        {
            IntPtr ipAttr;
            typelib.GetLibAttr(out ipAttr);
            try
            {
                this.attr = Marshal.PtrToStructure<TYPELIBATTR>(ipAttr);
            }
            finally
            {
                typelib.ReleaseTLibAttr(ipAttr);
            }
        }

        public Guid Guid { get { return this.attr.guid; } }
        public int Lcid { get { return this.attr.lcid; } }
        public SYSKIND Syskind { get { return this.attr.syskind; } }
        public int MajorVerNum { get { return this.attr.wMajorVerNum; } }
        public int MinorVerNum { get { return this.attr.wMinorVerNum; } }
        public LIBFLAGS LibFlags { get { return this.attr.wLibFlags; } }

        // Expand out SYSKIND
        public bool IsWin16 { get { return Syskind == SYSKIND.SYS_WIN16; } }
        public bool IsWin32 { get { return Syskind == SYSKIND.SYS_WIN32; } }
        public bool IsMac { get { return Syskind == SYSKIND.SYS_MAC; } }
        public bool IsWin64 { get { return Syskind == SYSKIND.SYS_WIN64; } }

        // Expand out LIBFLAGS
        public bool IsRestricted { get { return (LibFlags & LIBFLAGS.LIBFLAG_FRESTRICTED) != 0; } }
        public bool IsControl { get { return (LibFlags & LIBFLAGS.LIBFLAG_FCONTROL) != 0; } }
        public bool IsHidden { get { return (LibFlags & LIBFLAGS.LIBFLAG_FHIDDEN) != 0; } }
        public bool IsHasDiskImage { get { return (LibFlags & LIBFLAGS.LIBFLAG_FHASDISKIMAGE) != 0; } }
    }
}
