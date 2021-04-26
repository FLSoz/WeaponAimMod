using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeaponAimMod.src
{
    // Taken from https://www.growingwiththeweb.com/2013/01/a-list-extension-that-takes-lambda.html

    public static class ListExtensions
    {
        public static int BinarySearch<T>(this List<T> list,
                                          T item,
                                          Func<T, T, int> compare)
        {
            return list.BinarySearch(item, new ComparisonComparer<T>(compare));
        }
    }

    public class ComparisonComparer<T> : IComparer<T>
    {
        private readonly Comparison<T> comparison;

        public ComparisonComparer(Func<T, T, int> compare)
        {
            if (compare == null)
            {
                throw new ArgumentNullException("comparison");
            }
            comparison = new Comparison<T>(compare);
        }

        public int Compare(T x, T y)
        {
            return comparison(x, y);
        }
    }
}
