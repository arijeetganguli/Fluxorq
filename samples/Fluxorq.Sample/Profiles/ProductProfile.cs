using Fluxorq.Sample.Models;
using Mapture;

namespace Fluxorq.Sample.Profiles;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForMember(d => d.InStock,
                opt => opt.MapFrom((Func<Product, bool>)(s => s.StockQuantity > 0)));
    }
}
