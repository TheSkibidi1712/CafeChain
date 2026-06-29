using CafeChain.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Base controller cho tất cả POS API endpoints.
    /// 
    /// Cung cấp:
    ///   - [Authorize] — bắt buộc JWT/Cookie authentication
    ///   - [Route("api/v1/pos")] — base route prefix
    ///   - CurrentStoreId / CurrentStaffId — trích từ User Claims
    /// 
    /// Tất cả POS controllers kế thừa class này thay vì ControllerBase trực tiếp.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/v1/pos")]
    public abstract class PosApiController : ControllerBase
    {
        /// <summary>
        /// StoreId của nhân viên đang đăng nhập — trích từ JWT Claims.
        /// Throw UnauthorizedAccessException nếu claim không tồn tại.
        /// </summary>
        protected int CurrentStoreId => User.GetStoreId();

        /// <summary>
        /// StaffId của nhân viên đang đăng nhập — trích từ JWT Claims.
        /// Throw UnauthorizedAccessException nếu claim không tồn tại.
        /// </summary>
        protected int CurrentStaffId => User.GetStaffId();
    }
}
