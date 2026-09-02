using Microsoft.AspNetCore.Authorization;
using VinayagaPlates.Application.Constants;

namespace VinayagaPlates.Application.Security
{
    public class MasterAuthorizeAttribute : AuthorizeAttribute
    {
        public MasterAuthorizeAttribute()
        {
            Policy = "MasterPolicy";
        }
    }
}
