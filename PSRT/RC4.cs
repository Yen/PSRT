using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    static class RC4
    {
        public static byte[] Encrypt(byte[] key, byte[] data)
        {
            return _EncryptOutput(key, data).ToArray();
        }

        public static byte[] Decrypt(byte[] key, byte[] data)
        {
            return _EncryptOutput(key, data).ToArray();
        }

        static byte[] _EncryptInitalize(byte[] key)
        {
            var s = Enumerable.Range(0, 256)
              .Select(i => (byte)i)
              .ToArray();

            for (int i = 0, j = 0; i < 256; i++)
            {
                j = (j + key[i % key.Length] + s[i]) & 255;

                _Swap(s, i, j);
            }

            return s;
        }

        static IEnumerable<byte> _EncryptOutput(byte[] key, IEnumerable<byte> data)
        {
            var s = _EncryptInitalize(key);

            int i = 0;
            int j = 0;

            return data.Select((b) =>
            {
                i = (i + 1) & 255;
                j = (j + s[i]) & 255;

                _Swap(s, i, j);

                return (byte)(b ^ s[(s[i] + s[j]) & 255]);
            });
        }

        static void _Swap(byte[] s, int i, int j)
        {
            var c = s[i];

            s[i] = s[j];
            s[j] = c;
        }
    }
}
