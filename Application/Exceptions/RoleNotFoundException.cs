using System;

namespace CafeChain.Application.Exceptions
{
    public class RoleNotFoundException : Exception
    {
        public RoleNotFoundException(string roleName) 
            : base($"Không tìm thấy quyền '{roleName}' trong hệ thống. Vui lòng kiểm tra lại cấu hình DB Seeding.")
        {
        }
    }
}
