// FigureShop - SKIN1/SKIN2 model okuyucu ve sabit poza "pisirme" (CPU skinning).
// Model dosyasi AnimeStatuePack ile ayni formatta (model.skin). Rafta urunler GPU ile toplu cizildigi icin
// iskeletli (SkinnedMeshRenderer) degil, secilen pozda dondurulmus tek bir statik mesh kullaniyoruz.
// Hesap SkinnedMeshRenderer.BakeMesh ile ayni: v' = sum(w_i * node_i * bindPose_i * v). Unity matematik
// cagrilari IL2CPP uzerinden yavas oldugu icin dongulerde System.Numerics / duz float kullaniliyor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NV3 = System.Numerics.Vector3;

namespace FigureShop
{
    // 3x4 afin matris (sutun vektor: p' = A * p)
    internal struct Aff
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23;

        public static Aff Identity => new Aff { m00 = 1, m11 = 1, m22 = 1 };

        // satir satir 16 float (son satir 0 0 0 1)
        public static Aff FromRowMajor(float[] a) => new Aff
        {
            m00 = a[0], m01 = a[1], m02 = a[2], m03 = a[3],
            m10 = a[4], m11 = a[5], m12 = a[6], m13 = a[7],
            m20 = a[8], m21 = a[9], m22 = a[10], m23 = a[11],
        };

        public static Aff TRS(NV3 t, float qx, float qy, float qz, float qw, NV3 s)
        {
            float len = MathF.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (len > 1e-8f) { qx /= len; qy /= len; qz /= len; qw /= len; } else { qx = qy = qz = 0; qw = 1; }
            float xx = qx * qx, yy = qy * qy, zz = qz * qz, xy = qx * qy, xz = qx * qz, yz = qy * qz, wx = qw * qx, wy = qw * qy, wz = qw * qz;
            return new Aff
            {
                m00 = (1 - 2 * (yy + zz)) * s.X, m01 = 2 * (xy - wz) * s.Y, m02 = 2 * (xz + wy) * s.Z, m03 = t.X,
                m10 = 2 * (xy + wz) * s.X, m11 = (1 - 2 * (xx + zz)) * s.Y, m12 = 2 * (yz - wx) * s.Z, m13 = t.Y,
                m20 = 2 * (xz - wy) * s.X, m21 = 2 * (yz + wx) * s.Y, m22 = (1 - 2 * (xx + yy)) * s.Z, m23 = t.Z,
            };
        }

        public static Aff operator *(Aff a, Aff b) => new Aff
        {
            m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20,
            m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21,
            m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,
            m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m02 * b.m23 + a.m03,
            m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20,
            m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21,
            m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,
            m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m12 * b.m23 + a.m13,
            m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20,
            m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21,
            m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22,
            m23 = a.m20 * b.m03 + a.m21 * b.m13 + a.m22 * b.m23 + a.m23,
        };

