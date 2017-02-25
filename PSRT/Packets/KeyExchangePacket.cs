using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class KeyExchangePacket : Packet
    {
        public byte[] Token { get; private set; }
        public byte[] RC4Key { get; private set; }

        public KeyExchangePacket(Packet packet) : base(packet.Signature, packet.Body)
        {
            try
            {
                using (var psrtRSA = new RSACryptoServiceProvider())
                using (var segaRSA = new RSACryptoServiceProvider())
                {
                    // load keys
                    var psrtBlob = File.ReadAllBytes("Resources/privatekey.blob");
                    psrtRSA.ImportCspBlob(psrtBlob);

                    var segaBlob = File.ReadAllBytes("Resources/sega-publickey.blob");
                    segaRSA.ImportCspBlob(segaBlob);

                    var psrtDecrypt = new RSAPKCS1KeyExchangeDeformatter(psrtRSA);
                    var segaEncrypt = new RSAPKCS1KeyExchangeFormatter(segaRSA);

                    // extract data
                    var encrypted = new byte[128];
                    Array.Copy(Body, encrypted, encrypted.Length);
                    Array.Reverse(encrypted);

                    // decrypt
                    var decrypted = psrtDecrypt.DecryptKeyExchange(encrypted);
                    
                    Token = decrypted.Take(16).ToArray();
                    RC4Key = decrypted.Skip(16).Take(16).ToArray();

                    // reencrypt
                    var reencrypted = segaEncrypt.CreateKeyExchange(decrypted);

                    // save reencrypted data
                    Array.Reverse(reencrypted);
                    Array.Copy(reencrypted, Body, reencrypted.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred decoding encrypted key -> {ex.Message}");
                throw;
            }
        }
    }
}
