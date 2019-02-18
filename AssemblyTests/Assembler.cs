using System;
using System.Collections.Generic;
using System.Security;
using Iced.Intel;

namespace AssemblyTests
{
	public partial class Assembler
	{
		public delegate void BasicActionDelegate();

		private readonly ExecutableCodeWriter _writer = new ExecutableCodeWriter();

		public T Compile<T>(IList<Instruction> instructions) where T : Delegate
		{
			var block = new InstructionBlock(_writer, instructions, _writer.NextFunctionPointer);
			if (!BlockEncoder.TryEncode(IntPtr.Size * 8, block, out var errorMessage))
			{
				throw new InvalidOperationException(errorMessage);
			}

			return _writer.Commit<T>();
		}

		public void Compile<T>(ref T @delegate, IEnumerable<Instruction> instructions) where T : Delegate
		{
			var instructionList = new InstructionList(instructions) { Instruction.Create(Code.Retnq) };
			@delegate = Compile<T>(instructionList);
		}

		public static IEnumerable<Instruction> SpillRegister(Register reg, IEnumerable<Instruction> containedInstructions)
		{
			yield return Instruction.Create(Code.Push_r64, reg);
			foreach (var inst in containedInstructions) { yield return inst; }
			yield return Instruction.Create(Code.Pop_r64, reg);
		}

		public static IEnumerable<Instruction> IncrementStack(uint bytes, IEnumerable<Instruction> containedInstructions)
		{
			yield return Instruction.Create(Code.Sub_rm64_imm32, Register.RSP, bytes);
			foreach (var inst in containedInstructions) { yield return inst; }
			yield return Instruction.Create(Code.Add_rm64_imm32, Register.RSP, bytes);
		}

		public static IEnumerable<Instruction> SaveRcxToANonVolatileRegister(IEnumerable<Instruction> containedInstructions)
		{
			yield return Instruction.Create(Code.Mov_r64_rm64, Register.RSI, Register.RCX);
			foreach (var inst in containedInstructions) { yield return inst; }
			yield return Instruction.Create(Code.Mov_r64_rm64, Register.RCX, Register.RSI);
		}

		public static IEnumerable<Instruction> ZeroStackSSE2(uint stackBytes, byte stackOffset)
		{
			if (!((stackBytes & 0xF) == 0)) { throw new NotImplementedException(); }

			yield return Instruction.Create(Code.VEX_Vzeroupper);
			yield return Instruction.Create(Code.VEX_Vxorps_xmm_xmm_xmmm128, Register.XMM0, Register.XMM0, Register.XMM0);
			
			for (int i = 0; i < stackBytes; i+= 0x10)
			{
				var memory = new MemoryOperand(Register.RSP, i + stackOffset);
				yield return Instruction.Create(Code.VEX_Vmovdqu_xmmm128_xmm, memory, Register.XMM0);
			}
		}

		public static IEnumerable<Instruction> ZeroStack(uint stackBytes, byte stackOffset, byte operandSize)
		{
			//lea     rdi,[rsp+28h]
			var stackOp = new MemoryOperand(Register.RSP, stackOffset);
			yield return Instruction.Create(Code.Lea_r64_m, Register.RDI, stackOp);

			//mov     ecx,20h
			yield return Instruction.Create(Code.Mov_r32_imm32, Register.ECX, stackBytes / operandSize);
			
			//xor     eax,eax
			yield return Instruction.Create(Code.Xor_r32_rm32, Register.EAX, Register.EAX);

			switch (operandSize)
			{
				case 1:
					yield return Instruction.CreateStosb(64, RepPrefixKind.Repe);
					break;
				case 2:
					yield return Instruction.CreateStosw(64, RepPrefixKind.Repe);
					break;
				case 4:
					yield return Instruction.CreateStosd(64, RepPrefixKind.Repe);
					break;
				case 8:
					yield return Instruction.CreateStosq(64, RepPrefixKind.Repe);
					break;
				default:
					throw new InvalidOperationException();
			}
		}
	}
}
