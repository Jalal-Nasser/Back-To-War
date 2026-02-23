using System;
using System.Collections.Generic;

namespace Back2War.SimCore.Core
{
    public static class Deterministic
    {
        public static void SortUnique(List<uint> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            if (ids.Count <= 1)
            {
                return;
            }

            ids.Sort();

            int writeIndex = 1;
            uint previous = ids[0];

            for (int readIndex = 1; readIndex < ids.Count; readIndex++)
            {
                uint current = ids[readIndex];
                if (current == previous)
                {
                    continue;
                }

                ids[writeIndex] = current;
                writeIndex++;
                previous = current;
            }

            if (writeIndex < ids.Count)
            {
                ids.RemoveRange(writeIndex, ids.Count - writeIndex);
            }
        }

        public static uint FirstOrZero(uint[] arr)
        {
            if (arr == null || arr.Length == 0)
            {
                return 0u;
            }

            return arr[0];
        }
    }
}
