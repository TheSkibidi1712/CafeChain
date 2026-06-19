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
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 🔥 NEW: Self reference (reply comment)
        public int? ParentRatingId { get; set; }
        public virtual Rating? ParentRating { get; set; }
        public virtual ICollection<Rating> Replies { get; set; } = new List<Rating>();

        public virtual Customer? Customer { get; set; }
        public virtual Drink? Drink { get; set; }

        public virtual ICollection<RatingImage> Images { get; set; } = new List<RatingImage>();

        // 🔥 NEW: reactions
        public virtual ICollection<RatingReaction> Reactions { get; set; } = new List<RatingReaction>();
    }
}
