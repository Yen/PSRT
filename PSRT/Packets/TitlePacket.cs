using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    // TODO: not finished

    class TitlePacket : Packet
    {
        // lmao magic numbers
        private const uint PacketXor = 0x57e6;
        private const uint PacketSub = 0x92;

        public struct TitleInfo
        {
            public uint Id;
            public string Name;
        }

        public List<TitleInfo> Titles = new List<TitleInfo>();

        public TitlePacket(Packet packet) : base(packet)
        {
            var titleCountBuffer = Body.Take(4).ToArray();
            var titleCountXor = BitConverter.ToUInt32(titleCountBuffer, 0);
            var titleCount = checked((int)SubXor(titleCountXor, PacketSub, PacketXor));

            var titleIdsOffset = 4;
            var titleIds = new uint[titleCount];
            for (int i = 0; i < titleCount; i++)
                titleIds[i] = BitConverter.ToUInt32(Body, titleIdsOffset + i * 4);

            var characterCountOffset = titleIdsOffset + titleCount * 4;
            var characterCountBuffer = Body.Skip(characterCountOffset).Take(4).ToArray();
            var characterCountXor = BitConverter.ToUInt32(characterCountBuffer, 0);
            var characterCount = checked((int)SubXor(characterCountXor, PacketSub, PacketXor));

            var charactersBufferOffset = characterCountOffset + 4;
            // the byte length of characters must be a multiple of 4 so add alignment on when reading
            charactersBufferOffset += (characterCount * 2) % 4;

            var charactersBuffer = Body.Skip(charactersBufferOffset).Take(characterCount * 2).ToArray();

            var titleNameLengthsCountOffset = charactersBufferOffset + characterCount * 2;
            var titleNameLengthsCountBuffer = Body.Skip(titleNameLengthsCountOffset).Take(4).ToArray();
            var titleNameLengthsCountXor = BitConverter.ToUInt32(titleNameLengthsCountBuffer, 0);
            var titleNameLengthsCount = checked((int)SubXor(titleNameLengthsCountXor, PacketSub, PacketXor));

            var titleNameLenghtsOffset = titleNameLengthsCountOffset + 4;
            var titleNameLengths = Body.Skip(titleNameLenghtsOffset).Take(titleCount).ToArray();

            var characterPosition = 0;
            for (var i = 0; i < titleCount; i++)
            {
                var length = titleNameLengths[i];
                var name = Encoding.Unicode.GetString(charactersBuffer, characterPosition * 2, length * 2);

                Titles.Add(new TitleInfo
                {
                    Id = titleIds[i],
                    Name = name
                });

                characterPosition += length;
            }

            return;
        }

        public override byte[] ToBytes()
        {
            var titleCountXor = AddXor(checked((uint)Titles.Count), PacketSub, PacketXor);
            var titleCountBuffer = BitConverter.GetBytes(titleCountXor);

            var titleIdsBuffer = Titles.SelectMany(x => BitConverter.GetBytes(x.Id));

            var characters = Titles.SelectMany(x => x.Name).ToList();

            // character bytes must be aligned to 4 so align encoded characters to 2
            if (characters.Count() % 2 != 0)
                characters.Add('\0');

            var characterCount = characters.Count();
            var characterCountXor = AddXor(checked((uint)characterCount), PacketSub, PacketXor);
            var characterCountBuffer = BitConverter.GetBytes(characterCountXor);

            var charactersBuffer = Encoding.Unicode.GetBytes(characters.ToArray());

            var titleNameLengthsBuffer = Titles.Select(x => checked((byte)x.Name.Length)).ToList();

            // this data block must also be aligned to 4 for sega
            var titleNameLengthsAlign = titleNameLengthsBuffer.Count % 4;
            if (titleNameLengthsAlign != 0)
                titleNameLengthsBuffer.AddRange(Enumerable.Repeat<byte>(0, titleNameLengthsAlign));

            //

            var generatedBody = new List<byte>();
            generatedBody.AddRange(titleCountBuffer);
            generatedBody.AddRange(titleIdsBuffer);
            generatedBody.AddRange(characterCountBuffer);
            generatedBody.AddRange(charactersBuffer);
            generatedBody.AddRange(titleCountBuffer);
            generatedBody.AddRange(titleNameLengthsBuffer);

            Body = generatedBody.ToArray();
            return base.ToBytes();
        }
    }
}
