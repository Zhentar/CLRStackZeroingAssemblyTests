using System;
using System.Collections.Generic;
using System.Security;
using Iced.Intel;

namespace AssemblyTests
{
	public partial class Assembler
	{
		[SuppressUnmanagedCodeSecurity]
		public delegate void BasicActionDelegate();

		private readonly ExecutableCodeWriter _writer = new ExecutableCodeWriter();

		public T Compile<T>(IList<Instruction> instructions) where T : Delegate
		{
			var block = new InstructionBlock(_writer, instructions, _writer.NextFunctionPointer);
			var bitness = Environment.Is64BitProcess ? 64 : 32;
			if (!BlockEncoder.TryEncode(bitness, block, out var errorMessage))
			{
				throw new InvalidOperationException(errorMessage);
			}

			return _writer.Commit<T>();
		}

		public void Compile<T>(ref T @delegate, IEnumerable<Instruction> instructions) where T : Delegate
		{
			var instructionList = new InstructionList(instructions)
			{
				new Instruction() { Code = Code.Retnq }
			};

			@delegate = Compile<T>(instructionList);
		}



		public static IEnumerable<Instruction> SpillRegister(Register reg, IEnumerable<Instruction> containedInstructions)
		{
			yield return new Instruction() { Code = Code.Push_r64, Op0Register = reg };

			foreach (var inst in containedInstructions)
			{
				yield return inst;
			}

			yield return new Instruction() { Code = Code.Pop_r64, Op0Register = reg };
		}

		public static IEnumerable<Instruction> IncrementStack(uint bytes, IEnumerable<Instruction> containedInstructions)
		{
			yield return new Instruction()
			{
				Code = Code.Sub_rm64_imm32,
				Op0Register = Register.RSP,
				Op1Kind = OpKind.Immediate32to64,
				Immediate32 = bytes
			};

			foreach (var inst in containedInstructions)
			{
				yield return inst;
			}

			yield return new Instruction()
			{
				Code = Code.Add_rm64_imm32,
				Op0Register = Register.RSP,
				Op1Kind = OpKind.Immediate32to64,
				Immediate32 = bytes
			};
		}

		public static IEnumerable<Instruction> SaveRcxToANonVolatileRegister(IEnumerable<Instruction> containedInstructions)
		{
			//mov     rsi,rcx
			yield return new Instruction()
			{
				Code = Code.Mov_r64_rm64,
				Op0Register = Register.RSI,
				Op1Register = Register.RCX
			};

			foreach (var inst in containedInstructions)
			{
				yield return inst;
			}

			//mov     rcx,rsi
			yield return new Instruction()
			{
				Code = Code.Mov_r64_rm64,
				Op0Register = Register.RCX,
				Op1Register = Register.RSI
			};
		}

		public static IEnumerable<Instruction> ZeroStackSSE2(uint stackBytes, byte stackOffset)
		{
			yield return new Instruction() {Code = Code.VEX_Vzeroupper};

			//lea     r10,[rsp+28h]
			yield return new Instruction()
			{
				Code = Code.Lea_r64_m,
				Op0Register = Register.R10,
				Op1Kind = OpKind.Memory,
				MemoryBase = Register.RSP,
				MemoryDisplSize = 0x1,
				MemoryDisplacement = stackOffset
			};

			yield return new Instruction()
			{
				Code = Code.VEX_Vxorps_xmm_xmm_xmmm128,
				Op0Register = Register.XMM0,
				Op1Register = Register.XMM0,
				Op2Register = Register.XMM0
			};

			if (!((stackBytes & 0xF) == 0)) { throw new NotImplementedException(); }

			for (uint i = 0; i < stackBytes; i+= 0x10)
			{
				yield return new Instruction()
				{
					Code = Code.VEX_Vmovdqu_xmmm128_xmm,
					Op0Kind = OpKind.Memory,
					Op1Register = Register.XMM0,
					MemoryBase = Register.R10,
					MemoryDisplSize = 0x1,
					MemoryDisplacement = i
				};
			}

		}


		public static IEnumerable<Instruction> ZeroStack(uint stackBytes, byte stackOffset, byte operandSize)
		{
			//lea     rdi,[rsp+28h]
			yield return new Instruction()
			{
				Code = Code.Lea_r64_m,
				Op0Register = Register.RDI,
				Op1Kind = OpKind.Memory,
				MemoryBase = Register.RSP,
				MemoryDisplSize = 0x1,
				MemoryDisplacement = stackOffset
			};

			//mov     ecx,20h
			yield return new Instruction()
			{
				Code = Code.Mov_r32_imm32,
				Op0Register = Register.ECX,
				Op1Kind = OpKind.Immediate32,
				Immediate32 = stackBytes / operandSize
			};

			//xor     eax,eax
			yield return new Instruction()
			{
				Code = Code.Xor_r32_rm32,
				Op0Register = Register.EAX,
				Op1Register = Register.EAX
			};

			//rep     stos dword ptr [rdi]
			yield return new Instruction()
			{
				Code = operandSize switch
				{
					1 => Code.Stosb_m8_AL,
					2 => Code.Stosw_m16_AX,
					4 => Code.Stosd_m32_EAX,
					8 => Code.Stosq_m64_RAX,
					_ => throw new InvalidOperationException()
				},
				HasRepePrefix = true,
				Op0Kind = OpKind.MemoryESRDI,
				Op0Register = Register.RDI,
				Op1Register = operandSize switch
				{
					1 => Register.AL,
					2 => Register.AX,
					4 => Register.EAX,
					8 => Register.RAX,
					_ => throw new InvalidOperationException()
				}
			};

		}


	}
}
