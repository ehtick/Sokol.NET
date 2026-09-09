using System;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace SokolObfuscator
{
    // Replaces `ldstr "plain"` in in-scope method bodies with `ldstr "<base64>"; call D(string)`,
    // where D is an injected decryptor (base64-decode → XOR key → UTF-8). The plaintext therefore
    // never appears in the shipped binary (defeats `strings`/grep on the native .so/.dylib/.exe and
    // the WebCIL .wasm). This is obfuscation, not encryption-at-rest — the key + decryptor ship in
    // the binary. See docs/OBFUSCATION_TOOL_DESIGN.md §6.2.
    public sealed class StringEncryptor
    {
        readonly byte _key;

        public StringEncryptor(int? seed)
        {
            // Deterministic when a seed is given; always non-zero so XOR actually changes bytes.
            int s = seed ?? Environment.TickCount;
            _key = (byte)(((s ^ (s >> 8)) & 0xFF) | 1);
        }

        public int Encrypt(ModuleDefMD module, ObfuscationConfig config, Func<MethodDef, MethodContext> ctxOf, bool verbose)
        {
            MethodDef decryptor = CreateDecryptor(module);
            int count = 0;

            foreach (var type in module.GetTypes())
            {
                foreach (var m in type.Methods)
                {
                    if (m == decryptor || !m.HasBody) continue;
                    if (!config.InScope(ctxOf(m))) continue;

                    var instrs = m.Body.Instructions;
                    for (int i = 0; i < instrs.Count; i++)
                    {
                        if (instrs[i].OpCode.Code != Code.Ldstr) continue;
                        if (instrs[i].Operand is not string value) continue;
                        if (!config.ShouldEncryptString(value)) continue;

                        instrs[i].Operand = Encode(value);                       // ldstr now holds the ciphertext
                        instrs.Insert(i + 1, Instruction.Create(OpCodes.Call, decryptor));
                        i++;                                                     // skip the inserted call
                        count++;
                        if (verbose) Console.WriteLine($"  encrypt  \"{Trunc(value)}\"   ({m.DeclaringType.FullName})");
                    }
                }
            }
            return count;
        }

        string Encode(string value)
        {
            byte[] b = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < b.Length; i++) b[i] ^= _key;
            return Convert.ToBase64String(b);
        }

        static string Trunc(string s) => s.Length <= 24 ? s : s.Substring(0, 24) + "…";

        // Emits:  static string D(string s) {
        //             byte[] b = Convert.FromBase64String(s);
        //             for (int i = 0; i < b.Length; i++) b[i] = (byte)(b[i] ^ KEY);
        //             return Encoding.UTF8.GetString(b);
        //         }
        MethodDef CreateDecryptor(ModuleDefMD module)
        {
            var importer = new Importer(module);
            IMethod fromB64 = importer.Import(typeof(Convert).GetMethod(nameof(Convert.FromBase64String), new[] { typeof(string) })!);
            IMethod utf8Get = importer.Import(typeof(Encoding).GetProperty(nameof(Encoding.UTF8))!.GetGetMethod()!);
            IMethod getString = importer.Import(typeof(Encoding).GetMethod(nameof(Encoding.GetString), new[] { typeof(byte[]) })!);

            var type = new TypeDefUser("SokolObfuscator", "__Strings__", module.CorLibTypes.Object.TypeDefOrRef)
            {
                Attributes = dnlib.DotNet.TypeAttributes.NotPublic | dnlib.DotNet.TypeAttributes.Sealed
                           | dnlib.DotNet.TypeAttributes.Abstract | dnlib.DotNet.TypeAttributes.Class
                           | dnlib.DotNet.TypeAttributes.BeforeFieldInit
            };
            module.Types.Add(type);

            var sig = MethodSig.CreateStatic(module.CorLibTypes.String, module.CorLibTypes.String);
            var d = new MethodDefUser("D", sig,
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                dnlib.DotNet.MethodAttributes.Public | dnlib.DotNet.MethodAttributes.Static | dnlib.DotNet.MethodAttributes.HideBySig);
            type.Methods.Add(d);

            var body = new CilBody { InitLocals = true, MaxStack = 8 };
            d.Body = body;
            var bytes = new Local(new SZArraySig(module.CorLibTypes.Byte)); // [0] byte[] b
            var idx = new Local(module.CorLibTypes.Int32);                  // [1] int i
            body.Variables.Add(bytes);
            body.Variables.Add(idx);

            var loopBody = Instruction.Create(OpCodes.Ldloc, bytes); // first instr of the loop body
            var check = Instruction.Create(OpCodes.Ldloc, idx);      // first instr of the condition

            var il = body.Instructions;
            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            il.Add(Instruction.Create(OpCodes.Call, fromB64));
            il.Add(Instruction.Create(OpCodes.Stloc, bytes));
            il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Add(Instruction.Create(OpCodes.Stloc, idx));
            il.Add(Instruction.Create(OpCodes.Br, check));
            il.Add(loopBody);                                        // ldloc bytes  (array for stelem)
            il.Add(Instruction.Create(OpCodes.Ldloc, idx));          // index for stelem
            il.Add(Instruction.Create(OpCodes.Ldloc, bytes));
            il.Add(Instruction.Create(OpCodes.Ldloc, idx));
            il.Add(Instruction.Create(OpCodes.Ldelem_U1));           // b[i]
            il.Add(Instruction.Create(OpCodes.Ldc_I4, (int)_key));
            il.Add(Instruction.Create(OpCodes.Xor));
            il.Add(Instruction.Create(OpCodes.Conv_U1));
            il.Add(Instruction.Create(OpCodes.Stelem_I1));           // b[i] = (byte)(b[i]^KEY)
            il.Add(Instruction.Create(OpCodes.Ldloc, idx));
            il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
            il.Add(Instruction.Create(OpCodes.Add));
            il.Add(Instruction.Create(OpCodes.Stloc, idx));
            il.Add(check);                                           // ldloc idx
            il.Add(Instruction.Create(OpCodes.Ldloc, bytes));
            il.Add(Instruction.Create(OpCodes.Ldlen));
            il.Add(Instruction.Create(OpCodes.Conv_I4));
            il.Add(Instruction.Create(OpCodes.Blt, loopBody));
            il.Add(Instruction.Create(OpCodes.Call, utf8Get));
            il.Add(Instruction.Create(OpCodes.Ldloc, bytes));
            il.Add(Instruction.Create(OpCodes.Callvirt, getString));
            il.Add(Instruction.Create(OpCodes.Ret));

            return d;
        }
    }
}
