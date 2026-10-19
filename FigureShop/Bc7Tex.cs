// Bc7Tex - onceden BC7'ye cevrilmis dokulari yukler (ModDev\Files\Bc7\bc7_convert.py uretir).
// "<doku>.png/.jpg" yerine ayni klasorde "<doku>.bc7" varsa resim cozulmeden dogrudan ekran kartina yuklenir:
// RGBA32'ye gore 4 kat az ekran karti bellegi, acilista resim cozme yok. Dosya yoksa / bozuksa null doner, mod resme duser.
// FigureShop kopyasi (1.4.0): maxSize verilirse bundan buyuk en ust mip seviyeleri atlanir (daha az bellek).
//
// Dosya: "BC7T" | int32 surum (1) | int32 genislik | int32 yukseklik | int32 mip sayisi | tum mip'lerin BC7 bloklari (alttan uste, sRGB)

using System;
using System.IO;
using UnityEngine;

internal static class Bc7Tex
{
    static bool? _supported;

    public static Texture2D Load(string imagePath, int maxSize = 0)
    {
        try
        {
            string p = Path.ChangeExtension(imagePath, ".bc7");
            if (!File.Exists(p)) return null;
            if (_supported == null) _supported = SystemInfo.SupportsTextureFormat(TextureFormat.BC7);
            if (_supported == false) return null;
            byte[] b = File.ReadAllBytes(p);
            if (b.Length < 20 || b[0] != (byte)'B' || b[1] != (byte)'C' || b[2] != (byte)'7' || b[3] != (byte)'T') return null;
            int ver = BitConverter.ToInt32(b, 4), w = BitConverter.ToInt32(b, 8), h = BitConverter.ToInt32(b, 12), mips = BitConverter.ToInt32(b, 16);
            if (ver != 1 || w <= 0 || h <= 0 || mips <= 0 || mips > 16) return null;
            long need = 0;
            for (int i = 0; i < mips; i++)
            {
                int mw = Math.Max(1, w >> i), mh = Math.Max(1, h >> i);
                need += (long)((mw + 3) / 4) * ((mh + 3) / 4) * 16;
            }
            if (b.Length - 20 != need) return null;
            // buyuk ust seviyeleri atla (kalan taban 4'un kati olmali)
            int skip = 0; long off = 0;
            while (maxSize > 0 && skip < mips - 1 && Math.Max(w >> skip, h >> skip) > maxSize && ((w >> (skip + 1)) % 4 == 0) && ((h >> (skip + 1)) % 4 == 0))
            {
                off += (long)(((w >> skip) + 3) / 4) * (((h >> skip) + 3) / 4) * 16;
                skip++;
            }
            var data = new byte[need - off];
            Buffer.BlockCopy(b, 20 + (int)off, data, 0, data.Length);
            var t = new Texture2D(Math.Max(1, w >> skip), Math.Max(1, h >> skip), TextureFormat.BC7, mips - skip, false);
            t.LoadRawTextureData(data);
            t.Apply(false, true);   // CPU kopyasi tutulmaz
            return t;
        }
        catch { return null; }
    }
}
