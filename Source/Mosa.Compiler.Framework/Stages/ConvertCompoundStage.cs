﻿/*
 * (c) 2014 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Ki (kiootic) <kiootic@gmail.com>
 */

using Mosa.Compiler.Framework.IR;
using System.Collections.Generic;

// FIXME: This stage depends on Node.Next & Node.Previous properties but the compiler may insert empty nodes.
// Rewrite not to depend on those properities.

namespace Mosa.Compiler.Framework.Stages
{
	/// <summary>
	/// This stage identifies move/load/store/call instructions with compound types
	/// (i.e. user defined value types with size greater than sans enums) operands and
	/// convert them into respective compound instructions, which will be expanded into
	/// multiple native instructions by platform layer.
	/// </summary>
	public class ConvertCompoundStage : BaseMethodCompilerStage
	{
		private Dictionary<Operand, Operand> repl = new Dictionary<Operand, Operand>();

		protected override void Run()
		{
			for (int index = 0; index < BasicBlocks.Count; index++)
				for (var node = BasicBlocks[index].First; !node.IsBlockEndInstruction; node = node.Next)
					if (!node.IsEmpty)
						ProcessInstruction(node);

			for (int index = 0; index < BasicBlocks.Count; index++)
				for (var node = BasicBlocks[index].First; !node.IsBlockEndInstruction; node = node.Next)
					if (!node.IsEmpty)
						ReplaceOperands(node);
		}

		protected override void Finish()
		{
			repl = null;
		}

		private void ProcessInstruction(InstructionNode node)
		{
			if (node.Instruction == IRInstruction.Load)
			{
				if (node.MosaType != null &&
					TypeLayout.IsCompoundType(node.MosaType) && !node.MosaType.IsUI8 && !node.MosaType.IsR8)
				{
					if (node.Result.IsVirtualRegister && !repl.ContainsKey(node.Result))
					{
						repl[node.Result] = MethodCompiler.StackLayout.AddStackLocal(node.MosaType);
					}
					node.ReplaceInstructionOnly(IRInstruction.CompoundLoad);
				}
			}
			else if (node.Instruction == IRInstruction.Store)
			{
				if (node.MosaType != null &&
					TypeLayout.IsCompoundType(node.MosaType) && !node.MosaType.IsUI8 && !node.MosaType.IsR8)
				{
					if (node.Operand3.IsVirtualRegister && !repl.ContainsKey(node.Operand3))
					{
						repl[node.Operand3] = MethodCompiler.StackLayout.AddStackLocal(node.Result.Type);
					}
					node.ReplaceInstructionOnly(IRInstruction.CompoundStore);
				}
			}
			else if (node.Instruction == IRInstruction.Move)
			{
				if (node.Result.Type.Equals(node.Operand1.Type) &&
					TypeLayout.IsCompoundType(node.Result.Type) && !node.Result.Type.IsUI8 && !node.Result.Type.IsR8)
				{
					// If this move is proceded by a return then remove this instruction
					// It is basically a double up caused by some instructions result in the same instruction output
					if (node.Next.Instruction == IRInstruction.Return && node.Next.Operand1 == node.Result)
					{
						node.Next.Operand1 = node.Operand1;

						var nopNode = new InstructionNode(IRInstruction.Nop);
						node.Previous.Insert(nopNode);

						node.Empty();
						return;
					}

					// If this move is preceded by a compound move (which will turn into a compound move) remove this instruction
					// It is basically a double up caused by some instructions result in the same IR output
					if ((node.Previous.Instruction == IRInstruction.CompoundMove
							|| node.Previous.Instruction == IRInstruction.CompoundLoad
							|| node.Previous.Instruction == IRInstruction.Call)
						&& node.Previous.Result == node.Operand1)
					{
						if (repl.ContainsKey(node.Previous.Result))
						{
							repl[node.Result] = repl[node.Previous.Result];
							repl.Remove(node.Previous.Result);
						}
						node.Previous.Result = node.Result;

						var nopNode = new InstructionNode(IRInstruction.Nop);
						node.Previous.Insert(nopNode);

						node.Empty();
						return;
					}

					if (node.Result.IsVirtualRegister && !repl.ContainsKey(node.Result))
					{
						repl[node.Result] = MethodCompiler.StackLayout.AddStackLocal(node.Result.Type);
					}
					if (node.Operand1.IsVirtualRegister && !repl.ContainsKey(node.Operand1))
					{
						repl[node.Operand1] = MethodCompiler.StackLayout.AddStackLocal(node.Operand1.Type);
					}
					node.ReplaceInstructionOnly(IRInstruction.CompoundMove);
				}
			}
			else if (node.Instruction == IRInstruction.Call)
			{
				if (node.Result != null &&
					TypeLayout.IsCompoundType(node.Result.Type) && !node.Result.Type.IsUI8 && !node.Result.Type.IsR8)
				{
					if (node.Result.IsVirtualRegister && !repl.ContainsKey(node.Result))
					{
						repl[node.Result] = MethodCompiler.StackLayout.AddStackLocal(node.Result.Type);
					}
				}
			}
		}

		private void ReplaceOperands(InstructionNode node)
		{
			if (node.Result != null && repl.ContainsKey(node.Result))
				node.Result = repl[node.Result];

			if (node.Result2 != null && repl.ContainsKey(node.Result2))
				node.Result2 = repl[node.Result2];

			int count = node.OperandCount;
			for (int i = 0; i < count; i++)
			{
				var operand = node.GetOperand(i);
				if (operand != null && repl.ContainsKey(operand))
					node.SetOperand(i, repl[operand]);
			}
		}
	}
}
