using System;
using System.Collections.Generic;
namespace YiErBuBu;
internal readonly record struct KeyShape(int Vk, string Caption, double X, double Y, double W);
internal static class KeyboardLayout
{
    // Compact decorative keycaps with a physical-key identity. X/Y in a 490x245 scene.
    public static IReadOnlyList<KeyShape> Keys { get; } = Create();
    private static List<KeyShape> Create()
    {
        var keys = new List<KeyShape>();
        void Row(double x, double y, string labels, int[] vks, double unit = 17)
        {
            var words = labels.Split(' ');
            for (int i = 0; i < vks.Length; i++)
                keys.Add(new KeyShape(vks[i], words[i], x + i * unit, y, unit - 2));
        }
        Row(218, 172, "1 2 3 4 5 6 7 8 9 0", new[]{49,50,51,52,53,54,55,56,57,48});
        Row(223, 187, "Q W E R T Y U I O P", new[]{81,87,69,82,84,89,85,73,79,80});
        Row(231, 202, "A S D F G H J K L", new[]{65,83,68,70,71,72,74,75,76});
        Row(242, 217, "Z X C V B N M", new[]{90,88,67,86,66,78,77});
        keys.Add(new KeyShape(32, "SPACE", 284, 232, 84));
        keys.Add(new KeyShape(13, "↵", 386, 202, 23));
        keys.Add(new KeyShape(8, "⌫", 392, 172, 23));
        keys.Add(new KeyShape(160, "⇧", 220, 217, 20));
        keys.Add(new KeyShape(161, "⇧", 368, 217, 24));
        return keys;
    }
}
