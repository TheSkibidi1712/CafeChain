namespace CafeChain.Application.DTOs.Admin.Sizes
{
    public class SizeDto
    {
        public int SizeId { get; set; }
        public string SizeCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public CafeChain.Models.Enums.Drink.SizeTypeEnum SizeType { get; set; } =
            CafeChain.Models.Enums.Drink.SizeTypeEnum.Cup;
        public bool Active { get; set; }
    }
}
