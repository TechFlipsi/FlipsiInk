// FlipsiInk - Global Type Aliases
// Resolves ambiguity between System.Drawing and WPF types
#nullable enable
global using WpfColor = System.Windows.Media.Color;
global using WpfPoint = System.Windows.Point;
global using WpfSize = System.Windows.Size;
global using WpfBrush = System.Windows.Media.Brush;
global using WpfImage = System.Windows.Controls.Image;
global using DBitmap = System.Drawing.Bitmap;
global using DGraphics = System.Drawing.Graphics;
global using DRectangle = System.Drawing.Rectangle;
global using DPixelFormat = System.Drawing.Imaging.PixelFormat;
global using DImageFormat = System.Drawing.Imaging.ImageFormat;
global using DSize = System.Drawing.Size;