        public NV3 Point(NV3 p) => new NV3(m00 * p.X + m01 * p.Y + m02 * p.Z + m03, m10 * p.X + m11 * p.Y + m12 * p.Z + m13, m20 * p.X + m21 * p.Y + m22 * p.Z + m23);
        public NV3 Vector(NV3 v) => new NV3(m00 * v.X + m01 * v.Y + m02 * v.Z, m10 * v.X + m11 * v.Y + m12 * v.Z, m20 * v.X + m21 * v.Y + m22 * v.Z);
    }

    internal class SkinNode { public string Name; public int Parent; public NV3 T; public float Qx, Qy, Qz, Qw; public NV3 S; }
    internal class SkinSkin { public int[] Joints; public Aff[] BindPoses; }
    internal class SkinChannel { public int Node; public int Path; public float[] Data; }
    internal class SkinClip { public string Name; public float Fps; public int Frames; public List<SkinChannel> Channels = new(); }

    internal class SkinMesh
    {
        public string Name, Texture; public int ClipMode, Skin; public bool DoubleSided;
        public NV3[] Pos, Nrm; public float[] Uv; public int[] J; public float[] W; public int[] Idx;
    }

    // pisirilmis (pozlanmis) tek parca: konum/normal model uzayinda
    internal class BakedPart { public SkinMesh Src; public NV3[] Pos, Nrm; }

    internal class SkinModel
    {
        public float Scale, CenterX, Floor, CenterZ;
        public List<SkinNode> Nodes = new();
        public List<SkinSkin> Skins = new();
        public List<SkinMesh> Meshes = new();
        public List<SkinClip> Clips = new();

        static string Str(BinaryReader br) => Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
        static NV3 V3(BinaryReader br) => new NV3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        public static SkinModel Load(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));
            string magic = Encoding.ASCII.GetString(br.ReadBytes(5));
            if (magic != "SKIN1" && magic != "SKIN2") throw new Exception("Gecersiz model dosyasi: " + magic);
            var f = new SkinModel();
            f.Scale = br.ReadSingle(); f.CenterX = br.ReadSingle(); f.Floor = br.ReadSingle(); f.CenterZ = br.ReadSingle();
            int nn = br.ReadInt32();
            for (int i = 0; i < nn; i++)
            {
                var n = new SkinNode { Name = Str(br), Parent = br.ReadInt32() };
                n.T = V3(br);
                n.Qx = br.ReadSingle(); n.Qy = br.ReadSingle(); n.Qz = br.ReadSingle(); n.Qw = br.ReadSingle();
                n.S = V3(br);
                f.Nodes.Add(n);
            }
            int ns = br.ReadInt32();
            for (int i = 0; i < ns; i++)
            {
                int jc = br.ReadInt32();
                var s = new SkinSkin { Joints = new int[jc], BindPoses = new Aff[jc] };
                for (int j = 0; j < jc; j++) s.Joints[j] = br.ReadInt32();
                var a = new float[16];
                for (int j = 0; j < jc; j++)
                {
                    for (int k = 0; k < 16; k++) a[k] = br.ReadSingle(); // satir satir
                    s.BindPoses[j] = Aff.FromRowMajor(a);
                }
                f.Skins.Add(s);
            }
            int nm = br.ReadInt32();
            for (int i = 0; i < nm; i++)
            {
                var m = new SkinMesh { Name = Str(br), Texture = Str(br) };
                m.ClipMode = br.ReadByte(); m.DoubleSided = br.ReadByte() != 0; m.Skin = br.ReadInt32();
                int v = br.ReadInt32();
                m.Pos = new NV3[v]; m.Nrm = new NV3[v]; m.Uv = new float[v * 2]; m.J = new int[v * 4]; m.W = new float[v * 4];
                for (int k = 0; k < v; k++) m.Pos[k] = V3(br);
                for (int k = 0; k < v; k++) m.Nrm[k] = V3(br);
                for (int k = 0; k < v * 2; k++) m.Uv[k] = br.ReadSingle();
                for (int k = 0; k < v * 4; k++) m.J[k] = br.ReadInt32();
                for (int k = 0; k < v * 4; k++) m.W[k] = br.ReadSingle();
                int ic = br.ReadInt32();
                m.Idx = new int[ic];
                for (int k = 0; k < ic; k++) m.Idx[k] = br.ReadInt32();
                f.Meshes.Add(m);
            }
            int nclips = magic == "SKIN1" ? 1 : br.ReadInt32();
            for (int ci = 0; ci < nclips; ci++)
            {
                var cl = new SkinClip { Name = magic == "SKIN1" ? "Animation" : Str(br) };
                cl.Fps = br.ReadSingle(); cl.Frames = br.ReadInt32();
                int nc = br.ReadInt32();
                for (int i = 0; i < nc; i++)
                {
                    var c = new SkinChannel { Node = br.ReadInt32(), Path = br.ReadByte() };
                    int k = c.Path == 1 ? 4 : 3;
                    c.Data = new float[cl.Frames * k];
                    for (int j = 0; j < c.Data.Length; j++) c.Data[j] = br.ReadSingle();
                    cl.Channels.Add(c);
                }
                if (cl.Frames > 0) f.Clips.Add(cl);
            }
            return f;
        }

        // Dugumlerin model uzayindaki matrisleri: dinlenme pozu, istenirse bir klibin bir karesi uygulanmis.
        public Aff[] PoseMatrices(SkinClip clip, float time)
        {
            int n = Nodes.Count;
            var T = new NV3[n]; var S = new NV3[n]; var Q = new float[n * 4];
            for (int i = 0; i < n; i++)
            {
                var nd = Nodes[i];
                T[i] = nd.T; S[i] = nd.S;
                Q[i * 4] = nd.Qx; Q[i * 4 + 1] = nd.Qy; Q[i * 4 + 2] = nd.Qz; Q[i * 4 + 3] = nd.Qw;
            }
            if (clip != null && clip.Frames > 0)
            {
                int fr = Math.Clamp((int)(time * clip.Fps), 0, clip.Frames - 1);
                foreach (var c in clip.Channels)
                {
                    if (c.Node < 0 || c.Node >= n) continue;
                    var d = c.Data;
                    if (c.Path == 1) { int p = fr * 4; for (int k = 0; k < 4; k++) Q[c.Node * 4 + k] = d[p + k]; }
                    else
                    {
                        int p = fr * 3; var v = new NV3(d[p], d[p + 1], d[p + 2]);
                        if (c.Path == 0) T[c.Node] = v; else S[c.Node] = v;
                    }
                }
            }
            var W = new Aff[n]; var done = new bool[n];
            Aff Get(int i)
            {
                if (done[i]) return W[i];
                var l = Aff.TRS(T[i], Q[i * 4], Q[i * 4 + 1], Q[i * 4 + 2], Q[i * 4 + 3], S[i]);
                int p = Nodes[i].Parent;
                done[i] = true;   // dongu korumasi
                W[i] = p >= 0 && p < n && p != i ? Get(p) * l : l;
                return W[i];
            }
            for (int i = 0; i < n; i++) Get(i);
            return W;
        }

        // Her parcayi pozda dondur (BakeMesh esdegeri)
        public List<BakedPart> Bake(SkinClip clip, float time)
        {
            var world = PoseMatrices(clip, time);
            var res = new List<BakedPart>();
            foreach (var m in Meshes)
            {
                var skin = m.Skin >= 0 && m.Skin < Skins.Count ? Skins[m.Skin] : null;
                Aff[] sk = null;
                if (skin != null)
                {
                    sk = new Aff[skin.Joints.Length];
                    for (int j = 0; j < sk.Length; j++)
                    {
                        int node = skin.Joints[j];
                        sk[j] = (node >= 0 && node < world.Length ? world[node] : Aff.Identity) * skin.BindPoses[j];
                    }
                }
                int v = m.Pos.Length;
                var pos = new NV3[v]; var nrm = new NV3[v];
                for (int i = 0; i < v; i++)
                {
                    var p = m.Pos[i]; var nr = m.Nrm[i];
                    if (sk == null) { pos[i] = p; nrm[i] = nr; continue; }
                    NV3 ap = NV3.Zero, an = NV3.Zero; float ws = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        float w = m.W[i * 4 + k];
                        if (w <= 0) continue;
                        int j = m.J[i * 4 + k];
                        if (j < 0 || j >= sk.Length) continue;
                        ap += sk[j].Point(p) * w; an += sk[j].Vector(nr) * w; ws += w;
                    }
                    if (ws < 1e-6f) { pos[i] = p; nrm[i] = nr; continue; }
                    pos[i] = ap / ws;
                    float l = an.Length();
                    nrm[i] = l > 1e-8f ? an / l : nr;
                }
                res.Add(new BakedPart { Src = m, Pos = pos, Nrm = nrm });
            }
            return res;
        }
    }
}
