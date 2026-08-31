using AutoMapper;
using VinayagaPlates.Domain.Entities;
using VinayagaPlates.Application.DTOs;

namespace VinayagaPlates.Api
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Role, RoleDto>().ReverseMap();
        }
    }
}
