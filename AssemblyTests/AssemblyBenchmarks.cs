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
		private static Action s_overheadDelegate;
		private static Action s_baselineDelegate;
		private static Action s_improvedStosD;
		private static Action s_improvedStosQ;
		private static Action s_improvedStosB;
		private static Action s_sse2;

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


	//This is just to give me an easy way to call it for jitdumps
	public interface IBenchmark
	{
		void Test();
	}

	[Config(typeof(MyConfig))]
	public class Asm<T> : IBenchmark where T : struct, IAsmDelegateVoid
	{
		[Benchmark] public void Test()
		{	//Note - explicitly declaring a variable here helps the jit figure out it's not actually live
			T dummy = default;
			dummy.Invoke();
		}
	}

	[Config(typeof(MyConfig))]
	public class UnrolledAsm<T> where T : struct, IAsmDelegateVoid
	{
		[Benchmark]
		public void TestTen()
		{   //Note - explicitly declaring a variable here helps the jit figure out it's not actually live
			T dummy = default;
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
			dummy.Invoke();
		}
	}

	public static class TestSource
	{
		private static readonly Assembler s_assembler = new Assembler();
		public static Type OverheadEstimateType() => s_assembler.CompileDelegateType<IAsmDelegateVoid>(Enumerable.Empty<Instruction>(), "Overhead");


		public static IEnumerable<Type> GetBenchmarkTypes(Type openGeneric) => GetTheTypes().Select(typeParam => openGeneric.MakeGenericType(typeParam));

		private static IEnumerable<Type> GetTheTypes()
		{
			yield return OverheadEstimateType();

			foreach (var bytes in new[] { 32u, 64u/*, 128u, 256u, 512u, 1024u */})
			{
				var assembler = s_assembler;
				yield return assembler.CompileDelegateType<IAsmDelegateVoid>(
						Assembler.SpillRegister(Register.RDI,
						Assembler.SpillRegister(Register.RSI,
						Assembler.IncrementStack(bytes + 0x28,
						Assembler.SaveRcxToANonVolatileRegister(
						Assembler.ZeroStack(bytes, 0x28, 0x4
						))))), "Baseline_" + bytes);

				//yield return assembler.CompileDelegateType<IAsmDelegateVoid>(
				//	Assembler.SpillRegister(Register.RDI,
				//	Assembler.IncrementStack(bytes + 0x28,
				//	Assembler.ZeroStack(bytes, 0x28, 0x1
				//	))), "ImprovedStosB_" + bytes);

				//yield return assembler.CompileDelegateType<IAsmDelegateVoid>(
				//	Assembler.SpillRegister(Register.RDI,
				//	Assembler.IncrementStack(bytes + 0x28,
				//	Assembler.ZeroStack(bytes, 0x28, 0x4
				//	))), "ImprovedStosD_" + bytes);

				//yield return assembler.CompileDelegateType<IAsmDelegateVoid>(
				//	Assembler.SpillRegister(Register.RDI,
				//	Assembler.IncrementStack(bytes + 0x28,
				//	Assembler.ZeroStack(bytes, 0x28, 0x8
				//	))), "ImprovedStosQ_" + bytes);

				yield return assembler.CompileDelegateType<IAsmDelegateVoid>(
					Assembler.IncrementStack(bytes + 0x28,
					Assembler.ZeroStackSSE2(bytes, 0x28
					)), "SSE2_" + bytes);
			}
		}
	}
}
