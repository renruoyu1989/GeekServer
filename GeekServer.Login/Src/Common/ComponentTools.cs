using Geek.Server.Src.Login;

namespace Geek.Server
{
    public class ComponentTools
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public static void RegistAll()
        {
            RegistServerComp<LoginComp>(EntityType.Login);
            RegistServerComp<LoginInsComp>(EntityType.LoginInstance);
        }

        static void RegistServerComp<TComp>(EntityType type) where TComp : BaseComponent, new()
        {
            CompSetting.Singleton.RegistComp<TComp>((int)type, true);
        }

    }
}