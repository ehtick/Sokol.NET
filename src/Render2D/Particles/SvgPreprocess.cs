using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sokol.Render2D.Particles;

/// <summary>NanoSVG pre-processing before <c>nsvgParse</c> (mirrors <c>examples/NanoSVGDemo</c>'s
/// <c>OnSvgLoaded</c>). NanoSVG only honours inline <c>style=</c> attributes — not CSS class selectors —
/// and has no <c>&lt;clipPath&gt;</c> support (inner clip paths parse as solid black shapes that cover
/// the art). Inline the class rules and strip clipPaths so sprites rasterise with correct colours and
/// no black rectangles. (No white composite: particle sprites must keep their alpha.)</summary>
internal static class SvgPreprocess
{
    public static byte[] Apply(byte[] data) => StripClipPaths(InlineCssClasses(data));

    static byte[] InlineCssClasses(byte[] data)
    {
        string svg = Encoding.UTF8.GetString(data);
        var styleBlock = Regex.Match(svg, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline);
        if (!styleBlock.Success) return data;

        var classMap = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(styleBlock.Groups[1].Value, @"\.([\w-]+)\s*\{([^}]*)\}"))
            classMap[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        if (classMap.Count == 0) return data;

        string result = Regex.Replace(svg, @"class=""([\w\s-]+)""", m =>
        {
            var sb = new StringBuilder();
            foreach (var cls in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (classMap.TryGetValue(cls, out var props)) sb.Append(props).Append(';');
            return sb.Length > 0 ? $"style=\"{sb}\"" : m.Value;
        });
        return Encoding.UTF8.GetBytes(result);
    }

    static byte[] StripClipPaths(byte[] data)
    {
        string svg = Encoding.UTF8.GetString(data);
        svg = Regex.Replace(svg, @"<clipPath\b[^>]*>.*?</clipPath>", "", RegexOptions.Singleline);
        svg = Regex.Replace(svg, @"\s*clip-path=""[^""]*""", "");
        return Encoding.UTF8.GetBytes(svg);
    }
}
