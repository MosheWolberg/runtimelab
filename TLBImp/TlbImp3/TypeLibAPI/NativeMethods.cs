// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;

namespace TypeLibUtilities.TypeLibAPI
{
    // Enum passed in to LoadTypeLibEx.
    [Flags]
    internal enum REGKIND
    {
        REGKIND_DEFAULT = 0,
        REGKIND_REGISTER = 1,
        REGKIND_NONE = 2,
        REGKIND_LOAD_TLB_AS_32BIT = 0x20,
        REGKIND_LOAD_TLB_AS_64BIT = 0x40,
    }

    internal static class OleAut32
    {

        /// https://docs.microsoft.com/en-us/windows/desktop/api/oleauto/nf-oleauto-loadtypelib
        [DllImport(nameof(OleAut32))]
        public static extern int LoadTypeLib(
            [MarshalAs(UnmanagedType.BStr)] string strFile,
            out ITypeLib typeLib);

        /// https://docs.microsoft.com/en-us/windows/desktop/api/oleauto/nf-oleauto-loadtypelibex
        [DllImport(nameof(OleAut32), PreserveSig = false, CharSet = CharSet.Unicode)]
        public static extern void LoadTypeLibEx(
            string name,
            REGKIND regKind,
            out ITypeLib typeLib);

        /// https://docs.microsoft.com/en-us/windows/desktop/api/oleauto/nf-oleauto-querypathofregtypelib
        [DllImport(nameof(OleAut32))]
        public static extern int QueryPathOfRegTypeLib(
            [In]ref Guid guid,
            ushort verMajor,
            ushort verMinor,
            int lcid,
            [MarshalAs(UnmanagedType.BStr)] out string pathName);
    }
}
