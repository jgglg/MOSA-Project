/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (grover) <sharpos@michaelruck.de>
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Mosa.Runtime.Metadata.Signatures
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class Signature
	{
		private TokenTypes token;

		public TokenTypes Token
		{
			get
			{
				return token;
			}
		}


		/// <summary>
		/// Loads the signature.
		/// </summary>
		/// <param name="provider">The provider.</param>
		/// <param name="token">The token.</param>
		public void LoadSignature(ISignatureContext context, IMetadataProvider provider, TokenTypes token)
		{
			byte[] buffer = provider.ReadBlob(token);

			int index = 0;
			this.ParseSignature(context, buffer, ref index);
			Debug.Assert(index == buffer.Length, @"Signature parser didn't complete.");

			this.token = token;
		}

		/// <summary>
		/// Parses the signature.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		/// <param name="index">The index.</param>
		protected abstract void ParseSignature(ISignatureContext context, byte[] buffer, ref int index);

		/// <summary>
		/// Froms the member ref signature token.
		/// </summary>
		/// <param name="provider">The provider.</param>
		/// <param name="token">The token.</param>
		/// <returns></returns>
		public static Signature FromMemberRefSignatureToken(ISignatureContext context, IMetadataProvider provider, TokenTypes token)
		{
			Signature result;
			int index = 0;
			byte[] buffer = provider.ReadBlob(token);

			if (0x06 == buffer[0])
			{
				result = new FieldSignature();
				result.ParseSignature(context, buffer, ref index);
			}
			else
			{
				result = new MethodSignature();
				result.ParseSignature(context, buffer, ref index);
			}
			Debug.Assert(index == buffer.Length, @"Not all signature bytes read.");
			return result;
		}
	}
}
