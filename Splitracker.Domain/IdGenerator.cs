using System;

namespace Splitracker.Domain;

public static class IdGenerator
{
    [ThreadStatic]
    static Random? _random;

    public static string RandomId()
    {
        _random ??= new();
        var buf = new byte[18];
        buf[0] = (byte)_random.Next(byte.MaxValue);
        buf[^1] = (byte)_random.Next(byte.MaxValue);
        Guid.NewGuid().TryWriteBytes(buf.AsSpan(1));
        return Convert.ToBase64String(buf);
    }
}