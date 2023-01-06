using System;
using System.Text.RegularExpressions;

namespace Splitracker.Domain;

public readonly record struct PointsVec(int Channeled, int Exhausted, int Consumed)
{
    public const string IncrementalExpressionPattern = @"([KEVkev0-9+-]|\s)*";
    
    public int this[PointType type] => type switch
    {
        PointType.K => Channeled,
        PointType.E => Exhausted,
        PointType.V => Consumed,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static PointsVec operator +(PointsVec a, PointsVec b) => new(
        a.Channeled + b.Channeled,
        a.Exhausted + b.Exhausted,
        a.Consumed + b.Consumed
        );

    public override string ToString() =>
        "" +
        Channeled switch {
            > 0 => $"K{Channeled}",
            _ => ""
        } +
        Exhausted switch {
            > 0 => $"E{Exhausted}",
            _ => ""
        } +
        Consumed switch {
            > 0 => $"V{Consumed}",
            _ => ""
        } + (Channeled < 0 || Exhausted < 0 || Consumed < 0 ? "-" + Channeled switch {
                < 0 => $"K{Math.Abs(Channeled)}",
                _ => ""
            } +
            Exhausted switch {
                < 0 => $"E{Math.Abs(Exhausted)}",
                _ => ""
            } +
            Consumed switch {
                < 0 => $"V{Math.Abs(Consumed)}",
                _ => ""
            } : "");

    static readonly Regex TokenPattern = new(@"[kev+-]|(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    public static PointsVec From(int points, PointType ty) =>
        new() {
            Channeled = ty == PointType.K ? points : 0,
            Exhausted = ty == PointType.E ? points : 0,
            Consumed = ty == PointType.V ? points : 0
        };

    public static PointsVec From(ReadOnlySpan<char> input, PointType defaultType)
    {
        var matches = TokenPattern.EnumerateMatches(input);
        var vec = new PointsVec();
        var factor = 1;
        var currentType = defaultType;
        while (matches.MoveNext())
        {
            var match = matches.Current;
            switch (input[match.Index])
            {
                case '+':
                    factor = 1;
                    break;
                case '-':
                    factor = -1;
                    break;
                case 'k':
                case 'K':
                    currentType = PointType.K;
                    break;
                case 'e':
                case 'E':
                    currentType = PointType.E;
                    break;
                case 'v':
                case 'V':
                    currentType = PointType.V;
                    break;
                default:
                    if (int.TryParse(input.Slice(match.Index, match.Length), out var numericValue))
                    {
                        vec = currentType switch
                        {
                            PointType.K => vec with { Channeled = vec.Channeled + numericValue * factor },
                            PointType.E => vec with { Exhausted = vec.Exhausted + numericValue * factor },
                            PointType.V => vec with { Consumed = vec.Consumed + numericValue * factor },
                            _ => vec
                        };
                    }
                    break;
            }
        }
        
        return vec;
    }
    
    public PointsVec Collapse()
    {
        var p = this;
        p = p.Channeled switch
        {
            > 0 => p with { Channeled = Math.Max(0, Math.Min(p.Channeled - Math.Max(p.Exhausted, p.Consumed), p.Channeled)) },
            < 0 => p with { Channeled = Math.Min(0, Math.Max(p.Channeled - Math.Min(p.Exhausted, p.Consumed), p.Channeled)) },
            _ => p
        };
        p = p.Exhausted switch {
            > 0 => p with { Exhausted = Math.Max(0, Math.Min(p.Exhausted - p.Consumed, p.Exhausted)) },
            < 0 => p with { Exhausted = Math.Min(0, Math.Max(p.Exhausted - p.Consumed, p.Exhausted)) },
            _ => p
        };
        return p;
    }
    
    public PointsVec Normalized => new(Math.Abs(Channeled), Math.Abs(Exhausted), Math.Abs(Consumed));
    public bool IsZero => Channeled == 0 && Exhausted == 0 && Consumed == 0; 
}