using System;
using System.Collections.Generic;

namespace Liman.Core
{
    /// <summary>
    /// xorshift64* generator. System.Random's algorithm is not guaranteed across runtimes (Mono, IL2CPP, .NET),
    /// and levels are generated from a seed, so the same seed must give the same level on every device.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(int seed)
        {
            _state = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL);
            if (_state == 0) _state = 1;
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return unchecked(_state * 0x2545F4914F6CDD1DUL);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
