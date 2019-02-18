using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace AssemblyTests
{
	[Config(typeof(DiagnoserConfig))]
	public class BaselineBehaviors
	{

#pragma warning disable IDE0059 // Value assigned to symbol is never us
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CS0169  // field never used
#pragma warning disable IDE0044 // Add readonly modifier

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void OpaqueDummy<T>(out T val)
		{
			val = default;
		}

		public static bool DummyTest = false;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TestHelper<T>()
		{
			if (DummyTest)
			{
				OpaqueDummy(out T value);
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TestHelperWithMidFunctionZeroing<T>()
		{
			OpaqueDummy(out T value);
			value = default;
			OpaqueDummy(out value);
		}

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test16() => TestHelper<Struct16>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test24() => TestHelper<Struct24>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test32() => TestHelper<Struct32>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test48() => TestHelper<Struct48>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test64() => TestHelper<Struct64>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test96() => TestHelper<Struct96>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test128() => TestHelper<Struct128>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test256() => TestHelper<Struct256>();

		public struct Struct16
		{
			ulong fieldA;
			string fieldB;
		}

		public struct Struct24
		{
			Struct16 a;
			ulong b;
		}

		public struct Struct32
		{
			Struct16 a;
			Struct16 b;
		}
		public struct Struct48
		{
			Struct32 a;
			Struct16 b;
		}
		public struct Struct64
		{
			Struct32 a;
			Struct32 b;
		}
		public struct Struct96
		{
			Struct48 a;
			Struct48 b;
		}
		public struct Struct128
		{
			Struct64 a;
			Struct64 b;
		}
		public struct Struct256
		{
			Struct128 a;
			Struct128 b;
		}

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr16() => TestHelper<StructNoPtr16>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr24() => TestHelper<StructNoPtr24>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr32() => TestHelper<StructNoPtr32>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr48() => TestHelper<StructNoPtr48>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr64() => TestHelper<StructNoPtr64>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr96() => TestHelper<StructNoPtr96>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr128() => TestHelper<StructNoPtr128>();

		[Benchmark]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestNoPtr256() => TestHelper<StructNoPtr256>();

		public struct StructNoPtr16
		{
			ulong fieldA;
			ulong fieldB;
		}

		public struct StructNoPtr24
		{
			StructNoPtr16 a;
			ulong b;
		}

		public struct StructNoPtr32
		{
			StructNoPtr16 a;
			StructNoPtr16 b;
		}
		public struct StructNoPtr48
		{
			StructNoPtr32 a;
			StructNoPtr16 b;
		}
		public struct StructNoPtr64
		{
			StructNoPtr32 a;
			StructNoPtr32 b;
		}
		public struct StructNoPtr96
		{
			StructNoPtr48 a;
			StructNoPtr48 b;
		}
		public struct StructNoPtr128
		{
			StructNoPtr64 a;
			StructNoPtr64 b;
		}
		public struct StructNoPtr256
		{
			StructNoPtr128 a;
			StructNoPtr128 b;
		}


#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0169  // field never used
#pragma warning restore IDE0059 // Value assigned to symbol is never used
#pragma warning restore IDE0051 // Remove unused private members
	}
}
