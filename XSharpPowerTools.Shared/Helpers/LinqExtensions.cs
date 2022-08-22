using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XSharpPowerTools.Helpers
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> GetRange<T>(this IEnumerable<T> source, int startIndex, int length)
        {
            using var enumerator = source.Skip(startIndex).GetEnumerator();
            
            var endIndex = startIndex + length;
            for (var i = startIndex; enumerator.MoveNext(); i++)
            {
                if (i >= endIndex)
                    yield break;
                else
                    yield return enumerator.Current;
            }

            yield break;
        }
    }
}
