using CafeChain.Models.Drinks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Customers
{
    public class Rating
    {
        public int RatingId { get; set; }
        public int? CustomerId { get; set; }
        public int? DrinkId { get; set; }
        public int Stars { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public virtual Customer? Customer { get; set; }
        public virtual Drink? Drink { get; set; }
        public virtual ICollection<RatingImage> Images { get; set; } // 🔥 NEW

    }
}
