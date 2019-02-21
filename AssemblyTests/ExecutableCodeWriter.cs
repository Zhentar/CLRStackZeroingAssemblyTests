using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Iced.Intel;
using Mono.Cecil;

namespace AssemblyTests
{
	//This is an interface to enable pseudo static delegates, via generated value types
	//allowing for de-virtualization & inlining. The interface passed to CommitToAssembly
	//must have exactly one method on it.
	public interface IAsmDelegate { }

	public interface IAsmDelegateVoid : IAsmDelegate
	{
		void Invoke();
	}


	public sealed class ExecutableCodeWriter : CodeWriter, IDisposable
	{
		//This effectively causes the delegates to keep the backing memory rooted, and allowing it to be freed when they have all been GCed
		private static readonly ConditionalWeakTable<Delegate, SafeExecutableBuffer> s_delegateBufferTracker = new ConditionalWeakTable<Delegate, SafeExecutableBuffer>();

		public ExecutableCodeWriter(string assemblyName)
		{
			AssemblyName = assemblyName;
			AllocateNewBuffer();
		}

		public string AssemblyName { get; }

		public ulong NextFunctionPointer => (ulong)_buffer.DangerousGetHandle().ToInt64() + _currentStartPosition;

		private uint _currentStartPosition;
		private UnmanagedMemoryStream _stream;
		private SafeExecutableBuffer _buffer;

		private void AllocateNewBuffer()
		{
			_buffer?.Freeze();
			_buffer = new SafeExecutableBuffer();
			_stream = new UnmanagedMemoryStream(_buffer, 0, (long)_buffer.ByteLength, FileAccess.ReadWrite);
			_currentStartPosition = 0;
		}

		public void Dispose() => _buffer?.Freeze();

		public override void WriteByte(byte value)
		{
			//TODO: this only actually works for position independent code
			if (_stream.Position == _stream.Length)
			{
				var copyLen = (int)(_stream.Length - _currentStartPosition);
				Span<byte> copyBuffer = stackalloc byte[copyLen];
				_stream.Position = _currentStartPosition;
				_stream.Read(copyBuffer);
				AllocateNewBuffer();
				_stream.Write(copyBuffer);
			}
			_stream.WriteByte(value);
		}


		public T CommitToPInvokeDelegate<T>() where T : Delegate
		{
			ulong funcAddr = NextFunctionPointer;
			_currentStartPosition = (uint)_stream.Position;
			//Using an IL delegate here because Marshal.GetDelegateForFunctionPointer has forced
			//ClearLastError/GetLastError calls (about which the CLR code says "It's wrong, but please keep it for backward compatibility")
			//This costs multiple nanoseconds, so instead generate stubs that bypass it
			var delegateMethod = typeof(T).GetMethod("Invoke");
			var returnType = delegateMethod.ReturnType;
			var parameters = delegateMethod.GetParameters().Select(p => p.ParameterType).ToArray();

			var dm = new DynamicMethod("Asm_Stub", returnType, parameters, typeof(ExecutableCodeWriter).Module);
			var ilGen = dm.GetILGenerator();
			ilGen.Emit(OpCodes.Ldc_I8, (long)funcAddr);
			ilGen.Emit(OpCodes.Conv_I);
			for (int i = 0; i < parameters.Length; i++)
			{
				ilGen.Emit(OpCodes.Ldarg, i);
			}
			ilGen.EmitCalli(OpCodes.Calli, CallingConvention.StdCall, returnType, parameters);
			ilGen.Emit(OpCodes.Ret);
			var result = (T)dm.CreateDelegate(typeof(T));

			s_delegateBufferTracker.Add(result, _buffer);
			//I think I'm supposed to call FlushInstructionCache here... but the JIT will for
			//the delegate stub anyway, right?
			return result;
		}



		private const string BUFFER_FIELD = "__codeBufferReference";
		public Type CommitToAssembly<T>(string methodName) where T : IAsmDelegate
		{
			if (!typeof(T).IsInterface) { throw new ArgumentException(nameof(T) + " must be an interface type"); }
			var methodToImplement = typeof(T).GetMethods().Single();

			var assembly = AssemblyDefinition.CreateAssembly(
			new AssemblyNameDefinition(AssemblyName, new Version(1, 0, 0, 0)), AssemblyName, ModuleKind.Console);

			var module = assembly.MainModule;

			// create the program type and add it to the module
			var type = new TypeDefinition(AssemblyName, methodName,
				Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, module.ImportReference(typeof(ValueType)));

			module.Types.Add(type);


			type.Fields.Add(new FieldDefinition(BUFFER_FIELD,
				Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Private,
				module.ImportReference(typeof(SafeExecutableBuffer))));


			var im = new InterfaceImplementation(module.ImportReference(typeof(T)));
			type.Interfaces.Add(im);

			// add an empty constructor
			var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig
				| Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, module.TypeSystem.Void);

			// create the constructor's method body
			var il = ctor.Body.GetILProcessor();

			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Nop));
			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));

			type.Methods.Add(ctor);

			var generatedMethod = new MethodDefinition(methodToImplement.Name,
				Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Virtual,
				module.ImportReference(methodToImplement.ReturnType));

			type.Methods.Add(generatedMethod);

			foreach (var parameterInfo in methodToImplement.GetParameters())
			{
				generatedMethod.Parameters.Add(new ParameterDefinition(module.ImportReference(parameterInfo.ParameterType)));
			}
			
			// create the method body
			il = generatedMethod.Body.GetILProcessor();

			ulong funcAddr = NextFunctionPointer;
			_currentStartPosition = (uint)_stream.Position;

			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldc_I8, (long)funcAddr));
			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Conv_I));
			// If we're honest about what we're calling it adds a bunch of overhead, *nanoseconds* of it (FIVE!!! in my test cases)
			// If we claim it's a managed function, it can JIT to a simple mov+jmp
			var cs = new Mono.Cecil.CallSite(module.ImportReference(methodToImplement.ReturnType));
			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Calli, cs));
			il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));

			generatedMethod.Overrides.Add(module.ImportReference(methodToImplement));

			var memstream = new MemoryStream();

			assembly.Write(memstream);
			var realAssembly = Assembly.Load(memstream.ToArray());

			var realType = realAssembly.GetType(AssemblyName + "." + methodName);

			realType.GetField(BUFFER_FIELD, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _buffer);
			return realType;
		}
	}

}
