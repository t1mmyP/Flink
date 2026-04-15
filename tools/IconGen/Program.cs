using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Output path: Assets/flink.ico relative to solution root
string outputPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "flink.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

// Brand colors
var bgColor    = Color.FromArgb(255, 17, 19, 24);   // #111318
var accentColor = Color.FromArgb(255, 92, 184, 255); // #5CB8FF

int[] sizes = [256, 48, 32, 16];

var pngStreams = new List<byte[]>();

foreach (int size in sizes)
{
    using var bmp = DrawIcon(size, bgColor, accentColor);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    pngStreams.Add(ms.ToArray());
}

WriteIco(outputPath, sizes, pngStreams);
Console.WriteLine($"Icon written to: {outputPath}");

// ── Drawing ──────────────────────────────────────────────────────────────────

static Bitmap DrawIcon(int size, Color bg, Color accent)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);

    g.SmoothingMode    = SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    // Rounded rectangle background
    float radius = size * 0.22f;
    float pad    = size * 0.04f;
    var rect     = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);

    using var path = RoundedRect(rect, radius);
    using var bgBrush = new SolidBrush(bg);
    g.FillPath(bgBrush, path);

    // Subtle inner glow border
    using var borderPen = new Pen(Color.FromArgb(40, accent), size * 0.025f);
    g.DrawPath(borderPen, path);

    // Draw the "f"
    DrawLetterF(g, size, accent);

    return bmp;
}

static void DrawLetterF(Graphics g, int size, Color accent)
{
    // We draw the "f" manually as geometry so it looks sharp at every size.
    // It's a clean, modern lowercase f: vertical stroke + two horizontal bars.

    float s      = size;
    float stroke = s * 0.115f;   // thickness of the strokes
    float cx     = s * 0.5f;

    // Vertical stroke — slightly left of center
    float vx     = cx - stroke * 0.3f;
    float vTop   = s * 0.18f;
    float vBot   = s * 0.82f;

    // Top bar (shorter, sits at ~35%)
    float bar1Y  = s * 0.35f;
    float bar1L  = vx - stroke * 0.15f;
    float bar1R  = s * 0.73f;

    // Middle bar (longer, sits at ~53%)
    float bar2Y  = s * 0.53f;
    float bar2L  = vx - stroke * 0.15f;
    float bar2R  = s * 0.68f;

    using var brush = new SolidBrush(accent);
    using var pen   = new Pen(accent, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };

    // Vertical stroke with rounded top
    g.DrawLine(pen, vx + stroke / 2f, vTop + stroke / 2f, vx + stroke / 2f, vBot - stroke / 2f);

    // Top horizontal bar
    g.DrawLine(pen, bar1L, bar1Y + stroke / 2f, bar1R, bar1Y + stroke / 2f);

    // Middle horizontal bar
    g.DrawLine(pen, bar2L, bar2Y + stroke / 2f, bar2R - stroke * 0.3f, bar2Y + stroke / 2f);
}

static GraphicsPath RoundedRect(RectangleF bounds, float radius)
{
    float d = radius * 2;
    var path = new GraphicsPath();
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

// ── ICO writer ───────────────────────────────────────────────────────────────
// Modern ICO format embeds PNG streams directly (Vista+), giving crisp results.

static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    int count = sizes.Length;
    int headerSize = 6;
    int dirEntrySize = 16;
    int dataOffset = headerSize + dirEntrySize * count;

    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    // ICONDIR header
    bw.Write((ushort)0);     // reserved
    bw.Write((ushort)1);     // type: icon
    bw.Write((ushort)count);

    // Directory entries
    int offset = dataOffset;
    for (int i = 0; i < count; i++)
    {
        int sz = sizes[i];
        bw.Write((byte)(sz >= 256 ? 0 : sz));  // width (0 = 256)
        bw.Write((byte)(sz >= 256 ? 0 : sz));  // height
        bw.Write((byte)0);                      // color count
        bw.Write((byte)0);                      // reserved
        bw.Write((ushort)1);                    // planes
        bw.Write((ushort)32);                   // bit count
        bw.Write((uint)pngs[i].Length);         // size of image data
        bw.Write((uint)offset);                 // offset to image data
        offset += pngs[i].Length;
    }

    // PNG data
    foreach (var png in pngs)
        bw.Write(png);
}
