// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Text;
using System.Globalization;
using System.Security;
using TypeLibUtilities.TypeLibAPI;
using System.Diagnostics;

using SysPropertyInfo = System.Reflection.PropertyInfo;

namespace TypeLibUtilities
{
    internal class AssemblySourceWriter
    {
        private const string SafeComment = "////";
        private const string SingleIndent = "    ";
        private readonly Assembly assembly;

        public AssemblySourceWriter(Assembly assembly)
        {
            this.assembly = assembly;
        }

        public void Write(TextWriter stream)
        {
            var model = new FileModel();
            OnAssembly(this.assembly, "", ref model);

            // Write banner
            stream.WriteLine(model.Banner.ToString());

            // Write usings
            stream.WriteLine(model.Usings.ToString());

            // Write content
            stream.WriteLine(model.Content.ToString());
        }

        private static void OnAssembly(Assembly asm, string indent, ref FileModel model)
        {
            model.Content.AppendLine($@"{indent}{SafeComment} BEGIN Assembly: {asm.FullName}");

            foreach (Module m in asm.GetModules())
            {
                OnModule(m, indent, ref model);
            }

            model.Content.AppendLine($@"{indent}{SafeComment} END Assembly: {asm.FullName}");
        }

        private static void OnModule(Module mod, string indent, ref FileModel model)
        {
            model.Content.AppendLine($@"{indent}{SafeComment} BEGIN Module: {mod.Name}");

            foreach (Type t in mod.GetTypes())
            {
                OnType(t, indent, ref model);
            }

            model.Content.AppendLine($@"{indent}{SafeComment} END Module: {mod.Name}");
        }

        private static void OnType(Type type, string indent, ref FileModel model)
        {
            model.Content.AppendLine($@"{indent}{SafeComment} BEGIN Type: {type.Name}");

            model.Content.AppendLine(
$@"{indent}namespace {type.Namespace}
{indent}{{");
            model.Content.AppendLine(
$@"{indent}class {type.Name}
{indent}{{");

            string innerIndent = indent + SingleIndent;
            foreach (SysPropertyInfo pi in type.GetProperties())
            {
                OnProperty(pi, innerIndent, ref model);
            }

            foreach (MethodInfo mi in type.GetMethods())
            {
                OnMethod(mi, innerIndent, ref model);
            }


            model.Content.AppendLine(
$@"{indent}}} {SafeComment} {type.Name} ");
            model.Content.AppendLine(
$@"{indent}}} {SafeComment} {type.Namespace} ");

            model.Content.AppendLine($@"{indent}{SafeComment} END Type: {type.Name}");
        }

        private static void OnProperty(SysPropertyInfo pi, string indent, ref FileModel model)
        {
            model.Content.AppendLine($@"{indent}{SafeComment} BEGIN Property: {pi.Name}");
            model.Content.AppendLine($@"{indent}{SafeComment} END Property: {pi.Name}");
        }

        private static void OnMethod(MethodInfo mi, string indent, ref FileModel model)
        {
            model.Content.AppendLine($@"{indent}{SafeComment} BEGIN Method: {mi.Name}");
            model.Content.AppendLine($@"{indent}{SafeComment} END Method: {mi.Name}");
        }

        private class FileModel
        {
            public StringBuilder Banner { get; } = new StringBuilder();
            public StringBuilder Usings { get; } = new StringBuilder();
            public StringBuilder Content { get; } = new StringBuilder();
        }
    }
}
