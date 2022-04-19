using Geek.Server.Proto;
using Geek.Server.Src.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geek.Server
{

    public class LoginRes
    {
        public string UserName { get; set; }
        /// <summary>
        /// 此处设定一个服只有一个角色
        /// </summary>
        public long RoleId { get; set; }
        /// <summary>
        /// 是否为新角色
        /// </summary>
        public bool isNewRole { get; set; }
        public bool Success { get; set; }
        public string ErrorMsg { get; set; }
    }


    public class LoginInsComp : NoHotfixComponent
    {
    }

    public class LoginInsCompAgent : FuncComponentAgent<LoginInsComp>
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<LoginRes> Login(byte[] msg)
        {
            try
            {
                ReqLogin req = new ReqLogin();
                req.Read(msg, 0);

                //TODO:可以分配多个登录Actor进行登录
                var loginComp = await EntityMgr.GetCompAgent<LoginCompAgent>(EntityType.Login);
                var res = await loginComp.Login(req);

                return res; 
            }
            catch (Exception e)
            {
                LOGGER.Error(e.ToString());
                LoginRes res = new LoginRes();
                res.Success = false;
                res.ErrorMsg = "解析登录协议失败";
                return res;
            }
        }
    }

}
