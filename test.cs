using System;
using System.IO;
using System.Linq;

class Program {
    static void Main() {
        var invalid = Path.GetInvalidFileNameChars();
        string val = "..";
        Console.WriteLine(new string(val.Select(c => invalid.Contains(c) ? '-' : c).ToArray()));
    }
}
