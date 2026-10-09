Add-Type -TypeDefinition @'
using System;
using System.IO;
public class T {
    public static void Test(string p) {
        try {
            Directory.CreateDirectory(p);
            File.WriteAllText(Path.Combine(p, "x.txt"), "x");
            File.Delete(Path.Combine(p, "x.txt"));
            Directory.Delete(p);
            Console.WriteLine("OK: " + p);
        } catch (Exception e) {
            Console.WriteLine("FAIL: " + p + " -> " + e.GetType().Name + ": " + e.Message);
        }
    }
}
'@
# 仓库内的临时目录（sandbox 应该允许 workspace-write）
[T]::Test('D:\kudi-project\world-view\tests\WorldView.Tests\.tmp-probe')
[T]::Test('D:\kudi-project\world-view\.tmp-probe')
