/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (grover) <sharpos@michaelruck.de>
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Mosa.Runtime.Loader; // ?

using Mosa.Runtime.Metadata;
using Mosa.Runtime.Metadata.Tables;

namespace Mosa.Runtime.Metadata
{

	/// <summary>
	/// Metadata root structure according to ISO/IEC 23271:2006 (E), �24.2.1
	/// </summary>
	public class MergedMetadata : IMetadataProvider
	{
		#region Constants

		/// <summary>
		/// Signature constant of the provider root.
		/// </summary>
		private const uint MaxTables = 64;

		/// <summary>
		/// Shift value for tables in TokenTypes Enum
		/// </summary>
		private const byte TableTokenTypeShift = 24;

		#endregion // Constants

		#region Data members

		protected struct ModuleOffset
		{
			public int Start;
			public int End;
			public int Count;

			public ModuleOffset(int start, int count)
			{
				Start = start;
				End = start + count;
				Count = count;
			}
		}

		protected IMetadataModule[] modules;
		protected ModuleOffset[][] moduleOffset;

		#endregion // Data members

		/// <summary>
		/// Initializes a new instance of the <see cref="MetadataRoot"/> class.
		/// </summary>
		public MergedMetadata(IList<IMetadataModule> modules)
		{
			Initialize(modules);
		}

		#region Methods

		protected void Initialize(IList<IMetadataModule> modules)
		{
			this.modules = new IMetadataModule[modules.Count];
			moduleOffset = new ModuleOffset[modules.Count][];

			for (int mod = 0; mod < modules.Count; mod++)
			{
				IMetadataModule module = modules[mod];
				this.modules[mod] = module;

				moduleOffset[mod] = new ModuleOffset[MaxTables];

				for (int table = 0; table < MaxTables; table++)
				{
					int previous = (mod == 0 ? 0 : moduleOffset[mod - 1][table].End);

					TokenTypes entries = module.Metadata.GetMaxTokenValue((TokenTypes)(table << TableTokenTypeShift));

					moduleOffset[mod][table] = new ModuleOffset(previous, (int)(TokenTypes.RowIndexMask & entries));
				}
			}
		}

		protected int GetModuleIndex(IMetadataModule module)
		{
			for (int i = 0; i < modules.Length; i++)
				if (modules[i] == module)
					return i;

			throw new ArgumentException(@"Unable to locate module.", @"module");
		}

		protected bool GetModuleOffset(TokenTypes token, out int module, out int index)
		{
			int table = ((int)(token & TokenTypes.TableMask) >> TableTokenTypeShift);
			int rowindex = (int)(token & TokenTypes.RowIndexMask);

			for (int mod = 0; mod < modules.Length; mod++)
				if ((rowindex > moduleOffset[mod][table].Start) & (rowindex < moduleOffset[mod][table].End))
				{
					module = mod;
					index = rowindex - moduleOffset[mod][table].Start;
					return true;
				}

			throw new ArgumentException(@"Not a valid tokentype.", @"token");
		}

		protected TokenTypes GetOriginalToken(TokenTypes token, out int module)
		{
			int index;

			if (GetModuleOffset(token, out module, out index))
				return (TokenTypes)((token & TokenTypes.RowIndexMask) + index);

			throw new ArgumentException(@"Not a valid tokentype.", @"token");
		}

		protected TokenTypes GetNewToken(int module, TokenTypes token)
		{
			int table = ((int)(token & TokenTypes.TableMask) >> TableTokenTypeShift);
			int offset = moduleOffset[module][table].Start;

			return (TokenTypes)(token + offset);
		}

		#endregion // Methods

		#region IMetadataProvider members

		public int GetMaxTokenCount(TokenTypes token)
		{
			return moduleOffset[modules.Length - 1][((uint)token) >> TableTokenTypeShift].End;
		}

		TokenTypes IMetadataProvider.GetMaxTokenValue(TokenTypes token)
		{
			return (TokenTypes)GetMaxTokenCount(token);
		}

		string IMetadataProvider.ReadString(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			return modules[module].Metadata.ReadString(originalToken);
		}

		Guid IMetadataProvider.ReadGuid(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			return modules[module].Metadata.ReadGuid(originalToken);
		}

		byte[] IMetadataProvider.ReadBlob(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			return modules[module].Metadata.ReadBlob(originalToken);
		}

