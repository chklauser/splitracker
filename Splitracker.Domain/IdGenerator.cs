using System;
using System.Text.RegularExpressions;

namespace Splitracker.Domain;

public static partial class IdGenerator
{
    [ThreadStatic]
    static Random? _random;
    
    [GeneratedRegex("[/+]", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex urlUnsafeBase64Chars(); 
    
    public static string RandomId()
    {
        _random ??= new();
        var buf = new byte[18];
        buf[0] = (byte)_random.Next(byte.MaxValue);
        buf[^1] = (byte)_random.Next(byte.MaxValue);
        if (!Guid.NewGuid().TryWriteBytes(buf.AsSpan(1)))
        {
            throw new InvalidOperationException("Failed to write Guid to buffer");
        }

        var rendered = Convert.ToBase64String(buf);
        return urlUnsafeBase64Chars().Replace(rendered, c => c.ValueSpan[0] switch {
            '/' => "-",
            '+' => "_",
            _ => throw new InvalidOperationException(),
        });
    }
}