using Geek.Server.Proto;
using System.Threading.Tasks;

namespace Geek.Server.Src.Login
{
    public class LoginCompAgent : QueryComponentAgent<LoginComp>
    {

        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public async Task<LoginRes> Login(ReqLogin reqLogin)
        {
            LoginRes res = new LoginRes();
            if (string.IsNullOrEmpty(reqLogin.UserName))
            {
                res.Success = false;
                res.ErrorMsg = "账号不能为空";
                return res;
            }

            if (reqLogin.Platform != "android" && reqLogin.Platform != "ios" && reqLogin.Platform != "unity")
            {
                //验证平台合法性
                res.Success = false;
                res.ErrorMsg = "未知的平台";
                return res;
            }

            if (reqLogin.SdkType > 0)
            {
                //在此处去SDK服务器验证Token
            }

            //验证通过

            //查询角色账号，这里设定每个服务器只能有一个角色
            var roleId = await GetRoleIdOfPlayer(reqLogin.UserName, reqLogin.SdkType);
            var isNewRole = roleId <= 0;
            if (isNewRole)
            {
                //没有老角色，创建新号
                roleId = EntityID.NewID(EntityType.Role);
                await CreateRoleToPlayer(reqLogin.UserName, reqLogin.SdkType, roleId);
                LOGGER.Info("创建新号:" + roleId);
            }
            res.isNewRole = isNewRole;
            res.RoleId = roleId;
            res.Success = true;
            return res;
        }

        public async Task<long> GetRoleIdOfPlayer(string userName, int sdkType)
        {
            var playerId = $"{sdkType}_{userName}";
            if (Comp.PlayerMap.TryGetValue(playerId, out var state))
            {
                if (state.RoleMap.TryGetValue(Settings.Ins.ServerId, out var roleId))
                    return roleId;
                return 0;
            }
            state = await Comp.LoadState(playerId, () =>
            {
                return new PlayerInfoState()
                {
                    Id = playerId,
                    UserName = userName,
                    SdkType = sdkType
                };
            });
            Comp.PlayerMap[playerId] = state;
            if (state.RoleMap.TryGetValue(Settings.Ins.ServerId, out var roleId2))
                return roleId2;
            return 0;
        }

        public Task CreateRoleToPlayer(string userName, int sdkType, long roleId)
        {
            var playerId = $"{sdkType}_{userName}";
            Comp.PlayerMap.TryGetValue(playerId, out var state);
            if (state == null)
            {
                state = new PlayerInfoState();
                state.Id = playerId;
                state.SdkType = sdkType;
                state.UserName = userName;
                Comp.PlayerMap[playerId] = state;
            }
            state.RoleMap[Settings.Ins.ServerId] = roleId;
            return Comp.SaveState(playerId, state);
        }


    }
}
