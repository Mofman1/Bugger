namespace Bugger.App;

internal static class ApplicationIcons
{
    public static Icon Normal { get; } = Load("AppIcon.Normal");
    public static Icon Alert { get; } = Load("AppIcon.Alert");
    private static Icon Load(string resource)
    {
        using var stream = typeof(ApplicationIcons).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Application icon is missing.");
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }

    public static Bitmap Folder()
    {
        var image = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(image);
        using var fill = new SolidBrush(Color.FromArgb(232, 191, 93));
        using var edge = new Pen(Color.FromArgb(137, 107, 43));
        Point[] points = [new(1, 4), new(2, 2), new(6, 2), new(8, 4), new(14, 4), new(14, 13), new(1, 13)];
        graphics.FillPolygon(fill, points); graphics.DrawPolygon(edge, points);
        graphics.DrawLine(edge, 2, 6, 13, 6);
        return image;
    }
}
