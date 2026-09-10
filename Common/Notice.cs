using HandyControl.Controls;
using HandyControl.Data;

namespace RaveStudioAI.Common;

public static class Notice
{
    public static void Success(string message, int seconds = 3) =>
        Growl.Success(new GrowlInfo { Message = message, WaitTime = seconds });

    public static void Warning(string message, int seconds = 3) =>
        Growl.Warning(new GrowlInfo { Message = message, WaitTime = seconds });

    public static void Error(string message, int seconds = 5) =>
        Growl.Error(new GrowlInfo { Message = message, WaitTime = seconds });
}
