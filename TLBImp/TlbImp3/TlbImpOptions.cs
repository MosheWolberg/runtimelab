// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Reflection;

namespace TypeLibUtilities
{
    public sealed class TlbImpOptions
    {
        public string TypeLibName { get; set; }
        public string AssemblyName { get; set; }
        public string AssemblyNamespace { get; set; }
        public string OutputDir { get; set; }
        public byte[] PublicKey { get; set; }
        public StrongNameKeyPair KeyPair { get; set; }
        public string AssemblyRefList { get; set; }
        public string TypeLibRefList { get; set; }
        public Version AssemblyVersion { get; set; }
        public TypeLibImporterFlags Flags { get; set; }
        public bool NoLogo { get; set; }
        public bool SilentMode { get; set; }
        public ICollection<int> SilenceList { get; set; }
        public bool VerboseMode { get; set; }
        public bool StrictRef { get; set; }
        public bool StrictRefNoPia { get; set; }
        public bool SearchPathSucceeded { get; set; }
        public string Product { get; set; }
        public string ProductVersion { get; set; }
        public string Company { get; set; }
        public string Copyright { get; set; }
        public string Trademark { get; set; }
        public bool ConvertVariantBoolFieldToBool { get; set; }
        public bool UseLegacy35QuirksMode { get; set; }
    }
}
