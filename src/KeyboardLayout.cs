using System.Collections.Generic;
using System.Linq;
namespace YiErBuBu;
internal readonly record struct KeyShape(int Vk, string Caption, double X, double Y, double W, double H = 12)
{
    public double CenterX => X + W / 2;
    public double CenterY => Y + H / 2;
}
internal static class KeyboardLayout
{
    public static IReadOnlyList<KeyShape> Keys { get; } = Create();
    private static readonly Dictionary<int, KeyShape> ByVk = Keys.GroupBy(k => k.Vk).ToDictionary(g => g.Key, g => g.First());
    public static bool TryGetKey(int vk, out KeyShape shape) => ByVk.TryGetValue(vk, out shape);
    private static List<KeyShape> Create()
    {
        var keys = new List<KeyShape>();
        void Row(double x, double y, string labels, int[] vks, double unit = 20)
        {
            var words = labels.Split(' ');
            for (int i = 0; i < vks.Length; i++)
                keys.Add(new KeyShape(vks[i], words[i], x + i * unit, y, unit - 3, 13));
        }
        Row(238, 170, "1 2 3 4 5 6 7 8 9 0", new[]{49,50,51,52,53,54,55,56,57,48}, 19);
        Row(245, 186, "Q W E R T Y U I O P", new[]{81,87,69,82,84,89,85,73,79,80}, 19);
        Row(255, 202, "A S D F G H J K L", new[]{65,83,68,70,71,72,74,75,76}, 19);
        Row(268, 218, "Z X C V B N M", new[]{90,88,67,86,66,78,77}, 19);
        keys.Add(new KeyShape(32, "SPACE", 302, 234, 96, 13));
        keys.Add(new KeyShape(13, "↵", 421, 202, 24, 29));
        keys.Add(new KeyShape(8, "⌫", 421, 170, 24, 13));
        keys.Add(new KeyShape(160, "⇧", 242, 218, 23, 13));
        keys.Add(new KeyShape(161, "⇧", 405, 218, 27, 13));
        keys.Add(new KeyShape(17, "Ctrl", 240, 234, 25, 13));
        keys.Add(new KeyShape(18, "Alt", 271, 234, 25, 13));
        return keys;
    }
}
