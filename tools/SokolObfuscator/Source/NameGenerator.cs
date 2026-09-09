using System;
using System.Collections.Generic;

namespace SokolObfuscator
{
    // Generates random valid identifiers: letter first, then letters+digits.
    // Uniqueness is tracked per declaring type (IL allows overload name reuse, but unique
    // names keep the symbol map readable). See design doc §6.1.
    public sealed class NameGenerator
    {
        const string First = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string Rest = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        readonly Random _rng;
        readonly int _length;
        readonly HashSet<string> _used = new();

        public NameGenerator(int? seed, int length = 10)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _length = Math.Max(4, length);
        }

        public string Next()
        {
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                var chars = new char[_length];
                chars[0] = First[_rng.Next(First.Length)];
                for (int i = 1; i < _length; i++)
                    chars[i] = Rest[_rng.Next(Rest.Length)];
                var name = new string(chars);
                if (_used.Add(name))
                    return name;
            }
            throw new InvalidOperationException("Exhausted identifier space (unexpected).");
        }
    }
}
