using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using System;

namespace AssemblyTests
{
	class Program
	{
		static void Main()
		{
			BenchmarkRunner.Run<AssemblyBenchmarks>();
		}
	}


	public class MyConfig : ManualConfig
	{
		public MyConfig()
		{
			var run = Job.Default.WithMaxRelativeError(0.05)
						 .With(new[] { new EnvironmentVariable("COMPlus_TieredCompilation", "0") });
			Add(run);
			Add(ReturnValueValidator.FailOnError);
		}
	}

	public class Profiled : ManualConfig
	{
		public Profiled()
		{
			var run = Job.Default.WithMaxRelativeError(0.1)
						 .With(new[] { new EnvironmentVariable("COMPlus_TieredCompilation", "0") });
			Add(run);
			Add(ReturnValueValidator.FailOnError);
			Add(new EtwProfiler(new EtwProfilerConfig(false, cpuSampleIntervalInMiliseconds: 0.125f)));
		}
	}

	public class DiagnoserConfig : ManualConfig
	{
		public DiagnoserConfig()
		{
			var run = Job.Dry.With(new[] { new EnvironmentVariable("COMPlus_TieredCompilation", "0") });
			Add(run);
			Add(DisassemblyDiagnoser.Create(new DisassemblyDiagnoserConfig(true, true, true, true, 4)));
			Add(ReturnValueValidator.FailOnError);
			Add(new InliningDiagnoser());
		}
	}
}
