using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Stashbox;

public class Program
{
    public static void Main()
    {
        var (type1, type2) = GetCollidingTypes();

        // they're different
        Debug.Assert(type1!=type2);
        // but their hashes are the same
        Debug.Assert(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(type1)==System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(type2));

        var services = new StashboxContainer();
        services.Register(type1);
        services.Register(type2);

        services.Resolve(type2); // this succeeds as the last-write wins
        services.Resolve(type1); // this fails, "unable to resolve type"
    }

    private static (Type, Type) GetCollidingTypes()
    {
        var hashes = new Dictionary<int, Type>();
        uint n = 0;
        while (true)
        {
            var type = GenerateType(n++);
            var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(type);
            if (hashes.ContainsKey(hash))
            {
                return (hashes[hash], type);
            }

            hashes.Add(hash, type);
        }

        throw new InvalidOperationException("unreachable");
    }

    private static Type GenerateType(uint n)
    {
        var current = typeof(Nil);
        for (var i=0; i < sizeof(uint) * 8; i++)
        {
            var leading = n >> i;
            if (leading == 0) {
                return current;
            }

            var bit = (n >> i) & 0x01;
            if (bit == 0) {
                current = typeof(Zero<>).MakeGenericType(current);
            } else {
                current = typeof(One<>).MakeGenericType(current);
            }
        }
        return current;
    }
}

internal class Nil {}
internal class Zero<T> {}
internal class One<T> {}
