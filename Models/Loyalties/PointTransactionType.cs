namespace CafeChain.Models.Loyalties
{
    public class PointTransactionType
    {
        public int Id { get; set; }
        public string Code { get; set; } // EARN, SPEND, EXPIRE
        public string Name { get; set; }

        public virtual ICollection<PointTransaction> Transactions { get; set; }
    }
}
