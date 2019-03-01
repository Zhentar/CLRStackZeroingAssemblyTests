# CLRStackZeroingAssemblyTests
Test the performance of different stack zeroing options... using assembly compiled from the comfort of C#!

## How it works

Writing the assembly code is done in Assembler.cs, using https://github.com/0xd4d/iced as the assembler/encoder. This part isn't very exciting, because iced does all the hard work for us.

SafeExecutableBuffer provides a simple wrapper for VirtualAlloc that is set to be ExecutableReadWrite (flipped to ExecutableRead when you're done filling the buffer) since you need to put the code somewhere you're allowed to run it.

Turning the encoded assembly into something that can actually be executed is the fun stuff, in ExecutableCodeWriter.cs. Naively, `Marshal.GetDelegateForFunctionPointer` does exactly what is needed - but it turns out such delegates always include SetLastError handling with an overhead cost on the order of 10ns on my system (for a total of about 20ns per call) - clearly unacceptable in any scenario where inline assembly is a thing you would want. So instead, it uses `DynamicMethod`/Reflection.Emit to build a delegate that uses `calli` with an unmanaged calling convention; this is basically equivalent to what you would get with C++/CLI and as such is pretty fast (8-10ns for my tests) but still includes non-trivial overhead; it would not be effective to do such a thing for access to an unsupported intrinsic, for example.

To go even faster, you *could* just use `calli` with a managed calling convention. But you'd find that the `DynamicMethod` code can't be inlined, leaving you with an extra layer of indirection (though in at least some cases it JITed to nothing more than a `jmp` for me). So instead, the code uses Mono.Cecil to generate an assembly in memory & load it so that the delegate wrapper can be inlined away; you have nothing more than a `call` function for sub-nanosecond overhead. Actually using this for anything important would be totally nuts, though.
