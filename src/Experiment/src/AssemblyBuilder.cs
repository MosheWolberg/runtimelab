// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using static System.Reflection.Emit.Experimental.EntityWrappers;

namespace System.Reflection.Emit.Experimental
{
    public class AssemblyBuilder : System.Reflection.Assembly
    {
        private bool _previouslySaved = false;
        private AssemblyName _assemblyName;
        private ModuleBuilder? _module;
        private MetadataLoadContext? _metadataLoadContext;
        private Assembly? _coreAssembly;
        internal List<CustomAttributeWrapper> AssemblyAttributes = new List<CustomAttributeWrapper>();
        internal MetadataBuilder Metadata = new MetadataBuilder();
        private AssemblyBuilder(AssemblyName name, MetadataLoadContext? metadataLoadContext, System.Collections.Generic.IEnumerable<System.Reflection.Emit.Experimental.CustomAttributeBuilder>? assemblyAttributes)
        {
            _assemblyName = name;
            _metadataLoadContext = metadataLoadContext;
            if (metadataLoadContext != null)
            {
                _coreAssembly = metadataLoadContext.CoreAssembly;
                if (_coreAssembly == null)
                {
                    throw new ArgumentException("Could not load core assembly");
                }
            }

            if (assemblyAttributes != null)
            {
                foreach (CustomAttributeBuilder customAttributeBuilder in assemblyAttributes)
                {
                    ConstructorInfo constructorInfo = customAttributeBuilder.Constructor;
                    byte[] binaryAttribute = customAttributeBuilder._blob;

                    if (constructorInfo == null)
                    {
                        throw new ArgumentException(nameof(constructorInfo));
                    }

                    if (binaryAttribute == null)
                    {
                        throw new ArgumentException(nameof(binaryAttribute));
                    }

                    if (constructorInfo.DeclaringType == null)
                    {
                        throw new ArgumentException("Attribute constructor has no type.");
                    }

                    // We check whether the custom attribute is actually a pseudo-custom attribute.
                    // (We have only done ComImport for the prototype, eventually all pseudo-custom attributes will be hard-coded.)
                    // If it is, simply alter the TypeAttributes.
                    // We want to handle this before the type metadata is generated.
                    if (constructorInfo.DeclaringType.Name.Equals("ComImportAttribute"))
                    {
                        Debug.WriteLine("Modifying internal flags");
                    }
                    else
                    {
                        CustomAttributeWrapper customAttribute = new CustomAttributeWrapper(constructorInfo, binaryAttribute);
                        AssemblyAttributes.Add(customAttribute);
                    }
                }
            }
        }

        public static System.Reflection.Emit.Experimental.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access, MetadataLoadContext? loadContext = null)
        {
            if (name == null || name.Name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return DefineDynamicAssembly(name, access, loadContext);
        }

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Defining a dynamic assembly requires dynamic code.")]
        public static System.Reflection.Emit.Experimental.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access, System.Collections.Generic.IEnumerable<System.Reflection.Emit.Experimental.CustomAttributeBuilder>? assemblyAttributes, MetadataLoadContext? loadContext = null)
        {
            if (name == null || name.Name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            // AssemblyBuilderAccess affects runtime management only and is not relevant for saving to disk.
            AssemblyBuilder currentAssembly = new AssemblyBuilder(name, loadContext, assemblyAttributes);
            return currentAssembly;
        }

        public void Save(string assemblyFileName)
        {
            if (_previouslySaved) // You cannot save an assembly multiple times. This is consistent with Save() in .Net Framework.
            {
                throw new InvalidOperationException("Cannot save an assembly multiple times");
            }

            if (assemblyFileName == null)
            {
                throw new ArgumentNullException(nameof(assemblyFileName));
            }

            if (_assemblyName == null || _assemblyName.Name == null)
            {
                throw new ArgumentException(nameof(_assemblyName));
            }

            if (_module == null)
            {
                throw new InvalidOperationException("Assembly needs at least one module defined");
            }

            // Add assembly metadata
            var assemblyHandle = Metadata.AddAssembly( // Metadata is added for the new assembly - Current design - metadata generated only when Save method is called.
               Metadata.GetOrAddString(value: _assemblyName.Name),
               version: _assemblyName.Version ?? new Version(0, 0, 0, 0),
               culture: (_assemblyName.CultureName == null) ? default : Metadata.GetOrAddString(value: _assemblyName.CultureName),
               publicKey: (_assemblyName.GetPublicKey() is byte[] publicKey) ? Metadata.GetOrAddBlob(value: publicKey) : default,
               flags: (AssemblyFlags)_assemblyName.Flags,
               hashAlgorithm: AssemblyHashAlgorithm.None); // AssemblyName.HashAlgorithm is obsolete so default value used.

            foreach (CustomAttributeWrapper customAttribute in AssemblyAttributes)
            {
                EntityHandle constructorHandle = _module.AddorGetMethodReference(customAttribute.ConstructorInfo);
                customAttribute.ConToken = constructorHandle;
                MetadataHelper.AddCustomAttr(Metadata, customAttribute, assemblyHandle);
            }

            // Add module's metadata
            _module.AppendMetadata();

            using var peStream = new FileStream(assemblyFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var ilBuilder = new BlobBuilder();
            WritePEImage(peStream, Metadata, ilBuilder);
            _previouslySaved = true;
        }

        public System.Reflection.Emit.Experimental.ModuleBuilder DefineDynamicModule(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(nameof(name) + " is empty.");
            }

            if (_module != null)
            {
                throw new InvalidOperationException("Multi-module assemblies are not supported");
            }

            ModuleBuilder moduleBuilder = new ModuleBuilder(name, this, _coreAssembly, Metadata);
            _module = moduleBuilder;
            return moduleBuilder;
        }

        public System.Reflection.Emit.Experimental.ModuleBuilder? GetDynamicModule(string name) // Passing in a string here is really only a legacy from multi-module assemblies.
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException($"{nameof(name)} cannot have zero characters.");
            }

            if (_module == null)
            {
                return null;
            }
            else if (_module.Name.Equals(name))
            {
                return _module;
            }

            return null;
        }

        private static void WritePEImage(Stream peStream, MetadataBuilder metadataBuilder, BlobBuilder ilBuilder) // MethodDefinitionHandle entryPointHandle when we have main method.
        {
            // Create executable with the managed metadata from the specified MetadataBuilder.
            var peHeaderBuilder = new PEHeaderBuilder(
                imageCharacteristics: Characteristics.Dll);

            var peBuilder = new ManagedPEBuilder(
                peHeaderBuilder,
                new MetadataRootBuilder(metadataBuilder),
                ilBuilder,
                flags: CorFlags.ILOnly,
                deterministicIdProvider: content => new BlobContentId(Guid.NewGuid(), 0x04030201));

            // Write executable into the specified stream.
            var peBlob = new BlobBuilder();
            BlobContentId contentId = peBuilder.Serialize(peBlob);
            peBlob.WriteContentTo(peStream);
        }
    }
}