#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Godot;

public record AtlasRegion(string Name, Rect2I Bounds, bool Rotated);

public static class SpineAtlasParser
{
    public static (string PngName, float Scale, List<AtlasRegion> Regions) Parse(string atlasText)
    {
        var lines = atlasText.Split('\n');
        var regions = new List<AtlasRegion>();

        string pngName = "";
        float scale = 1f;
        int i = 0;

        // 첫 번째 PNG 이름 찾기 (콜론 없는 첫 줄)
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.Length > 0 && !line.Contains(':'))
            {
                pngName = line;
                i++;
                break;
            }
            i++;
        }

        // 헤더 파싱
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("scale:"))
                scale = float.Parse(line.Substring(6), CultureInfo.InvariantCulture);

            if (line.Length > 0 && !line.Contains(':'))
                break;
            i++;
        }

        // 리전 파싱
        while (i < lines.Length)
        {
            var nameLine = lines[i].Trim();
            i++;

            if (nameLine.Length == 0 || nameLine.Contains(':'))
                continue;

            Rect2I bounds = default;
            bool rotated = false;

            while (i < lines.Length)
            {
                var propLine = lines[i].Trim();
                if (propLine.Length == 0 || !propLine.Contains(':'))
                    break;

                if (propLine.StartsWith("bounds:"))
                {
                    var p = propLine.Substring(7).Split(',');
                    bounds = new Rect2I(
                        int.Parse(p[0].Trim()),
                        int.Parse(p[1].Trim()),
                        int.Parse(p[2].Trim()),
                        int.Parse(p[3].Trim())
                    );
                }
                else if (propLine.StartsWith("rotate:"))
                {
                    var val = propLine.Substring(7).Trim();
                    rotated = val != "false" && val != "0";
                }
                i++;
            }

            if (bounds.Size.X > 0 && bounds.Size.Y > 0)
                regions.Add(new AtlasRegion(nameLine, bounds, rotated));
        }

        return (pngName, scale, regions);
    }
}
