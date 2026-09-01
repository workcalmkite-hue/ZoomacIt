using System.IO;
using System.Text;

namespace ZoomacItWin.Core;

/// <summary>
/// %APPDATA%\ZoomacItWin\log.txt 에 남기는 최소 로그. 실행할 때마다 새로 쓴다.
/// 단축키가 안 먹을 때(다른 프로그램이 이미 쓰는 경우) 원인을 확인하는 용도.
/// </summary>
internal static class Log
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZoomacItWin", "log.txt");

    public static void Reset()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // 메모장·PowerShell이 한글을 깨지 않고 읽도록 BOM 있는 UTF-8로 쓴다.
            File.WriteAllText(FilePath,
                $"ZoomacIt for Windows — {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                new UTF8Encoding(true));
        }
        catch
        {
            // 로그를 못 써도 앱은 정상 동작해야 한다.
        }
    }

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}",
                new UTF8Encoding(true));
        }
        catch
        {
        }
    }
}
