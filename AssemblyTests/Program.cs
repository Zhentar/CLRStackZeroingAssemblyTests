using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using System;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using Iced.Intel;

namespace AssemblyTests
{
	static class Program
	{
		static void Main()
		{
			//JitDump code:
			//var type = new Assembler().CompileDelegateType<IAsmDelegateVoid>(Enumerable.Empty<Instruction>(), "Testy");
			//var benchmarkType = typeof(Asm<>).MakeGenericType(type);
			//IBenchmark bench = (IBenchmark)Activator.CreateInstance(benchmarkType);
			//bench.Test();

			BenchmarkSwitcher.FromTypes(TestSource.GetBenchmarkTypes(typeof(Asm<>)).ToArray()).RunAllJoined();
		}
	}


	public class MyConfig : ManualConfig
	{
		public MyConfig()
		{
			var run = Job.InProcess.WithMaxRelativeError(0.05)
						 .With(new[] { new EnvironmentVariable("COMPlus_TieredCompilation", "0") });
			Add(run);
			Add(ReturnValueValidator.FailOnError);
			Add(new TypeTagColumn("Case", t => t.GenericTypeArguments?[0]?.Name.Split('_')[0]));
			Add(new TypeTagColumn("Size", t => t.GenericTypeArguments?[0]?.Name.Split('_').ElementAtOrDefault(1)?.ToString()));
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


	public class TypeTagColumn : IColumn
	{
		private readonly Func<Type, string> getTag;

		public string Id => nameof(TypeTagColumn) + ColumnName;
		public string ColumnName { get; }

		public TypeTagColumn(string columnName, Func<Type, string> getTag)
		{
			this.getTag = getTag;
			ColumnName = columnName;
		}

		public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
		public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => getTag(benchmarkCase.Descriptor.Type) ?? "";

		public bool IsAvailable(Summary summary) => true;
		public bool AlwaysShow => true;
		public ColumnCategory Category => ColumnCategory.Custom;
		public int PriorityInCategory => 0;
		public bool IsNumeric => false;
		public UnitType UnitType => UnitType.Dimensionless;
		public string Legend => $"Custom '{ColumnName}' tag column";
		public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
		public override string ToString() => ColumnName;
	}
}
