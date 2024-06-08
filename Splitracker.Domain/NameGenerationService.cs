using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Splitracker.Domain;

public partial class NameGenerationService
{
     public const char Placeholder = '\uFFFC';
     
     [GeneratedRegex(@"\b((?<num>\d+)|(?<alpha>[a-z]+)|(?<ALPHA>[A-Z]+))\s*$", RegexOptions.ExplicitCapture)]
     private static partial Regex suffixPattern();

     public string InferTemplateName(string combinedName)
     {
          if(suffixPattern().Match(combinedName) is {Success: true, Index: {} index} match)
          {
               return combinedName[..index].Trim();
          }
          else
          {
               return combinedName.Trim();
          }
     }

     enum Scheme
     {
          Number,
          LowerAlpha,
          UpperAlpha,
     }
     
     public INamingScheme InferNamingScheme(IEnumerable<string> names)
     {
          var dominantNames = names.Select(parsed)
               .Where(x => x.Scheme != null)
               .GroupBy(x => x.Scheme!).MaxBy(g => g.Count());
          if (dominantNames == null)
          {
               return new Numeric([]);
          }

          var existingNumbers = dominantNames.Select(x => x.Parsed);
          return dominantNames.Key switch {
               Scheme.Number => new Numeric(existingNumbers),
               Scheme.LowerAlpha => new Alpha('a', existingNumbers),
               Scheme.UpperAlpha => new Alpha('A', existingNumbers),
               _ => throw new InvalidOperationException($"Scheme {dominantNames.Key} not implemented"),
          };

          static (string Name, Scheme? Scheme, int Parsed) parsed(string name)
          {
               var match = suffixPattern().Match(name);
               if (match.Groups["num"] is { Success: true } num && int.TryParse(num.ValueSpan,
                    provider: CultureInfo.InvariantCulture,
                    out var parsed))
               {
                    return (name, Scheme.Number, parsed);
               }
               else if (match.Groups["alpha"] is { Success: true } alpha && tryParseAlpha('a', alpha.ValueSpan, out parsed))
               {
                    return (name, Scheme.LowerAlpha, parsed);
               }
               else if (match.Groups["ALPHA"] is { Success: true } upperAlpha && tryParseAlpha('A', upperAlpha.ValueSpan, out parsed))
               {
                    return (name, Scheme.UpperAlpha, parsed);
               }
               else
               {
                    return (name, null, 0);
               }
          }
     }

     static bool tryParseAlpha(char baseChar, ReadOnlySpan<char> input, out int parsed)
     {
          parsed = 0;
          var factor = 1;
          for (var i = input.Length - 1; i >= 0; i--)
          {
               var digit = input[i] - baseChar;
               if (digit is < 0 or > 25)
               {
                    return false;
               }

               parsed += digit * factor;
               factor *= 26;
          }

          return true;
     }

     abstract class NamingScheme(IEnumerable<int> existingNumbers) : INamingScheme
     {
          readonly HashSet<int> existing = [..existingNumbers];
          int cursor = 1;
          
          protected abstract string Render(int number);

          public string GenerateNext()
          {
               while (existing.Contains(cursor))
               {
                    cursor += 1;
               }

               var result = Render(cursor);
               cursor += 1;
               return result;
          }
     }

     class Numeric(IEnumerable<int> existingNumbers) : NamingScheme(existingNumbers)
     {
          protected override string Render(int number)
          {
               return $"{Placeholder} {number}";
          }
     }

     class Alpha(char baseCharacter, IEnumerable<int> existingNumbers) : NamingScheme(existingNumbers)
     {
          protected override string Render(int number)
          {
               var result = new StringBuilder();
               
               do
               {
                    var digit = number % 26;
                    number /= 26;
                    result.Insert(0, (char)(baseCharacter + digit));
               } while (number > 0);

               result.Append(' ');
               result.Append(Placeholder);
               
               // reverse the string
               for (var i = 0; i < result.Length / 2; i++)
               {
                    (result[i], result[result.Length - i - 1]) = (result[result.Length - i - 1], result[i]);
               }
               
               return result.ToString();
          }
     }
}

public interface INamingScheme
{
     string GenerateNext();
}