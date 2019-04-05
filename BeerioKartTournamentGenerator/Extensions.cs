using System;
using System.Collections.Generic;

namespace BeerioKartTournamentGenerator
{
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> ts)
        {
            var random = new Random();
            var count = ts.Count;
            var last = count - 1;
            for(var i = 0; i < last; ++i)
            {
                var r = random.Next(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }
    }
}
