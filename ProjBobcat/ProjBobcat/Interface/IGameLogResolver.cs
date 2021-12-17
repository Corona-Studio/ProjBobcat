using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IGameLogResolver
{
    GameLogType ResolveLogType(string log);
    string ResolveSource(string log);
    string ResolveTime(string log);
    string ResolveTotalPrefix(string log);

    string ResolveStackTrace(string log);
    string ResolveExceptionMsg(string log);
}