		ModuleRow IMetadataProvider.ReadModuleRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ModuleRow row = modules[module].Metadata.ReadModuleRow(originalToken);
			return new ModuleRow(
				row.Generation,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.MvidGuidIdx),
				GetNewToken(module, row.EncIdGuidIdx),
				GetNewToken(module, row.EncBaseIdGuidIdx)
			);
		}

		TypeRefRow IMetadataProvider.ReadTypeRefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			TypeRefRow row = modules[module].Metadata.ReadTypeRefRow(originalToken);
			return new TypeRefRow(
				GetNewToken(module, row.ResolutionScopeIdx),
				GetNewToken(module, row.TypeNameIdx),
				GetNewToken(module, row.TypeNamespaceIdx)
			);
		}

		TypeDefRow IMetadataProvider.ReadTypeDefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			TypeDefRow row = modules[module].Metadata.ReadTypeDefRow(originalToken);
			return new TypeDefRow(
				row.Flags,
				GetNewToken(module, row.TypeNameIdx),
				GetNewToken(module, row.TypeNamespaceIdx),
				GetNewToken(module, row.Extends),
				GetNewToken(module, row.FieldList),
				GetNewToken(module, row.MethodList)
			);
		}

		FieldRow IMetadataProvider.ReadFieldRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			FieldRow row = modules[module].Metadata.ReadFieldRow(originalToken);
			return new FieldRow(
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.SignatureBlobIdx)
				);
		}

		MethodDefRow IMetadataProvider.ReadMethodDefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			MethodDefRow row = modules[module].Metadata.ReadMethodDefRow(originalToken);
			return new MethodDefRow(
				row.Rva,
				row.ImplFlags,
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.SignatureBlobIdx),
				GetNewToken(module, row.ParamList)
			);
		}

		ParamRow IMetadataProvider.ReadParamRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ParamRow row = modules[module].Metadata.ReadParamRow(originalToken);
			return new ParamRow(
				row.Flags,
				row.Sequence,
				GetNewToken(module, row.NameIdx)
			);
		}

		InterfaceImplRow IMetadataProvider.ReadInterfaceImplRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			InterfaceImplRow row = modules[module].Metadata.ReadInterfaceImplRow(originalToken);
			return new InterfaceImplRow(
				GetNewToken(module, row.ClassTableIdx),
				GetNewToken(module, row.InterfaceTableIdx)
			);
		}

		MemberRefRow IMetadataProvider.ReadMemberRefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			MemberRefRow row = modules[module].Metadata.ReadMemberRefRow(originalToken);
			return new MemberRefRow(
				GetNewToken(module, row.ClassTableIdx),
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.SignatureBlobIdx)
			);
		}

		ConstantRow IMetadataProvider.ReadConstantRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ConstantRow row = modules[module].Metadata.ReadConstantRow(originalToken);
			return new ConstantRow(
				row.Type,
				GetNewToken(module, row.Parent),
				GetNewToken(module, row.ValueBlobIdx)
			);
		}

		CustomAttributeRow IMetadataProvider.ReadCustomAttributeRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			CustomAttributeRow row = modules[module].Metadata.ReadCustomAttributeRow(originalToken);
			return new CustomAttributeRow(
				GetNewToken(module, row.ParentTableIdx),
				GetNewToken(module, row.TypeIdx),
				GetNewToken(module, row.ValueBlobIdx)
			);
		}

		FieldMarshalRow IMetadataProvider.ReadFieldMarshalRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			FieldMarshalRow row = modules[module].Metadata.ReadFieldMarshalRow(originalToken);
			return new FieldMarshalRow(
				GetNewToken(module, row.ParentTableIdx),
				GetNewToken(module, row.NativeTypeBlobIdx)
			);
		}

		DeclSecurityRow IMetadataProvider.ReadDeclSecurityRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			DeclSecurityRow row = modules[module].Metadata.ReadDeclSecurityRow(originalToken);
			return new DeclSecurityRow(
				row.Action,
				GetNewToken(module, row.ParentTableIdx),
				GetNewToken(module, row.PermissionSetBlobIdx)
			);
		}

		ClassLayoutRow IMetadataProvider.ReadClassLayoutRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ClassLayoutRow row = modules[module].Metadata.ReadClassLayoutRow(originalToken);
			return new ClassLayoutRow(
				row.PackingSize,
				row.ClassSize,
				GetNewToken(module, row.ParentTypeDefIdx)
			);
		}

		FieldLayoutRow IMetadataProvider.ReadFieldLayoutRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			FieldLayoutRow row = modules[module].Metadata.ReadFieldLayoutRow(originalToken);
			return new FieldLayoutRow(
				row.Offset,
				GetNewToken(module, row.Field)
			);
		}

		StandAloneSigRow IMetadataProvider.ReadStandAloneSigRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			StandAloneSigRow row = modules[module].Metadata.ReadStandAloneSigRow(originalToken);
			return new StandAloneSigRow(
				GetNewToken(module, row.SignatureBlobIdx)
			);
		}

		EventMapRow IMetadataProvider.ReadEventMapRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			EventMapRow row = modules[module].Metadata.ReadEventMapRow(originalToken);
			return new EventMapRow(
				GetNewToken(module, row.TypeDefTableIdx),
				GetNewToken(module, row.EventListTableIdx)
			);
		}

		EventRow IMetadataProvider.ReadEventRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			EventRow row = modules[module].Metadata.ReadEventRow(originalToken);
			return new EventRow(
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.EventTypeTableIdx)
			);
		}

		PropertyMapRow IMetadataProvider.ReadPropertyMapRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			PropertyMapRow row = modules[module].Metadata.ReadPropertyMapRow(originalToken);
			return new PropertyMapRow(
				GetNewToken(module, row.ParentTableIdx),
				GetNewToken(module, row.PropertyTableIdx)
			);
		}

		PropertyRow IMetadataProvider.ReadPropertyRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			PropertyRow row = modules[module].Metadata.ReadPropertyRow(originalToken);
			return new PropertyRow(
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.TypeBlobIdx)
			);
		}

		MethodSemanticsRow IMetadataProvider.ReadMethodSemanticsRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			MethodSemanticsRow row = modules[module].Metadata.ReadMethodSemanticsRow(originalToken);
			return new MethodSemanticsRow(
				row.Semantics,
			GetNewToken(module, row.MethodTableIdx),
			GetNewToken(module, row.AssociationTableIdx)
			);
		}

		MethodImplRow IMetadataProvider.ReadMethodImplRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			MethodImplRow row = modules[module].Metadata.ReadMethodImplRow(originalToken);
			return new MethodImplRow(
				GetNewToken(module,row.ClassTableIdx),
				GetNewToken(module, row.MethodBodyTableIdx),
				GetNewToken(module, row.MethodDeclarationTableIdx)
			);
		}

		ModuleRefRow IMetadataProvider.ReadModuleRefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ModuleRefRow row = modules[module].Metadata.ReadModuleRefRow(originalToken);
			return new ModuleRefRow(
				GetNewToken(module, row.NameStringIdx)
			);
		}

		TypeSpecRow IMetadataProvider.ReadTypeSpecRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			TypeSpecRow row = modules[module].Metadata.ReadTypeSpecRow(originalToken);
			return new TypeSpecRow(
				GetNewToken(module, row.SignatureBlobIdx)
			);
		}

		ImplMapRow IMetadataProvider.ReadImplMapRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ImplMapRow row = modules[module].Metadata.ReadImplMapRow(originalToken);
			return new ImplMapRow(
				row.MappingFlags,
			GetNewToken(module, row.MemberForwardedTableIdx),
			GetNewToken(module, row.ImportNameStringIdx),
			GetNewToken(module, row.ImportScopeTableIdx)
			);
		}

		FieldRVARow IMetadataProvider.ReadFieldRVARow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			FieldRVARow row = modules[module].Metadata.ReadFieldRVARow(originalToken);
			return new FieldRVARow(
				row.Rva,
				GetNewToken(module, row.FieldTableIdx)
			);
		}

		AssemblyRow IMetadataProvider.ReadAssemblyRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyRow row = modules[module].Metadata.ReadAssemblyRow(originalToken);
			return new AssemblyRow(
				row.HashAlgId,
				row.MajorVersion,
				row.MinorVersion,
				row.BuildNumber,
				row.Revision,
				row.Flags,
				GetNewToken(module, row.PublicKeyIdx),
				GetNewToken(module, row.NameIdx),
				GetNewToken(module, row.CultureIdx)
			);
		}

		AssemblyProcessorRow IMetadataProvider.ReadAssemblyProcessorRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyProcessorRow row = modules[module].Metadata.ReadAssemblyProcessorRow(originalToken);
			return row; // no change
		}

		AssemblyOSRow IMetadataProvider.ReadAssemblyOSRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyOSRow row = modules[module].Metadata.ReadAssemblyOSRow(originalToken);
			return row; // no change
		}

		AssemblyRefRow IMetadataProvider.ReadAssemblyRefRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyRefRow row = modules[module].Metadata.ReadAssemblyRefRow(originalToken);
			return new AssemblyRefRow(
				row.MajorVersion,
				row.MinorVersion,
				row.BuildNumber,
				row.Revision,
				row.Flags,
				GetNewToken(module, row.PublicKeyOrTokenIdx),
				GetNewToken(module, row.NameIdx),
				GetNewToken(module, row.CultureIdx),
				GetNewToken(module, row.HashValueIdx)
			);
		}

		AssemblyRefProcessorRow IMetadataProvider.ReadAssemblyRefProcessorRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyRefProcessorRow row = modules[module].Metadata.ReadAssemblyRefProcessorRow(originalToken);
			return new AssemblyRefProcessorRow(
				row.Processor,
			GetNewToken(module, row.AssemblyRef)
			);
		}

		AssemblyRefOSRow IMetadataProvider.ReadAssemblyRefOSRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			AssemblyRefOSRow row = modules[module].Metadata.ReadAssemblyRefOSRow(originalToken);
			return new AssemblyRefOSRow(
				row.PlatformId,
				row.MajorVersion,
				row.MinorVersion,
				GetNewToken(module, row.AssemblyRefIdx)
			);
		}

		FileRow IMetadataProvider.ReadFileRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			FileRow row = modules[module].Metadata.ReadFileRow(originalToken);
			return new FileRow(
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.HashValueBlobIdx)
			);
		}

		ExportedTypeRow IMetadataProvider.ReadExportedTypeRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ExportedTypeRow row = modules[module].Metadata.ReadExportedTypeRow(originalToken);
			return new ExportedTypeRow(
				row.Flags,
				GetNewToken(module, row.TypeDefTableIdx),
				GetNewToken(module, row.TypeNameStringIdx),
				GetNewToken(module, row.TypeNamespaceStringIdx),
				GetNewToken(module, row.ImplementationTableIdx)
			);
		}

		ManifestResourceRow IMetadataProvider.ReadManifestResourceRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			ManifestResourceRow row = modules[module].Metadata.ReadManifestResourceRow(originalToken);
			return new ManifestResourceRow(
				row.Offset,
				row.Flags,
				GetNewToken(module, row.NameStringIdx),
				GetNewToken(module, row.ImplementationTableIdx)
			);
		}

		NestedClassRow IMetadataProvider.ReadNestedClassRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			NestedClassRow row = modules[module].Metadata.ReadNestedClassRow(originalToken);
			return new NestedClassRow(
				GetNewToken(module, row.NestedClassTableIdx),
				GetNewToken(module, row.EnclosingClassTableIdx)
			);
		}

		GenericParamRow IMetadataProvider.ReadGenericParamRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			GenericParamRow row = modules[module].Metadata.ReadGenericParamRow(originalToken);
			return new GenericParamRow(
				row.Number,
				row.Flags,
				GetNewToken(module, row.OwnerTableIdx),
				GetNewToken(module, row.NameStringIdx)
			);
		}

		MethodSpecRow IMetadataProvider.ReadMethodSpecRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			MethodSpecRow row = modules[module].Metadata.ReadMethodSpecRow(originalToken);
			return new MethodSpecRow(
				GetNewToken(module, row.MethodTableIdx),
				GetNewToken(module, row.InstantiationBlobIdx)
			);
		}

		GenericParamConstraintRow IMetadataProvider.ReadGenericParamConstraintRow(TokenTypes token)
		{
			int module;
			TokenTypes originalToken = GetOriginalToken(token, out module);

			GenericParamConstraintRow row = modules[module].Metadata.ReadGenericParamConstraintRow(originalToken);
			return new GenericParamConstraintRow(
				GetNewToken(module, row.OwnerTableIdx),
				GetNewToken(module, row.ConstraintTableIdx)
			);
		}

		#endregion // IMetadataProvider members
	}
}