using System;

namespace Serein.Library.Network.WebSocketCommunication
{
    public interface ISocketHandleModule
    {
        Guid HandleGuid { get;  }
    }


    //[AutoRegister(RegisterSequence.FlowLoading)]
    //[AutoSocketModule(JsonThemeField = "theme", JsonDataField = "data")]
    //public class UserService : ISocketControlBase
    //{
    //    public Guid HandleGuid { get; } = new Guid(); 

    //    // Action<string> 类型是特殊的，会用一个委托代替，这个委托可以将文本信息发送到客户端
    //    // Action<object> 类型是特殊的，会用一个委托代替，这个委托可以将对象转成json发送到客户端

    //    [AutoSocketHandle]
    //    public void AddUser(User user,Action<string> Recover)
    //    {
    //        Console.WriteLine(user.ToString());
    //        Recover("ok");
    //    }

    //    [AutoSocketHandle(ThemeValue = "Remote")]
    //    public void DeleteUser(User user, Action<string> Recover)
    //    {
    //        Console.WriteLine(user.ToString());
    //    }

    //}

}
