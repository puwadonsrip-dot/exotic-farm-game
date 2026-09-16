using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สร้างรูปทรงพื้นฐานสำหรับ UI ด้วยโค้ด — ไม่ต้องมีไฟล์ภาพ
///
/// ใช้กับ Image แบบ Sliced ได้เลย ยืดเท่าไหร่มุมโค้งก็ไม่เพี้ยน
/// </summary>
public static class UIShapes
{
    private static readonly Dictionary<int, Sprite> s_Cache = new Dictionary<int, Sprite>();
    private static Sprite s_Circle;

    /// <summary>วงกลมสีขาวล้วน ขอบเนียน (ไปใส่สีจริงที่ Image.color)</summary>
    public static Sprite Circle()
    {
        if (s_Circle != null) return s_Circle;

        const int size = 256;
        float r = size * 0.5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Circle"
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01(r - 0.5f - d);   // ขอบเนียน 1 พิกเซล
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        s_Circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        s_Circle.name = "Circle";
        return s_Circle;
    }

    private static Sprite s_Book;

    /// <summary>
    /// ไอคอนสมุดบันทึก วาดด้วยโค้ด
    /// สีถูกวาดฝังมาเลย ไม่ต้องไปใส่สีที่ Image.color (ใส่ขาวไว้)
    /// </summary>
    public static Sprite Book()
    {
        if (s_Book != null) return s_Book;

        const int size = 128;

        var cover = new Color32(150, 96, 52, 255);
        var spine = new Color32(104, 64, 33, 255);
        var pages = new Color32(244, 238, 222, 255);
        var band = new Color32(214, 176, 92, 255);
        var line = new Color32(58, 40, 24, 255);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Book"
        };

        var pixels = new Color32[size * size];   // โปร่งใสหมดตั้งแต่แรก

        void Fill(int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;
                    pixels[(size - 1 - y) * size + x] = c;   // วาดจากบนลงล่าง
                }
            }
        }

        Fill(18, 14, 92, 100, line);    // ขอบดำรอบเล่ม
        Fill(21, 17, 86, 94, cover);    // ปกหนังสือ
        Fill(21, 17, 20, 94, spine);    // สันปก
        Fill(41, 17, 3, 94, line);      // เส้นแบ่งสัน
        Fill(96, 20, 11, 88, pages);    // ขอบกระดาษด้านขวา
        Fill(52, 40, 40, 8, band);      // แถบทองบนปก
        Fill(52, 60, 40, 5, band);
        Fill(52, 74, 28, 5, band);

        tex.SetPixels32(pixels);
        tex.Apply();

        s_Book = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        s_Book.name = "Book";
        return s_Book;
    }

    private static Sprite s_Gear;

    /// <summary>ไอคอนฟันเฟือง วาดด้วยโค้ด ไม่ต้องมีไฟล์ภาพ</summary>
    public static Sprite Gear()
    {
        if (s_Gear != null) return s_Gear;

        const int size = 256;
        const int teeth = 8;

        float c = size * 0.5f;
        float bodyR = size * 0.325f;   // วงกลมตัวเฟือง
        float toothR = size * 0.455f;  // ปลายฟัน
        float holeR = size * 0.145f;   // รูตรงกลาง

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Gear"
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;

                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // คลื่นรอบวง ยอดคลื่น = ฟันเฟือง
                float wave = Mathf.Cos(angle * teeth);
                float edge = wave > 0.30f ? toothR : bodyR;

                float outer = Mathf.Clamp01(edge - d);      // ขอบนอก
                float inner = Mathf.Clamp01(d - holeR);     // เจาะรูตรงกลาง

                float a = Mathf.Min(outer, inner);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        s_Gear = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        s_Gear.name = "Gear";
        return s_Gear;
    }

    /// <summary>สี่เหลี่ยมมุมโค้ง สีขาวล้วน (ไปใส่สีจริงที่ Image.color)</summary>
    public static Sprite RoundedRect(int radius = 22)
    {
        radius = Mathf.Clamp(radius, 2, 96);
        if (s_Cache.TryGetValue(radius, out var cached) && cached != null) return cached;

        const int pad = 2;
        int size = radius * 2 + pad * 2 + 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = $"RoundedRect{radius}"
        };

        var pixels = new Color32[size * size];
        float r = radius;
        float lo = pad + r;
        float hi = size - pad - r;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // จุดที่ใกล้ที่สุดบน "แกนกลาง" ของสี่เหลี่ยม แล้ววัดระยะไปหามัน
                float cx = Mathf.Clamp(px, lo, hi);
                float cy = Mathf.Clamp(py, lo, hi);
                float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));

                float a = Mathf.Clamp01(r + 0.5f - d);   // ขอบเนียน 1 พิกเซล
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        int border = radius + pad;
        var sprite = Sprite.Create(
            tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = tex.name;

        s_Cache[radius] = sprite;
        return sprite;
    }
}
