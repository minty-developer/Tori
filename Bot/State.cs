using DotNetEnv;

public static class BotEnv
{
    public readonly static bool isDev = true;
    public readonly static string botVersion = "2.0.0";
    
    public static void CheckEnv() {
        if(isDev) Env.Load();
    }
}