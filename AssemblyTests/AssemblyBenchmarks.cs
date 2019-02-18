using BenchmarkDotNet.Attributes;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyTests
{
	[Config(typeof(MyConfig))]
	public class AssemblyBenchmarks
	{
		private static Assembler.BasicActionDelegate s_overheadDelegate;
		private static Assembler.BasicActionDelegate s_baselineDelegate;
		private static Assembler.BasicActionDelegate s_improvedStosD;
		private static Assembler.BasicActionDelegate s_improvedStosQ;
		private static Assembler.BasicActionDelegate s_improvedStosB;
		private static Assembler.BasicActionDelegate s_sse2;

		private static readonly Assembler s_assembler = new Assembler();
		
		[Params(32u, 64u, 128u, 256u, 512u, 1024u)]
		public static uint BytesToTest { get; set; }

		[GlobalSetup]
		public static void Setup()
		{
			s_assembler.Compile(ref s_overheadDelegate, Enumerable.Empty<Instruction>());

			s_assembler.Compile(ref s_baselineDelegate,
				Assembler.SpillRegister(Register.RDI,
				Assembler.SpillRegister(Register.RSI,
				Assembler.IncrementStack(BytesToTest + 0x28,
				Assembler.SaveRcxToANonVolatileRegister(
				Assembler.ZeroStack(BytesToTest,0x28, 0x4
				))))));

			s_assembler.Compile(ref s_improvedStosB,
				Assembler.SpillRegister(Register.RDI,
				Assembler.IncrementStack(BytesToTest + 0x28,
				Assembler.ZeroStack(BytesToTest, 0x28, 0x4
				))));


			s_assembler.Compile(ref s_improvedStosD,
				Assembler.SpillRegister(Register.RDI,
				Assembler.IncrementStack(BytesToTest + 0x28,
				Assembler.ZeroStack(BytesToTest, 0x28, 0x4
				))));

			s_assembler.Compile(ref s_improvedStosQ,
				Assembler.SpillRegister(Register.RDI,
				Assembler.IncrementStack(BytesToTest + 0x28,
				Assembler.ZeroStack(BytesToTest, 0x28, 0x4
				))));

			s_assembler.Compile(ref s_sse2,
				Assembler.IncrementStack(BytesToTest + 0x28,
				Assembler.ZeroStackSSE2(BytesToTest, 0x28
				)));
		}

		[Benchmark]
		public void Overhead() => s_overheadDelegate();

		[Benchmark]
		public void Baseline() => s_baselineDelegate();

		[Benchmark]
		public void ImprovedStosB() => s_improvedStosB();
		[Benchmark]
		public void ImprovedStosD() => s_improvedStosD();
		[Benchmark]
		public void ImprovedStosQ() => s_improvedStosQ();

		[Benchmark]
		public void SSE2() => s_sse2();


	}
}
