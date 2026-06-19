using CafeChain.Models.Enums.Customer;
namespace CafeChain.Models.Customers
{
    public class RatingReaction
    {
        public int RatingReactionId { get; set; }

        public int RatingId { get; set; }
        public int CustomerId { get; set; }

        public ReactionType Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Rating Rating { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
