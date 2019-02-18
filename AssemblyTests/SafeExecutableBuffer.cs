using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Iced.Intel;
using SIZE_T = System.IntPtr;

namespace AssemblyTests
{
	public class SafeExecutableBuffer : SafeBuffer
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr VirtualAlloc(IntPtr lpAddress, SIZE_T dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool VirtualFree(IntPtr lpAddress, SIZE_T dwSize, FreeType dwFreeType);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool VirtualProtect(IntPtr lpAddress, SIZE_T dwSize, MemoryProtection flNewProtect, out MemoryProtection lpflOldProtect);

		[Flags]
		public enum AllocationType
		{
			Commit = 0x1000,
			Reserve = 0x2000,
			ReserveAndCommit = Commit | Reserve
		}

		[Flags]
		public enum MemoryProtection
		{
			Execute = 0x10,
			ExecuteRead = 0x20,
			ExecuteReadWrite = 0x40,
		}

		[Flags]
		public enum FreeType
		{
			Decommit = 0x4000,
			Release = 0x8000,
		}

		private const int VirtualAllocGranularity = 0x1_0000;
		public SafeExecutableBuffer() : base(true)
		{
			this.handle = VirtualAlloc(IntPtr.Zero, (SIZE_T)VirtualAllocGranularity, AllocationType.ReserveAndCommit, MemoryProtection.ExecuteReadWrite);
			this.Initialize(VirtualAllocGranularity);
		}

		protected override bool ReleaseHandle() => VirtualFree(this.handle, SIZE_T.Zero, FreeType.Release);

		public void Freeze() => VirtualProtect(this.handle, SIZE_T.Zero, MemoryProtection.Execute, out _);
	}
}
