using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Iced.Intel;

namespace AssemblyTests
{
	public partial class Assembler
	{
		//TODO: make disposable, freeze buffer on dispose
		sealed class ExecutableCodeWriter : CodeWriter
		{
			//This effectively causes the delegates to keep the backing memory rooted, and allowing it to be freed when they have all been GCed
			private static readonly ConditionalWeakTable<Delegate, SafeExecutableBuffer> s_delegateBufferTracker = new ConditionalWeakTable<Delegate, SafeExecutableBuffer>();

			public ExecutableCodeWriter() => AllocateNewBuffer();
			
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

			public T Commit<T>() where T : Delegate
			{
				ulong funcAddr = NextFunctionPointer;
				_currentStartPosition = (uint)_stream.Position;
				T result;
				//TODO: better way to determine void func() delegate
				if (typeof(T) == typeof(BasicActionDelegate))
				{
					//Using an IL delegate here because Marshal.GetDelegateForFunctionPointer has forced
					//ClearLastError/GetLastError calls (about which the CLR code says "It's wrong, but please keep it for backward compatibility")
					//This costs multiple nanoseconds, so instead generate stubs that bypass it
					var dm = new DynamicMethod("Asm_Stub", typeof(void), null, typeof(ExecutableCodeWriter).Module);
					var ilGen = dm.GetILGenerator();
					ilGen.Emit(OpCodes.Ldc_I8, (long)funcAddr);
					ilGen.Emit(OpCodes.Conv_I);
					// If we're honest about what we're calling it adds a bunch of overhead, nanoseconds of it
					// If we claim it's a managed function, it can JIT to a simple mov+jmp
					//ilGen.EmitCalli(OpCodes.Calli, CallingConvention.StdCall, typeof(void), null);
					ilGen.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(void), null, null);
					ilGen.Emit(OpCodes.Ret);
					result = (T) dm.CreateDelegate(typeof(T));
				}
				else
				{
					result = Marshal.GetDelegateForFunctionPointer<T>((IntPtr) funcAddr);
				}

				s_delegateBufferTracker.Add(result, _buffer);
				//I think I'm supposed to call FlushInstructionCache here... but the JIT will for
				//the delegate stub anyway, right?
				return result;
			}
		}

	}
}
